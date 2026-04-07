using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public struct RollbackNetworkSettings
{
    public int maxRollbackFrames;
    public int inputDelayFrames;
    public int desyncAbortThreshold;
}

[System.Serializable]
public class NetworkSyncController
{
    [SerializeField] private RollbackNetworkSettings networkSettings = new RollbackNetworkSettings { maxRollbackFrames = 7, inputDelayFrames = 2, desyncAbortThreshold = 5 };

    private P2PNetworkManager currentP2PNetwork;
    private LocalInputProvider inputProvider;
    private GameStateSnapshot[] stateBuffer;
    
    private NetworkSyncState syncState;
    private InputFlags[] p1InputBuffer;
    private InputFlags[] p2InputBuffer;
    private Dictionary<int, ulong> localHashBuffer;
    
    private int localPlayerSlot;
    private int rollbackWindow;
    private int syncVerifyInterval;
    
    private Action<int, int> onResimulateRequired;
    private Action onDesyncAborted;

    /*
     * 네트워크 동기화 컨트롤러의 참조와 콜백을 설정하고 내부 버퍼를 초기화합니다.
     */
    public void Initialize(P2PNetworkManager p2pNetwork, LocalInputProvider provider, GameStateSnapshot[] states, int slot, int window, int verifyInterval, Action<int, int> resimulateCallback, Action abortCallback)
    {
        currentP2PNetwork = p2pNetwork;
        inputProvider = provider;
        stateBuffer = states;
        localPlayerSlot = slot;
        rollbackWindow = window;
        syncVerifyInterval = verifyInterval;
        onResimulateRequired = resimulateCallback;
        onDesyncAborted = abortCallback;

        syncState = new NetworkSyncState();
        p1InputBuffer = new InputFlags[rollbackWindow];
        p2InputBuffer = new InputFlags[rollbackWindow];
        localHashBuffer = new Dictionary<int, ulong>();

        syncState.latestConfirmedTick = 0;
        syncState.currentPingMs = 0;
        syncState.lastHashedTick = -1;
        syncState.consecutiveDesyncCount = 0;
        syncState.isSoftStalling = false;
        syncState.isDesyncDetected = false;
    }

    public void ResetForNextRound()
    {
        syncState.latestConfirmedTick = 0;
        syncState.lastHashedTick = -1;
        syncState.consecutiveDesyncCount = 0;
        syncState.isDesyncDetected = false;
        syncState.isSoftStalling = false;
        
        localHashBuffer.Clear();
        System.Array.Clear(p1InputBuffer, 0, p1InputBuffer.Length);
        System.Array.Clear(p2InputBuffer, 0, p2InputBuffer.Length);
    }

    public RollbackNetworkSettings GetSettings()
    {
        return networkSettings;
    }

    /*
     * 온라인 시뮬레이션의 단일 틱 처리를 수행하고 틱 진행 가능 여부를 반환합니다.
     */
    public bool TryProcessNetworkTick(int currentTick, bool isFacingRight, bool isCameraFlipped, out PlayerInput p1Input, out PlayerInput p2Input)
    {
        p1Input = new PlayerInput();
        p2Input = new PlayerInput();

        bool isP1Local = localPlayerSlot == 0;

        VerifyRemoteInputsAndRollback(isP1Local, currentTick);
        BroadcastSyncHashes(currentTick);

        int currentRollback = currentTick - syncState.latestConfirmedTick;
        syncState.isSoftStalling = false;

        bool isHardStalling = currentRollback > networkSettings.maxRollbackFrames;
        if (isHardStalling)
        {
            ResendLastInput(isP1Local, currentTick);
            return false;
        }

        bool isTimeSyncRequired = ShouldApplyTimeSync(currentRollback, currentTick);
        if (isTimeSyncRequired)
        {
            syncState.isSoftStalling = true;
            ResendLastInput(isP1Local, currentTick);
            return false;
        }

        UpdateLocalInput(isP1Local, currentTick, isFacingRight, isCameraFlipped);
        PredictRemoteInput(isP1Local, currentTick);

        p1Input.flags = p1InputBuffer[currentTick % rollbackWindow];
        p2Input.flags = p2InputBuffer[currentTick % rollbackWindow];

        return true;
    }



