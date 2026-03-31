using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlayerSessionContext
{
    public CharacterDataSO characterData;
    public InputBinding customBinding;
    [HideInInspector] public GameObject instance;
    [HideInInspector] public PlayerRenderer renderer;
    [HideInInspector] public PlayerController controller;

    public InputBinding GetBinding(bool isPlayerOne)
    {
        bool hasCustomBinding = customBinding != null && customBinding.IsValid();
        if (hasCustomBinding) return customBinding;
        return isPlayerOne ? InputBinding.GetDefaultP1() : InputBinding.GetDefaultP2();
    }
}

public struct GameStateSnapshot
{
    public int tick;
    public PlayerSnapshot p1Snapshot;
    public PlayerSnapshot p2Snapshot;
    public FPVector3 sharedDepthAxis;
    public int currentTimerFrames;
    public bool isTimerPaused;
    public bool isRoundOver;
    public int postMatchDelayTicks;
}

public class GameLoopManager : MonoBehaviour
{
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private float playerCollisionMinDistance = 1.0f;
    [SerializeField] private float globalGravity = 0.02f;
    [SerializeField] private PlayerSessionContext playerOne;
    [SerializeField] private PlayerSessionContext playerTwo;
    [SerializeField] private Vector3 p1SpawnPos = new Vector3(-2, 0, 0);
    [SerializeField] private Vector3 p2SpawnPos = new Vector3(2, 0, 0);
    [SerializeField] private HealthBarController p1HealthBar;
    [SerializeField] private HealthBarController p2HealthBar;
    [SerializeField] private SpriteNumberDisplay roundTimerDisplay;
    
    [SerializeField] private int maxRollbackFrames = 7;
    [SerializeField] private int inputDelayFrames = 2;
    [SerializeField] private float postMatchDelaySeconds = 3.0f;
    [SerializeField] private int desyncAbortThreshold = 5;

    private const int ROLLBACK_WINDOW = 60;
    private const int SYNC_VERIFY_INTERVAL = 60;

    private GameStateSnapshot[] stateBuffer;
    private InputFlags[] p1InputBuffer;
    private InputFlags[] p2InputBuffer;
    private LocalInputProvider inputProvider;
    private GameSimulationCore simulationCore;
    private RoundTimerManager roundTimer;
    
    private int currentTick;
    private int latestConfirmedTick;
    private bool isSimulationRunning;
    private bool isRoundOver;
    private bool isResimulating;
    private bool isWaitingForServerGameStart;
    private bool isWaitingForP2PConnection;
    private bool isWaitingForServerSync;
    private bool isCameraFlipped;
    private bool isSoftStalling;
    private FPVector3 sharedDepthAxis;

    private Dictionary<int, ulong> localHashBuffer;
    private int lastHashedTick;
    private bool isDesyncDetected;
    private int postMatchDelayTicks;
    private int currentPingMs;
    private int localPlayerSlot;
    private int consecutiveDesyncCount;

    private P2PNetworkManager currentP2PNetwork;

