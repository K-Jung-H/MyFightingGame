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

public struct RemoteHashData
{
    public int tick;
    public ulong hash;

    public RemoteHashData(int tick, ulong hash)
    {
        this.tick = tick;
        this.hash = hash;
    }
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
    
    private Queue<RemoteHashData> remoteHashQueue;
    
    private int localPlayerSlot;
    private int rollbackWindow;
    private int syncVerifyInterval;
    
    private Action<int, int> onResimulateRequired;
    private Action onDesyncAborted;

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
        remoteHashQueue = new Queue<RemoteHashData>(); 

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
        remoteHashQueue.Clear(); 
        System.Array.Clear(p1InputBuffer, 0, p1InputBuffer.Length);
        System.Array.Clear(p2InputBuffer, 0, p2InputBuffer.Length);
    }

    public RollbackNetworkSettings GetSettings() { return networkSettings; }

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

    public void EnqueueRemoteHash(int tick, ulong hash)
    {
        remoteHashQueue.Enqueue(new RemoteHashData(tick, hash));
    }

    public void VerifySyncState()
    {
        syncState.currentPingMs = currentP2PNetwork.GetCurrentPingMs();

        while (remoteHashQueue.Count > 0)
        {
            RemoteHashData targetHash = remoteHashQueue.Peek();

            if (targetHash.tick >= syncState.latestConfirmedTick)
            {
                break;
            }

            remoteHashQueue.Dequeue();

            if (localHashBuffer.TryGetValue(targetHash.tick, out ulong localHash))
            {
                bool isHashMismatch = localHash != targetHash.hash;

                if (isHashMismatch)
                {
                    syncState.consecutiveDesyncCount++;
                    GameStateSnapshot snapshot = stateBuffer[targetHash.tick % rollbackWindow];
                    string roleLabel = localPlayerSlot == 0 ? "P1" : "P2";
                    HashTraceUtility.TraceAndDumpHash(roleLabel, snapshot);

                    if (syncState.consecutiveDesyncCount >= networkSettings.desyncAbortThreshold)
                    {
                        TriggerDesyncError(targetHash.tick, localHash, targetHash.hash);
                        return;
                    }
                }
                else
                {
                    syncState.consecutiveDesyncCount = 0;
                    syncState.isDesyncDetected = false;
                }

                localHashBuffer.Remove(targetHash.tick);
            }
        }
    }

    public InputFlags[] GetP1InputBuffer() { return p1InputBuffer; }
    public InputFlags[] GetP2InputBuffer() { return p2InputBuffer; }
    public NetworkSyncState GetSyncState() { return syncState; }

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

    private void UpdateLocalInput(bool isP1Local, int currentTick, bool isFacingRight, bool isCameraFlipped)
    {
        PlayerInput physicalInput = inputProvider.GetCurrentInput(currentTick, localPlayerSlot, isFacingRight, isCameraFlipped);
        int targetTick = currentTick + networkSettings.inputDelayFrames;
        int bufferIndex = targetTick % rollbackWindow;

        if (isP1Local) p1InputBuffer[bufferIndex] = physicalInput.flags;
        else p2InputBuffer[bufferIndex] = physicalInput.flags;

        currentP2PNetwork.SendLocalInput(targetTick, (ushort)physicalInput.flags);
    }

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

    private void ResendLastInput(bool isP1Local, int currentTick)
    {
        int lastTick = Mathf.Max(0, currentTick + networkSettings.inputDelayFrames - 1);
        int bufferIndex = lastTick % rollbackWindow;
        InputFlags lastInput = isP1Local ? p1InputBuffer[bufferIndex] : p2InputBuffer[bufferIndex];
        
        currentP2PNetwork.SendLocalInput(lastTick, (ushort)lastInput);
    }

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

        if (maxHashTick > syncState.lastHashedTick + 1)
        {
            syncState.lastHashedTick = maxHashTick - 1;
        }
    }

    private bool ShouldApplyTimeSync(int currentRollback, int currentTick)
    {
        float oneWayPingMs = syncState.currentPingMs / 2f;
        int oneWayFrames = Mathf.RoundToInt(oneWayPingMs / (1000f / 60f));
        int expectedRollback = Mathf.Max(0, oneWayFrames - networkSettings.inputDelayFrames);
        int timeSyncThreshold = expectedRollback + 2;

        return currentRollback > timeSyncThreshold && currentTick % 3 == 0;
    }

    private void TriggerDesyncError(int tick, ulong local, ulong remote)
    {
        if (syncState.isDesyncDetected) return;
        syncState.isDesyncDetected = true;
        onDesyncAborted?.Invoke();
    }
}