    /*
     * 해시 버퍼를 순회하여 디싱크 여부를 검증합니다.
     */
    public void VerifySyncState()
    {
        syncState.currentPingMs = currentP2PNetwork.GetCurrentPingMs();

        List<int> verifiedTicks = new List<int>();

        foreach (var kvp in localHashBuffer)
        {
            bool hasRemoteHash = currentP2PNetwork.TryGetRemoteHash(kvp.Key, out ulong remoteHash);

            if (hasRemoteHash)
            {
                bool isHashMismatch = kvp.Value != remoteHash;

                if (isHashMismatch)
                {
                    syncState.consecutiveDesyncCount++;
                    GameStateSnapshot snapshot = stateBuffer[kvp.Key % rollbackWindow];
                    string roleLabel = localPlayerSlot == 0 ? "P1" : "P2";
                    HashTraceUtility.TraceAndDumpHash(roleLabel, snapshot);

                    bool isAbortThresholdReached = syncState.consecutiveDesyncCount >= networkSettings.desyncAbortThreshold;
                    if (isAbortThresholdReached)
                    {
                        TriggerDesyncError(kvp.Key, kvp.Value, remoteHash);
                        return;
                    }
                }
                else
                {
                    syncState.consecutiveDesyncCount = 0;
                    syncState.isDesyncDetected = false;
                }

                verifiedTicks.Add(kvp.Key);
            }
        }

        foreach (int t in verifiedTicks)
        {
            localHashBuffer.Remove(t);
        }
    }

    /*
     * 외부 시뮬레이션에서 사용할 수 있도록 전체 P1 인풋 버퍼를 반환합니다.
     */
    public InputFlags[] GetP1InputBuffer()
    {
        return p1InputBuffer;
    }

    /*
     * 외부 시뮬레이션에서 사용할 수 있도록 전체 P2 인풋 버퍼를 반환합니다.
     */
    public InputFlags[] GetP2InputBuffer()
    {
        return p2InputBuffer;
    }

    /*
     * 동기화 상태 데이터를 반환합니다.
     */
    public NetworkSyncState GetSyncState()
    {
        return syncState;
    }

    /*
     * 네트워크 수신 버퍼에서 실제 인풋을 꺼내와 예측과 다를 경우 콜백을 호출합니다.
     */
    private void VerifyRemoteInputsAndRollback(bool isP1Local, int currentTick)
    {
        int rollbackTick = -1;
        InputFlags lastConfirmedRemote = InputFlags.None;

        for (int t = syncState.latestConfirmedTick; t < currentTick; t++)
        {
            bool hasRemoteInput = currentP2PNetwork.TryGetRemoteInput(t, out ushort rawInput);
            if (hasRemoteInput)
            {
                InputFlags actualRemote = (InputFlags)rawInput;
                int idx = t % rollbackWindow;
                InputFlags predicted = isP1Local ? p2InputBuffer[idx] : p1InputBuffer[idx];

                bool isPredictionFailed = predicted != actualRemote;
                if (isPredictionFailed)
                {
                    if (isP1Local) p2InputBuffer[idx] = actualRemote;
                    else p1InputBuffer[idx] = actualRemote;
                    
                    bool isFirstFailure = rollbackTick == -1;
                    if (isFirstFailure) rollbackTick = t;
                }
                
                lastConfirmedRemote = actualRemote;
                syncState.latestConfirmedTick = t + 1;
            }
            else
            {
                break;
            }
        }

        bool isRollbackNeeded = rollbackTick != -1;
        if (isRollbackNeeded)
        {
            for (int t = syncState.latestConfirmedTick; t < currentTick; t++)
            {
                int idx = t % rollbackWindow;
                if (isP1Local) p2InputBuffer[idx] = lastConfirmedRemote;
                else p1InputBuffer[idx] = lastConfirmedRemote;
            }
            onResimulateRequired?.Invoke(rollbackTick, currentTick);
        }
    }