    private void Awake()
    {
        if (MatchDataManager.P1CharacterData != null) playerOne.characterData = MatchDataManager.P1CharacterData;
        if (MatchDataManager.P2CharacterData != null) playerTwo.characterData = MatchDataManager.P2CharacterData;

        Time.fixedDeltaTime = 1f / 60f;
        Application.targetFrameRate = 120;

        simulationCore = new GameSimulationCore();
        simulationCore.Initialize(playerCollisionMinDistance);

        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnGameStartReceived += HandleServerGameStart;
            ServerNetworkManager.Instance.OnCountdownUpdateReceived += HandleServerSyncCountdown;
            ServerNetworkManager.Instance.OnMatchAbortedReceived += HandleMatchAborted;
        }
    }

    private void Start()
    {
        DetermineCameraFlipState();

        if (GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient)
        {
            isSimulationRunning = false;
            
            //string existingIp = RoomStateManager.Instance != null ? RoomStateManager.Instance.GetTargetPeerIpAddress() : null;
            //string existingIp = RoomStateManager.Instance != null ? RoomStateManager.Instance.GetTargetPeerIpAddress() : "127.0.0.1";
            string existingIp = "127.0.0.1";
            Debug.Log($"[GameLoopManager] Existing peer IP found: {existingIp}. Setting up P2P connection immediately.");

            if (!string.IsNullOrEmpty(existingIp))
            {
                isWaitingForServerGameStart = false;
                isWaitingForP2PConnection = true;
                isWaitingForServerSync = true;
                SetupP2PConnection(existingIp);
                Debug.Log("[GameLoopManager] P2P connection setup initiated with existing IP.");
            }
            else
            {
                isWaitingForServerGameStart = true;
                isWaitingForP2PConnection = false;
                isWaitingForServerSync = false;
            }
        }
        else
        {
            InitializeMatch(false);
            isSimulationRunning = true;
        }
    }

    private void OnDestroy()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnGameStartReceived -= HandleServerGameStart;
            ServerNetworkManager.Instance.OnCountdownUpdateReceived -= HandleServerSyncCountdown;
            ServerNetworkManager.Instance.OnMatchAbortedReceived -= HandleMatchAborted;
        }

        if (currentP2PNetwork != null)
        {
            Destroy(currentP2PNetwork.gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient)
        {
            if (isWaitingForServerGameStart) return;

            if (isWaitingForP2PConnection)
            {
                ProcessP2PHandshake();
                return;
            }

            if (isWaitingForServerSync)
            {
                if (currentP2PNetwork != null) currentP2PNetwork.PumpNetworkTick();
                return;
            }

            if (!isSimulationRunning) return;

            if (currentP2PNetwork != null)
            {
                currentP2PNetwork.PumpNetworkTick();
                
                if (!currentP2PNetwork.GetIsConnected()) return;

                currentPingMs = currentP2PNetwork.GetCurrentPingMs();
                ProcessOnlineTick();
                VerifySyncState();
            }
        }
        else
        {
            if (!isSimulationRunning) return;
            ProcessOfflineTick();
        }
    }

    private void OnGUI()
    {
        if (GameFlowManager.Instance.currentMode == ConnectionMode.Offline) return;

        Vector2 refRes = GameFlowManager.Instance.GetReferenceResolution();
        Vector3 scale = new Vector3(Screen.width / refRes.x, Screen.height / refRes.y, 1f);
        float minScale = Mathf.Min(scale.x, scale.y);
        scale = new Vector3(minScale, minScale, 1f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);

        int rollbackFrames = Mathf.Max(0, currentTick - latestConfirmedTick);
        
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(10, 180, 200, 20), $"Ping: {currentPingMs} ms");
        GUI.Label(new Rect(10, 200, 200, 20), $"Rollback: {rollbackFrames} F");
        
        if (GetIsHardStalling())
        {
            GUI.contentColor = Color.red;
            GUI.Label(new Rect(10, 220, 200, 20), "WAITING FOR NETWORK (HARD STALL)...");
        }
        else if (GetIsSoftStalling())
        {
            GUI.contentColor = Color.yellow;
            GUI.Label(new Rect(10, 220, 200, 20), "SYNCING TIME (SOFT STALL)...");
        }
    }

    public bool GetIsHardStalling() => (currentTick - latestConfirmedTick) > maxRollbackFrames;
    public bool GetIsSoftStalling() => isSoftStalling;
    public bool GetIsDesyncDetected() => isDesyncDetected;
    public int GetCurrentTick() => currentTick;
    public RoundTimerManager GetRoundTimer() => roundTimer;
    public PlayerState_Type GetP1State() => playerOne.controller != null ? playerOne.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.controller != null ? playerOne.controller.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwo.controller != null ? playerTwo.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.controller != null ? playerTwo.controller.GetPosition() : Vector3.zero;
    public PlayerController GetPlayerOneController() => playerOne.controller;
    public PlayerController GetPlayerTwoController() => playerTwo.controller;

    /*
     * 룸 매니저에서 가져온 로컬 슬롯 번호에 맞춰 P2P 호스트 생성 또는 접속을 실행합니다.
     */
    private void SetupP2PConnection(string peerIp)
    {
        GameObject p2pObj = new GameObject("P2PNetworkManager");
        currentP2PNetwork = p2pObj.AddComponent<P2PNetworkManager>();

        ushort port = 9001;
        localPlayerSlot = RoomStateManager.Instance != null ? RoomStateManager.Instance.GetLocalPlayerSlot() : 0;

        if (localPlayerSlot == 0)
        {
            Debug.Log("[GameLoop] Initializing P2P as HOST (Slot 0)");
            currentP2PNetwork.InitializeDriverAsHost(port);
        }
        else
        {
            Debug.Log($"[GameLoop] Initializing P2P as CLIENT (Slot 1) connecting to {peerIp}");
            currentP2PNetwork.ConnectToPeer(peerIp, port);
        }
    }

    /*
     * 플레이어의 진영 데이터를 확인하여 로컬 카메라의 좌우 반전 여부를 결정합니다.
     */
    private void DetermineCameraFlipState()
    {
        if (RoomStateManager.Instance != null)
        {
            int slot = RoomStateManager.Instance.GetLocalPlayerSlot();
            RoomStateModel roomState = RoomStateManager.Instance.roomModel;
            
            int mySide = (slot == 0) ? roomState.p1PreferredSide : roomState.p2PreferredSide;
            isCameraFlipped = (slot == 0 && mySide == 1) || (slot == 1 && mySide == 0);

            if (cameraManager != null)
            {
                cameraManager.SetCameraFlip(isCameraFlipped);
            }
        }
    }

    /*
     * 서버 연결 해제 명령 수신 시 시뮬레이션을 중단하고 상태를 갱신합니다.
     */
    private void HandleMatchAborted(GameSceneType targetScene)
    {
        isSimulationRunning = false;
        isRoundOver = true;
    }

    /*
     * 서버로부터 아이피를 늦게 수신했을 경우, 이를 감지하여 P2P 연결 페이즈로 넘어갑니다.
     */
    private void HandleServerGameStart(string peerIp)
    {
        if (isWaitingForServerGameStart)
        {
            isWaitingForServerGameStart = false;
            isWaitingForP2PConnection = true;
            isWaitingForServerSync = true;
            SetupP2PConnection(peerIp);
        }
    }

    /*
     * 서버로부터 시작 명령을 수신하면 모든 대기를 풀고 시뮬레이션을 개시합니다.
     */
    private void HandleServerSyncCountdown(bool isStarted)
    {
        if (isWaitingForServerSync && isStarted)
        {
            isWaitingForServerSync = false;
            InitializeMatch(true);
            isSimulationRunning = true;
            Debug.Log("[GameLoopManager] Server sync complete. Simulation started.");
        }
    }

    /*
     * 매치에 필요한 데이터 구조, 타이머, 캐릭터 컨트롤러를 동적 초기화합니다.
     */
    private void InitializeMatch(bool isNetworkReset)
    {
        currentTick = 0;
        isRoundOver = false;
        isDesyncDetected = false;
        isResimulating = false;
        isSoftStalling = false;
        lastHashedTick = -1;
        latestConfirmedTick = 0;
        currentPingMs = 0;
        consecutiveDesyncCount = 0;

        postMatchDelayTicks = Mathf.RoundToInt(postMatchDelaySeconds * 60f);

        stateBuffer = new GameStateSnapshot[ROLLBACK_WINDOW];
        p1InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        p2InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        localHashBuffer = new Dictionary<int, ulong>();
        sharedDepthAxis = new FPVector3(new FP64(0), new FP64(0), FP64.FromFloat(1f));

        roundTimer = new RoundTimerManager();
        roundTimer.InitializeTimer(99);

        if (isNetworkReset && currentP2PNetwork != null)
        {
            currentP2PNetwork.ClearBuffer();
        }

        if (playerOne.instance != null) Destroy(playerOne.instance);
        if (playerTwo.instance != null) Destroy(playerTwo.instance);

        inputProvider = new LocalInputProvider(playerOne.GetBinding(true), playerTwo.GetBinding(false));

        SetupPlayer(playerOne, p1SpawnPos);
        SetupPlayer(playerTwo, p2SpawnPos);

        if (playerOne.controller != null && playerTwo.controller != null)
        {
            playerOne.controller.SetTarget(playerTwo.controller);
            playerTwo.controller.SetTarget(playerOne.controller);

            if (p1HealthBar != null) p1HealthBar.Initialize(playerOne.controller.GetCombat(), false);
            if (p2HealthBar != null) p2HealthBar.Initialize(playerTwo.controller.GetCombat(), true);
        }

        if (cameraManager != null) cameraManager.SetTargetPlayers(playerOne.instance, playerTwo.instance);

        SaveGameState(0);
    }

    /*
     * P2P 연결이 확립될 때까지 대기하며, 성공 시 서버로 Handshake를 발송합니다.
     */
    private void ProcessP2PHandshake()
    {
        if (currentP2PNetwork != null)
        {
            currentP2PNetwork.PumpNetworkTick();
            
            if (currentP2PNetwork.GetIsConnected())
            {
                isWaitingForP2PConnection = false;
                if (ServerNetworkManager.Instance != null)
                {
                    Debug.Log($"[CL-NET] P2P Connected! Sending Handshake to Server. Slot: {localPlayerSlot}");
                    ServerNetworkManager.Instance.SendHandshake();
                }
                Debug.Log("[GameLoopManager] P2P Connected. Handshake sent to server. Waiting for Sync...");
            }
        }
    }

    /*
     * 온라인 롤백 넷코드 시뮬레이션의 단일 틱 처리를 수행합니다.
     */
    private void ProcessOnlineTick()
    {
        bool isP1Local = (localPlayerSlot == 0);

        VerifyRemoteInputsAndRollback(isP1Local);
        BroadcastSyncHashes();

        int currentRollback = currentTick - latestConfirmedTick;
        isSoftStalling = false;

        if (currentRollback > maxRollbackFrames)
        {
            ResendLastInput(isP1Local);
            return;
        }

        bool isTimeSyncRequired = ShouldApplyTimeSync(currentRollback);
        if (isTimeSyncRequired)
        {
            isSoftStalling = true;
            ResendLastInput(isP1Local);
            return;
        }

        UpdateLocalInput(isP1Local, localPlayerSlot);
        PredictRemoteInput(isP1Local);

        ProcessTick(new PlayerInput { flags = p1InputBuffer[currentTick % ROLLBACK_WINDOW] }, 
                    new PlayerInput { flags = p2InputBuffer[currentTick % ROLLBACK_WINDOW] });
    }

    /*
     * 핑 기반으로 양측 클라이언트의 진행 속도 오차를 조절하기 위해 대기 여부를 판정합니다.
     */
    private bool ShouldApplyTimeSync(int currentRollback)
    {
        float oneWayPingMs = currentPingMs / 2f;
        int oneWayFrames = Mathf.RoundToInt(oneWayPingMs / (1000f / 60f));
        int expectedRollback = Mathf.Max(0, oneWayFrames - inputDelayFrames);
        int timeSyncThreshold = expectedRollback + 2;

        bool isOverThreshold = currentRollback > timeSyncThreshold;
        bool isSkipFrame = currentTick % 3 == 0;

        if (isOverThreshold && isSkipFrame)
        {
            return true;
        }

        return false;
    }

    /*
     * 상대방의 실제 인풋을 버퍼에서 꺼내어 예측 데이터와 다르면 과거로 돌아가 재시뮬레이션합니다.
     */
    private void VerifyRemoteInputsAndRollback(bool isP1Local)
    {
        int rollbackTick = -1;
        InputFlags lastConfirmedRemote = InputFlags.None;

        for (int t = latestConfirmedTick; t < currentTick; t++)
        {
            if (currentP2PNetwork.TryGetRemoteInput(t, out ushort rawInput))
            {
                InputFlags actualRemote = (InputFlags)rawInput;
                int idx = t % ROLLBACK_WINDOW;
                InputFlags predicted = isP1Local ? p2InputBuffer[idx] : p1InputBuffer[idx];

                if (predicted != actualRemote)
                {
                    if (isP1Local) p2InputBuffer[idx] = actualRemote;
                    else p1InputBuffer[idx] = actualRemote;
                    if (rollbackTick == -1) rollbackTick = t;
                }
                lastConfirmedRemote = actualRemote;
                latestConfirmedTick = t + 1;
            }
            else break;
        }

        if (rollbackTick != -1)
        {
            for (int t = latestConfirmedTick; t < currentTick; t++)
            {
                int idx = t % ROLLBACK_WINDOW;
                if (isP1Local) p2InputBuffer[idx] = lastConfirmedRemote;
                else p1InputBuffer[idx] = lastConfirmedRemote;
            }
            Resimulate(rollbackTick, currentTick);
        }
    }

    /*
     * 로컬 물리 입력을 샘플링하여 지연 버퍼에 넣고 P2P 망으로 발송합니다.
     */
    private void UpdateLocalInput(bool isP1Local, int localIdx)
    {
        PlayerInput physicalInput = inputProvider.GetCurrentInput(currentTick, localIdx, isCameraFlipped);
        int targetTick = currentTick + inputDelayFrames;
        
        if (isP1Local) p1InputBuffer[targetTick % ROLLBACK_WINDOW] = physicalInput.flags;
        else p2InputBuffer[targetTick % ROLLBACK_WINDOW] = physicalInput.flags;
        
        currentP2PNetwork.SendLocalInput(targetTick, (ushort)physicalInput.flags);
    }

    /*
     * 아직 수신되지 않은 미래 틱에 대해 상대방이 이전과 동일한 키를 누르고 있을 것이라 추론합니다.
     */
    private void PredictRemoteInput(bool isP1Local)
    {
        int idx = currentTick % ROLLBACK_WINDOW;
        if (currentP2PNetwork.TryGetRemoteInput(currentTick, out ushort rawInput))
        {
            InputFlags actualRemote = (InputFlags)rawInput;
            if (isP1Local) p2InputBuffer[idx] = actualRemote;
            else p1InputBuffer[idx] = actualRemote;
            if (latestConfirmedTick == currentTick) latestConfirmedTick = currentTick + 1;
        }
        else if (currentTick > 0)
        {
            int prevIdx = (currentTick - 1) % ROLLBACK_WINDOW;
            if (isP1Local) p2InputBuffer[idx] = p2InputBuffer[prevIdx];
            else p1InputBuffer[idx] = p1InputBuffer[prevIdx];
        }
    }

    /*
     * 연결이 불안정하여 하드 스톨이 걸렸을 때 로컬 입력만 강제로 재전송합니다.
     */
    private void ResendLastInput(bool isP1Local)
    {
        int lastTick = Mathf.Max(0, currentTick + inputDelayFrames - 1);
        InputFlags lastInput = isP1Local ? p1InputBuffer[lastTick % ROLLBACK_WINDOW] : p2InputBuffer[lastTick % ROLLBACK_WINDOW];
        currentP2PNetwork.SendLocalInput(lastTick, (ushort)lastInput);
    }

    /*
     * 주기적으로 로컬 시뮬레이션 해시값을 생성하여 상대방에게 전송합니다.
     */
    private void BroadcastSyncHashes()
    {
        int maxHashTick = Mathf.Min(latestConfirmedTick, currentTick);
        for (int t = lastHashedTick + 1; t < maxHashTick; t++)
        {
            if (t % SYNC_VERIFY_INTERVAL == 0)
            {
                ulong hash = StateHashUtility.ComputeHash(stateBuffer[t % ROLLBACK_WINDOW]);
                localHashBuffer[t] = hash;
                currentP2PNetwork.SendSyncHash(t, hash);
                lastHashedTick = t;
            }
        }
    }

    /*
     * 상대의 해시와 로컬 해시를 비교하여 디싱크 발생 시 덤프 로그를 생성하고 유예 카운트를 셉니다.
     */
    private void VerifySyncState()
    {
        List<int> verifiedTicks = new List<int>();
        
        foreach (var kvp in localHashBuffer)
        {
            bool hasRemoteHash = currentP2PNetwork.TryGetRemoteHash(kvp.Key, out ulong remoteHash);
            
            if (hasRemoteHash)
            {
                bool isHashMismatch = kvp.Value != remoteHash;
                
                if (isHashMismatch)
                {
                    consecutiveDesyncCount++;
                    GameStateSnapshot snapshot = stateBuffer[kvp.Key % ROLLBACK_WINDOW];
                    string roleLabel = (localPlayerSlot == 0) ? "P1" : "P2";
                    HashTraceUtility.TraceAndDumpHash(roleLabel, snapshot);
                    
                    if (consecutiveDesyncCount >= desyncAbortThreshold)
                    {
                        TriggerDesyncError(kvp.Key, kvp.Value, remoteHash);
                        return; 
                    }
                }
                else
                {
                    consecutiveDesyncCount = 0;
                    isDesyncDetected = false;
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
     * 유예 기간을 초과한 진짜 디싱크가 감지되면 시뮬레이션을 중지하고 로비로 탈출합니다.
     */
    private void TriggerDesyncError(int tick, ulong local, ulong remote)
    {
        if (isDesyncDetected) return;

        isDesyncDetected = true;
        isSimulationRunning = false; 
        
        Debug.LogError($"[FATAL DESYNC] Tick: {tick} | Local: {local} | Remote: {remote}. Aborting Match.");

        if (ServerNetworkManager.Instance != null)
        {
            
        }
        GameFlowManager.Instance.ChangeScene(GameSceneType.OnlineMatchedRoom);
    }

    /*
     * 입력 데이터를 버퍼에 저장하고 물리 프레임을 전진시킵니다.
     */
    private void ProcessTick(PlayerInput p1, PlayerInput p2)
    {
        int idx = currentTick % ROLLBACK_WINDOW;
        p1InputBuffer[idx] = p1.flags;
        p2InputBuffer[idx] = p2.flags;
        RunTick(p1, p2);
        SaveGameState(currentTick);
        currentTick++;
    }

    /*
     * 양 플레이어의 상태 업데이트, 충돌, 타이머 계산 등 실제 인게임 규칙을 처리합니다.
     */
    private void RunTick(PlayerInput p1, PlayerInput p2)
    {
        bool isDelayFinished = isRoundOver && postMatchDelayTicks <= 0;
        if (isDelayFinished) 
        { 
            p1.flags = InputFlags.None; 
            p2.flags = InputFlags.None; 
        }
        
        if (!isRoundOver)
        {
            roundTimer.UpdateTick();
        }
        
        if (playerOne.controller != null && playerTwo.controller != null)
        {
            simulationCore.SimulateFrame(playerOne.controller, playerTwo.controller, p1, p2, ref sharedDepthAxis, HandleHitSpark);
        }

        UpdateMatchState();

        if (!isResimulating) SyncVisuals();
    }

    /*
     * 라운드 종료 여부를 확인하고, 종료되었다면 결과 연산 대기 시간을 계산합니다.
     */
    private void UpdateMatchState()
    {
        if (isRoundOver)
        {
            ProcessPostMatchDelay();
            return;
        }

        CheckRoundEndCondition();
    }

    /*
     * 체력과 타이머를 검사하여 게임 승패가 갈릴 조건인지 판단합니다.
     */
    private void CheckRoundEndCondition()
    {
        if (playerOne.controller == null || playerTwo.controller == null) return;

        int p1Hp = playerOne.controller.GetCombat().GetCurrentHealth();
        int p2Hp = playerTwo.controller.GetCombat().GetCurrentHealth();
        int timeFrames = roundTimer.GetCurrentFrames();

        if (p1Hp > 0 && p2Hp > 0 && timeFrames > 0)
        {
            return;
        }

        isRoundOver = true;
    }

    /*
     * KO 직후의 여운 시간을 기다린 뒤 승패 애니메이션 트리거를 발동시킵니다.
     */
    private void ProcessPostMatchDelay()
    {
        if (postMatchDelayTicks > 0)
        {
            postMatchDelayTicks--;
            
            if (postMatchDelayTicks == 0)
            {
                ApplyFinalMatchResult();
                
                if (!isResimulating)
                {
                    ShowSceneTransitionUI();
                }
            }
        }
    }

    /*
     * 남은 체력 판정에 따라 각 컨트롤러의 승리 또는 패배 상태를 강제 주입합니다.
     */
    private void ApplyFinalMatchResult()
    {
        if (playerOne.controller == null || playerTwo.controller == null) return;

        int p1Hp = playerOne.controller.GetCombat().GetCurrentHealth();
        int p2Hp = playerTwo.controller.GetCombat().GetCurrentHealth();
        int timeFrames = roundTimer.GetCurrentFrames();

        if (p1Hp <= 0 && p2Hp <= 0)
        {
            SetDrawState();
        }
        else if (p1Hp <= 0)
        {
            SetWinLossState(playerTwo, playerOne);
        }
        else if (p2Hp <= 0)
        {
            SetWinLossState(playerOne, playerTwo);
        }
        else if (timeFrames <= 0)
        {
            if (p1Hp > p2Hp) SetWinLossState(playerOne, playerTwo);
            else if (p2Hp > p1Hp) SetWinLossState(playerTwo, playerOne);
            else SetDrawState();
        }
    }

    /*
     * 승자와 패자의 애니메이션 상태를 갱신합니다.
     */
    private void SetWinLossState(PlayerSessionContext winner, PlayerSessionContext loser)
    {
        winner.controller.GetStateMachine().TransitionTo(PlayerState_Type.Win, true);
        loser.controller.GetStateMachine().TransitionTo(PlayerState_Type.Defeat, true); 
    }

    /*
     * 무승부 시 양측 모두 패배(쓰러짐) 모션으로 처리합니다.
     */
    private void SetDrawState()
    {
        playerOne.controller.GetStateMachine().TransitionTo(PlayerState_Type.Defeat, true);
        playerTwo.controller.GetStateMachine().TransitionTo(PlayerState_Type.Defeat, true);
    }

    /*
     * 경기가 끝난 후 로비로 돌아가기 위한 UI를 표출합니다.
     */
    private void ShowSceneTransitionUI()
    {
        Debug.Log("Show Scene Transition UI");
    }

    /*
     * 롤백 대응을 위해 현재 틱의 게임 시뮬레이션 상태를 버퍼에 복사하여 보존합니다.
     */
    private void SaveGameState(int tick)
    {
        int idx = tick % ROLLBACK_WINDOW;
        stateBuffer[idx].tick = tick;
        stateBuffer[idx].sharedDepthAxis = sharedDepthAxis;
        stateBuffer[idx].isRoundOver = isRoundOver;
        stateBuffer[idx].postMatchDelayTicks = postMatchDelayTicks;
        
        roundTimer.ExportState(ref stateBuffer[idx]);
        
        if (playerOne.controller != null) playerOne.controller.ExportState(ref stateBuffer[idx].p1Snapshot);
        if (playerTwo.controller != null) playerTwo.controller.ExportState(ref stateBuffer[idx].p2Snapshot);
    }

    /*
     * 과거 시점의 데이터를 버퍼에서 읽어와 현재 게임 메모리에 덮어씌웁니다.
     */
    private void LoadGameState(int tick)
    {
        int idx = tick % ROLLBACK_WINDOW;
        sharedDepthAxis = stateBuffer[idx].sharedDepthAxis;
        isRoundOver = stateBuffer[idx].isRoundOver;
        postMatchDelayTicks = stateBuffer[idx].postMatchDelayTicks;
        
        roundTimer.ImportState(stateBuffer[idx]);
        
        if (playerOne.controller != null) playerOne.controller.ImportState(stateBuffer[idx].p1Snapshot);
        if (playerTwo.controller != null) playerTwo.controller.ImportState(stateBuffer[idx].p2Snapshot);
    }

    /*
     * 잘못된 예측 프레임부터 현재 프레임까지 고속으로 루프를 돌려 상태를 교정합니다.
     */
    private void Resimulate(int from, int to)
    {
        isResimulating = true;
        LoadGameState(Mathf.Max(0, from - 1));
        for (int t = from; t < to; t++)
        {
            int idx = t % ROLLBACK_WINDOW;
            RunTick(new PlayerInput { flags = p1InputBuffer[idx] }, new PlayerInput { flags = p2InputBuffer[idx] });
            SaveGameState(t);
        }
        isResimulating = false;
    }

    /*
     * 캐릭터 프리팹을 씬에 생성하고 논리 컨트롤러 및 렌더러를 연결합니다.
     */
    private void SetupPlayer(PlayerSessionContext context, Vector3 spawnPos)
    {
        if (context.characterData == null) return;

        context.instance = Instantiate(context.characterData.characterPrefab, spawnPos, Quaternion.identity);
        context.renderer = context.instance.GetComponent<PlayerRenderer>();
        context.controller = new PlayerController();
        context.controller.Initialize(spawnPos, context.characterData);
        context.controller.GetPhysics().SetGlobalGravity(globalGravity);
        if (context.renderer != null) context.renderer.InitializeRenderer(context.controller, context.characterData.animationMap.stateMap, context.characterData.effectTable);
    }

    /*
     * 연산된 논리적 위치와 상태를 실제 3D 모델(유니티 트랜스폼/애니메이터)에 반영합니다.
     */
    private void SyncVisuals()
    {
        if (playerOne.renderer != null) playerOne.renderer.UpdateRenderer();
        if (playerTwo.renderer != null) playerTwo.renderer.UpdateRenderer();

        if (roundTimer != null && roundTimerDisplay != null)
        {
            roundTimerDisplay.SetNumber(roundTimer.GetRemainingSeconds());
        }
    }

    /*
     * 타격이 성공했을 때 화면에 스파크 이펙트를 생성합니다.
     */
    private void HandleHitSpark(PlayerController target, Vector3 point, EffectType effect)
    {
        if (isResimulating) return;
        PlayerSessionContext ctx = (target == playerOne.controller) ? playerOne : playerTwo;
        if (ctx.renderer != null) ctx.renderer.PlayHitSpark(point, effect);
    }

    /*
     * 오프라인 모드일 때 로컬 기기의 두 입력 장치를 샘플링하여 틱을 회전시킵니다.
     */
    private void ProcessOfflineTick()
    {
        bool isP1Right = cameraManager.IsPlayerOneOnRightSide();
        PlayerInput p1 = inputProvider.GetCurrentInput(currentTick, 0, !isP1Right);
        PlayerInput p2 = inputProvider.GetCurrentInput(currentTick, 1, isP1Right);
        ProcessTick(p1, p2);
    }
}