    /*
     * 로컬 물리 입력을 샘플링하여 지연 버퍼에 넣고 P2P 망으로 발송합니다.
     */
    private void UpdateLocalInput(bool isP1Local, int currentTick, bool isFacingRight, bool isCameraFlipped)
    {
        PlayerInput physicalInput = inputProvider.GetCurrentInput(currentTick, localPlayerSlot, isFacingRight, isCameraFlipped);
        int targetTick = currentTick + networkSettings.inputDelayFrames;
        int bufferIndex = targetTick % rollbackWindow;

        if (isP1Local) p1InputBuffer[bufferIndex] = physicalInput.flags;
        else p2InputBuffer[bufferIndex] = physicalInput.flags;

        currentP2PNetwork.SendLocalInput(targetTick, (ushort)physicalInput.flags);
    }

    /*
     * 상대방의 입력이 지연되었을 경우 최신 입력을 유지할 것으로 추론합니다.
     */
    private void PredictRemoteInput(bool isP1Local, int currentTick)
    {
        int idx = currentTick % rollbackWindow;
        bool hasCurrentRemoteInput = currentP2PNetwork.TryGetRemoteInput(currentTick, out ushort rawInput);
        
        if (hasCurrentRemoteInput)
        {
            InputFlags actualRemote = (InputFlags)rawInput;
            if (isP1Local) p2InputBuffer[idx] = actualRemote;
            else p1InputBuffer[idx] = actualRemote;
            
            bool isLatestTick = syncState.latestConfirmedTick == currentTick;
            if (isLatestTick) syncState.latestConfirmedTick = currentTick + 1;
        }
        else
        {
            bool isPastFirstTick = currentTick > 0;
            if (isPastFirstTick)
            {
                int prevIdx = (currentTick - 1) % rollbackWindow;
                if (isP1Local) p2InputBuffer[idx] = p2InputBuffer[prevIdx];
                else p1InputBuffer[idx] = p1InputBuffer[prevIdx];
            }
        }
    }

    /*
     * 통신 불량 시 가장 최근에 전송한 로컬 입력을 한 번 더 전송합니다.
     */
    private void ResendLastInput(bool isP1Local, int currentTick)
    {
        int lastTick = Mathf.Max(0, currentTick + networkSettings.inputDelayFrames - 1);
        int bufferIndex = lastTick % rollbackWindow;
        InputFlags lastInput = isP1Local ? p1InputBuffer[bufferIndex] : p2InputBuffer[bufferIndex];
        
        currentP2PNetwork.SendLocalInput(lastTick, (ushort)lastInput);
    }

    /*
     * 주기에 맞춰 게임 상태 버퍼의 해시를 생성하고 상대방에게 브로드캐스트합니다.
     */
    private void BroadcastSyncHashes(int currentTick)
    {
        int maxHashTick = Mathf.Min(syncState.latestConfirmedTick, currentTick);
        
        for (int t = syncState.lastHashedTick + 1; t < maxHashTick; t++)
        {
            bool isVerifyInterval = t % syncVerifyInterval == 0;
            if (isVerifyInterval)
            {
                ulong hash = StateHashUtility.ComputeHash(stateBuffer[t % rollbackWindow]);
                localHashBuffer[t] = hash;
                currentP2PNetwork.SendSyncHash(t, hash);
                syncState.lastHashedTick = t;
            }
        }
    }

    /*
     * 네트워크 핑을 바탕으로 한쪽 클라이언트의 연산을 일시 정지시킬지 판단합니다.
     */
    private bool ShouldApplyTimeSync(int currentRollback, int currentTick)
    {
        float oneWayPingMs = syncState.currentPingMs / 2f;
        int oneWayFrames = Mathf.RoundToInt(oneWayPingMs / (1000f / 60f));
        int expectedRollback = Mathf.Max(0, oneWayFrames - networkSettings.inputDelayFrames);
        int timeSyncThreshold = expectedRollback + 2;

        bool isOverThreshold = currentRollback > timeSyncThreshold;
        bool isSkipFrame = currentTick % 3 == 0;

        return isOverThreshold && isSkipFrame;
    }

    /*
     * 한계치 이상의 디싱크가 감지되면 즉시 게임 진행을 포기합니다.
     */
    private void TriggerDesyncError(int tick, ulong local, ulong remote)
    {
        bool isAlreadyDetected = syncState.isDesyncDetected;
        if (isAlreadyDetected) return;

        syncState.isDesyncDetected = true;
        onDesyncAborted?.Invoke();
    }
}