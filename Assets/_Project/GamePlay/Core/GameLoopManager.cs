using UnityEngine;

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

[System.Serializable]
public struct MatchScoreContext
{
    public int currentRound;
    public int maxRounds;
    public int requiredRoundWins;
    public int p1RoundWins;
    public int p2RoundWins;
}

[System.Serializable]
public struct GameEnvironmentSettings
{
    public float playerCollisionMinDistance;
    public float globalGravity;
    public Vector3 p1SpawnPos;
    public Vector3 p2SpawnPos;
    public float postMatchDelaySeconds;
}

[System.Serializable]
public struct UIBindings
{
    public CameraManager cameraManager;
    public HealthBarController p1HealthBar;
    public HealthBarController p2HealthBar;
    public SpriteNumberDisplay roundTimerDisplay;
    public WinCounterUIManager winCounterUI;
    public MatchResultUIManager matchResultUI;
}

public struct ConnectionFlowState
{
    public bool isWaitingForP2PConnection;
    public bool isWaitingForServerSync;
    public float syncTimeoutTimer;
    public float p2pConnectTimeoutTimer;
    public int localPlayerSlot;
    public P2PNetworkManager currentP2PNetwork;
}

public struct SimulationState
{
    public int currentTick;
    public bool isSimulationRunning;
    public bool isRoundOver;
    public bool isResimulating;
    public bool isCameraFlipped;
    public int postMatchDelayTicks;
    public FPVector3 sharedDepthAxis;
}

public struct NetworkSyncState
{
    public int latestConfirmedTick;
    public bool isSoftStalling;
    public bool isDesyncDetected;
    public int consecutiveDesyncCount;
    public int currentPingMs;
    public int lastHashedTick;
}

public class GameLoopManager : MonoBehaviour
{
    [SerializeField] private GameEnvironmentSettings envSettings = new GameEnvironmentSettings { playerCollisionMinDistance = 1.0f, globalGravity = 0.02f, p1SpawnPos = new Vector3(-2, 0, 0), p2SpawnPos = new Vector3(2, 0, 0), postMatchDelaySeconds = 3.0f };
    [SerializeField] private UIBindings uiBindings;
    [SerializeField] private PlayerSessionContext playerOne;
    [SerializeField] private PlayerSessionContext playerTwo;
    [SerializeField] private NetworkSyncController syncController = new NetworkSyncController();

    private const int ROLLBACK_WINDOW = 60;
    private const int SYNC_VERIFY_INTERVAL = 60;
    private const float SYNC_TIMEOUT_LIMIT = 10f;
    private const float P2P_CONNECT_TIMEOUT_LIMIT = 5f;

    private MatchScoreContext scoreContext;
    private ConnectionFlowState connectionState;
    private SimulationState simState;

    private GameStateSnapshot[] stateBuffer;
    private LocalInputProvider inputProvider;
    private GameSimulationCore simulationCore;
    private RoundTimerManager roundTimer;

    private void Awake()
    {
        if (MatchDataManager.P1CharacterData != null) playerOne.characterData = MatchDataManager.P1CharacterData;
        if (MatchDataManager.P2CharacterData != null) playerTwo.characterData = MatchDataManager.P2CharacterData;

        Time.fixedDeltaTime = 1f / 60f;
        Application.targetFrameRate = 120;

        simulationCore = new GameSimulationCore();
        simulationCore.Initialize(envSettings.playerCollisionMinDistance);

        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnGameStartReceived += HandleServerGameStart;
            ServerNetworkManager.Instance.OnMatchAbortedReceived += HandleMatchAborted;
            ServerNetworkManager.Instance.OnRoundVerifiedReceived += HandleRoundVerified;
            ServerNetworkManager.Instance.OnRematchSyncReceived += HandleRematchSync;
        }

        if (uiBindings.matchResultUI != null)
        {
            uiBindings.matchResultUI.OnActionRequested += HandleMatchEndAction;
        }
    }

    private void Start()
    {
        DetermineCameraFlipState();

        if (GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient)
        {
            simState.isSimulationRunning = false;
            connectionState.isWaitingForP2PConnection = true;
            connectionState.isWaitingForServerSync = true;
            connectionState.syncTimeoutTimer = 0f;
            connectionState.p2pConnectTimeoutTimer = 0f;
            
            string targetIp = RoomStateManager.Instance != null ? RoomStateManager.Instance.GetTargetPeerIpAddress() : "127.0.0.1";
            SetupP2PConnection(targetIp);
        }
        else
        {
            InitializeMatch(false);
        }
    }

    private void OnDestroy()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnGameStartReceived -= HandleServerGameStart;
            ServerNetworkManager.Instance.OnMatchAbortedReceived -= HandleMatchAborted;
            ServerNetworkManager.Instance.OnRoundVerifiedReceived -= HandleRoundVerified;
            ServerNetworkManager.Instance.OnRematchSyncReceived -= HandleRematchSync;
        }

        if (uiBindings.matchResultUI != null)
        {
            uiBindings.matchResultUI.OnActionRequested -= HandleMatchEndAction;
        }

        if (connectionState.currentP2PNetwork != null)
        {
            Destroy(connectionState.currentP2PNetwork.gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient)
        {
            if (connectionState.isWaitingForP2PConnection)
            {
                connectionState.p2pConnectTimeoutTimer += Time.fixedDeltaTime;
                if (connectionState.p2pConnectTimeoutTimer > P2P_CONNECT_TIMEOUT_LIMIT)
                {
                    HandleMatchAborted(GameSceneType.OnlineMatchedRoom);
                    return;
                }

                ProcessP2PHandshake();
                return;
            }

            if (connectionState.isWaitingForServerSync)
            {
                connectionState.syncTimeoutTimer += Time.fixedDeltaTime;
                if (connectionState.syncTimeoutTimer > SYNC_TIMEOUT_LIMIT)
                {
                    HandleMatchAborted(GameSceneType.OnlineMatchedRoom);
                    return;
                }

                if (connectionState.currentP2PNetwork != null) connectionState.currentP2PNetwork.PumpNetworkTick();
                return;
            }

            if (!simState.isSimulationRunning) return;

            if (connectionState.currentP2PNetwork != null)
            {
                connectionState.currentP2PNetwork.PumpNetworkTick();
                
                if (!connectionState.currentP2PNetwork.GetIsConnected()) return;

                syncController.VerifySyncState();

                bool isTickProcessed = syncController.TryProcessNetworkTick(simState.currentTick, simState.isCameraFlipped, out PlayerInput p1Input, out PlayerInput p2Input);
                
                if (isTickProcessed)
                {
                    ProcessTick(p1Input, p2Input);
                }
            }
        }
        else
        {
            if (!simState.isSimulationRunning) return;
            ProcessOfflineTick();
        }
    }

    private void OnGUI()
    {
        if (GameFlowManager.Instance.currentMode == ConnectionMode.Offline) return;

        NetworkSyncState syncState = syncController.GetSyncState();
        int rollbackFrames = Mathf.Max(0, simState.currentTick - syncState.latestConfirmedTick);
        
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(10, 180, 200, 20), $"Ping: {syncState.currentPingMs} ms");
        GUI.Label(new Rect(10, 200, 200, 20), $"Rollback: {rollbackFrames} F");
        
        if (rollbackFrames > syncController.GetSettings().maxRollbackFrames)
        {
            GUI.contentColor = Color.red;
            GUI.Label(new Rect(10, 220, 200, 20), "WAITING FOR NETWORK (HARD STALL)...");
        }
        else if (syncState.isSoftStalling)
        {
            GUI.contentColor = Color.yellow;
            GUI.Label(new Rect(10, 220, 200, 20), "SYNCING TIME (SOFT STALL)...");
        }
    }

    public int GetCurrentTick() => simState.currentTick;
    public RoundTimerManager GetRoundTimer() => roundTimer;
    public PlayerState_Type GetP1State() => playerOne.controller != null ? playerOne.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.controller != null ? playerOne.controller.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwo.controller != null ? playerTwo.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.controller != null ? playerTwo.controller.GetPosition() : Vector3.zero;
    public PlayerController GetPlayerOneController() => playerOne.controller;
    public PlayerController GetPlayerTwoController() => playerTwo.controller;

    public bool GetIsHardStalling()
    {
        NetworkSyncState syncState = syncController.GetSyncState();
        int rollbackFrames = Mathf.Max(0, simState.currentTick - syncState.latestConfirmedTick);
        return rollbackFrames > syncController.GetSettings().maxRollbackFrames;
    }

    public bool GetIsSoftStalling()
    {
        return syncController.GetSyncState().isSoftStalling;
    }

    public bool GetIsDesyncDetected()
    {
        return syncController.GetSyncState().isDesyncDetected;
    }

    private void SetupP2PConnection(string peerIp)
    {
        GameObject p2pObj = new GameObject("P2PNetworkManager");
        connectionState.currentP2PNetwork = p2pObj.AddComponent<P2PNetworkManager>();

        ushort port = 9001;
        connectionState.localPlayerSlot = RoomStateManager.Instance != null ? RoomStateManager.Instance.GetLocalPlayerSlot() : 0;

        if (connectionState.localPlayerSlot == 0)
        {
            connectionState.currentP2PNetwork.InitializeDriverAsHost(port);
        }
        else
        {
            connectionState.currentP2PNetwork.ConnectToPeer(peerIp, port);
        }
    }

    private void DetermineCameraFlipState()
    {
        if (RoomStateManager.Instance != null)
        {
            int slot = RoomStateManager.Instance.GetLocalPlayerSlot();
            RoomStateModel roomState = RoomStateManager.Instance.roomModel;
            
            int mySide = (slot == 0) ? roomState.p1PreferredSide : roomState.p2PreferredSide;
            simState.isCameraFlipped = (slot == 0 && mySide == 1) || (slot == 1 && mySide == 0);

            if (uiBindings.cameraManager != null)
            {
                uiBindings.cameraManager.SetCameraFlip(simState.isCameraFlipped);
            }
        }
    }

    private void HandleMatchAborted(GameSceneType targetScene)
    {
        simState.isSimulationRunning = false;
        simState.isRoundOver = true;
    }

    private void HandleServerGameStart()
    {
        if (connectionState.isWaitingForServerSync)
        {
            connectionState.isWaitingForServerSync = false;
            InitializeMatch(true);
            simState.isSimulationRunning = true;
        }
    }

    private void InitializeMatch(bool isNetworkReset)
    {
        int timeLimit = 99;
        int maxRds = 3;

        if (RoomStateManager.Instance != null)
        {
            RoomStateModel model = RoomStateManager.Instance.roomModel;
            timeLimit = model.roundTimeLimit;
            maxRds = model.maxRounds;
        }

        scoreContext.p1RoundWins = 0;
        scoreContext.p2RoundWins = 0;
        scoreContext.maxRounds = maxRds;
        scoreContext.requiredRoundWins = (maxRds / 2) + 1;
        scoreContext.currentRound = 1; 

        if (uiBindings.winCounterUI != null)
        {
            uiBindings.winCounterUI.InitializeCounters(scoreContext.requiredRoundWins);
            uiBindings.winCounterUI.UpdateCounters(scoreContext.p1RoundWins, scoreContext.p2RoundWins);
        }

        simState.isResimulating = false;
        simState.sharedDepthAxis = new FPVector3(new FP64(0), new FP64(0), FP64.FromFloat(1f));

        stateBuffer = new GameStateSnapshot[ROLLBACK_WINDOW];
        roundTimer = new RoundTimerManager();

        if (playerOne.instance != null) Destroy(playerOne.instance);
        if (playerTwo.instance != null) Destroy(playerTwo.instance);

        inputProvider = new LocalInputProvider(playerOne.GetBinding(true), playerTwo.GetBinding(false));

        syncController.Initialize(
            connectionState.currentP2PNetwork,
            inputProvider,
            stateBuffer,
            connectionState.localPlayerSlot,
            ROLLBACK_WINDOW,
            SYNC_VERIFY_INTERVAL,
            Resimulate,
            TriggerDesyncError
        );

        SetupPlayer(playerOne, envSettings.p1SpawnPos);
        SetupPlayer(playerTwo, envSettings.p2SpawnPos);

        if (playerOne.controller != null && playerTwo.controller != null)
        {
            playerOne.controller.SetTarget(playerTwo.controller);
            playerTwo.controller.SetTarget(playerOne.controller);

            if (uiBindings.p1HealthBar != null) uiBindings.p1HealthBar.Initialize(playerOne.controller.GetCombat(), false);
            if (uiBindings.p2HealthBar != null) uiBindings.p2HealthBar.Initialize(playerTwo.controller.GetCombat(), true);
        }

        if (uiBindings.cameraManager != null) uiBindings.cameraManager.SetTargetPlayers(playerOne.instance, playerTwo.instance);

        ResetForNextRound();
    }

    private void ProcessP2PHandshake()
    {
        if (connectionState.currentP2PNetwork != null)
        {
            connectionState.currentP2PNetwork.PumpNetworkTick();
            
            if (connectionState.currentP2PNetwork.GetIsConnected())
            {
                connectionState.isWaitingForP2PConnection = false;
                if (ServerNetworkManager.Instance != null)
                {
                    ServerNetworkManager.Instance.SendHandshake();
                }
            }
        }
    }

    private void TriggerDesyncError()
    {
        simState.isSimulationRunning = false; 
        GameFlowManager.Instance.ChangeScene(GameSceneType.OnlineMatchedRoom);
    }

    private void ProcessTick(PlayerInput p1, PlayerInput p2)
    {
        RunTick(p1, p2);
        SaveGameState(simState.currentTick);
        simState.currentTick++;
    }

    private void RunTick(PlayerInput p1, PlayerInput p2)
    {
        bool isDelayFinished = simState.isRoundOver && simState.postMatchDelayTicks <= 0;
        if (isDelayFinished) 
        { 
            p1.flags = InputFlags.None; 
            p2.flags = InputFlags.None; 
        }
        
        if (!simState.isRoundOver)
        {
            roundTimer.UpdateTick();
        }
        
        if (playerOne.controller != null && playerTwo.controller != null)
        {
            simulationCore.SimulateFrame(playerOne.controller, playerTwo.controller, p1, p2, ref simState.sharedDepthAxis, HandleHitSpark);
        }

        UpdateMatchState();

        if (!simState.isResimulating) SyncVisuals();
    }

    private void UpdateMatchState()
    {
        if (simState.isRoundOver)
        {
            ProcessPostMatchDelay();
            return;
        }

        CheckRoundEndCondition();
    }

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

        simState.isRoundOver = true;
    }

    private void ProcessPostMatchDelay()
    {
        if (simState.postMatchDelayTicks > 0)
        {
            simState.postMatchDelayTicks--;
            
            if (simState.postMatchDelayTicks == 0)
            {
                EvaluateRoundResult(out int winnerSlot);
                
                if (!simState.isResimulating)
                {
                    bool isOnline = GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient;
                    
                    if (isOnline)
                    {
                        ReportRoundEndToServer(winnerSlot);
                    }
                    else
                    {
                        PrepareNextRoundOrEndMatch();
                    }
                }
            }
        }
    }

    private void EvaluateRoundResult(out int winnerSlot)
    {
        winnerSlot = -1;
        if (playerOne.controller == null || playerTwo.controller == null) return;

        int p1Hp = playerOne.controller.GetCombat().GetCurrentHealth();
        int p2Hp = playerTwo.controller.GetCombat().GetCurrentHealth();
        int timeFrames = roundTimer.GetCurrentFrames();

        if (p1Hp <= 0 && p2Hp <= 0)
        {
            winnerSlot = -1; 
            SetDrawState();
        }
        else if (p1Hp <= 0)
        {
            winnerSlot = 1; 
            scoreContext.p2RoundWins++;
            SetWinLossState(playerTwo, playerOne);
        }
        else if (p2Hp <= 0)
        {
            winnerSlot = 0; 
            scoreContext.p1RoundWins++;
            SetWinLossState(playerOne, playerTwo);
        }
        else if (timeFrames <= 0)
        {
            if (p1Hp > p2Hp) 
            {
                winnerSlot = 0;
                scoreContext.p1RoundWins++;
                SetWinLossState(playerOne, playerTwo);
            }
            else if (p2Hp > p1Hp) 
            {
                winnerSlot = 1;
                scoreContext.p2RoundWins++;
                SetWinLossState(playerTwo, playerOne);
            }
            else 
            {
                winnerSlot = -1;
                SetDrawState();
            }
        }

        if (uiBindings.winCounterUI != null)
        {
            uiBindings.winCounterUI.UpdateCounters(scoreContext.p1RoundWins, scoreContext.p2RoundWins);
        }
    }

    private void ReportRoundEndToServer(int winnerSlot)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendRoundEndReport(winnerSlot, scoreContext.p1RoundWins, scoreContext.p2RoundWins);
        }
    }

    private void HandleRoundVerified()
    {
        PrepareNextRoundOrEndMatch();
    }

    private void PrepareNextRoundOrEndMatch()
    {
        bool isMatchOver = scoreContext.p1RoundWins >= scoreContext.requiredRoundWins || scoreContext.p2RoundWins >= scoreContext.requiredRoundWins;

        if (isMatchOver)
        {
            ProcessFinalMatchEnd();
        }
        else
        {
            ResetForNextRound();
        }
    }

    private void ResetForNextRound()
    {
        simState.currentTick = 0;
        simState.isRoundOver = false;
        simState.postMatchDelayTicks = Mathf.RoundToInt(envSettings.postMatchDelaySeconds * 60f);

        scoreContext.currentRound++;

        if (playerOne.controller != null && playerTwo.controller != null)
        {
            playerOne.controller.GetCombat().InitializeHealth();
            playerTwo.controller.GetCombat().InitializeHealth();

            playerOne.controller.GetPhysics().SetPosition(envSettings.p1SpawnPos);
            playerTwo.controller.GetPhysics().SetPosition(envSettings.p2SpawnPos);

            playerOne.controller.GetStateMachine().TransitionTo(PlayerState_Type.Idle, true);
            playerTwo.controller.GetStateMachine().TransitionTo(PlayerState_Type.Idle, true);

            playerOne.controller.GetActionController().ClearAllBuffers();
            playerTwo.controller.GetActionController().ClearAllBuffers();
            
            playerOne.controller.GetCombat().ClearRegisteredHitGroupIds();
            playerTwo.controller.GetCombat().ClearRegisteredHitGroupIds();
        }

        int timeLimit = RoomStateManager.Instance != null ? RoomStateManager.Instance.roomModel.roundTimeLimit : 99;
        roundTimer.InitializeTimer(timeLimit);

        if (connectionState.currentP2PNetwork != null)
        {
            connectionState.currentP2PNetwork.ClearBuffer();
        }

        System.Array.Clear(stateBuffer, 0, stateBuffer.Length);
        
        SaveGameState(0);

        simState.isSimulationRunning = true;
    }

    private void ProcessFinalMatchEnd()
    {
        if (uiBindings.matchResultUI != null)
        {
            uiBindings.matchResultUI.ShowResult(
                scoreContext.p1RoundWins, 
                scoreContext.p2RoundWins, 
                scoreContext.requiredRoundWins, 
                connectionState.localPlayerSlot
            );
        }
    }

    private void HandleMatchEndAction(MatchEndActionType actionType)
    {
        bool isOnline = GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient;

        if (isOnline)
        {
            if (ServerNetworkManager.Instance != null)
            {
                ServerNetworkManager.Instance.SendMatchEndAction(actionType);
            }
        }
        else
        {
            switch (actionType)
            {
                case MatchEndActionType.ReturnToMenu:
                    GameFlowManager.Instance.ChangeScene(GameSceneType.GameModeSelect);
                    break;
                case MatchEndActionType.ReturnToCharacterSelect:
                    GameFlowManager.Instance.ChangeScene(GameSceneType.CharacterSelect);
                    break;
                case MatchEndActionType.Rematch:
                    InitializeMatch(false);
                    simState.isSimulationRunning = true;
                    break;
            }
        }
    }

    private void HandleRematchSync(bool isP1Ready, bool isP2Ready)
    {
        if (uiBindings.matchResultUI != null)
        {
            uiBindings.matchResultUI.UpdateRematchSync(isP1Ready, isP2Ready);
        }
    }

    private void SetWinLossState(PlayerSessionContext winner, PlayerSessionContext loser)
    {
        winner.controller.GetStateMachine().TransitionTo(PlayerState_Type.Win, true);
        loser.controller.GetStateMachine().TransitionTo(PlayerState_Type.Defeat, true); 
    }

    private void SetDrawState()
    {
        playerOne.controller.GetStateMachine().TransitionTo(PlayerState_Type.Defeat, true);
        playerTwo.controller.GetStateMachine().TransitionTo(PlayerState_Type.Defeat, true);
    }

    private void SaveGameState(int tick)
    {
        int idx = tick % ROLLBACK_WINDOW;
        stateBuffer[idx].tick = tick;
        stateBuffer[idx].sharedDepthAxis = simState.sharedDepthAxis;
        stateBuffer[idx].isRoundOver = simState.isRoundOver;
        stateBuffer[idx].postMatchDelayTicks = simState.postMatchDelayTicks;
        
        roundTimer.ExportState(ref stateBuffer[idx]);
        
        if (playerOne.controller != null) playerOne.controller.ExportState(ref stateBuffer[idx].p1Snapshot);
        if (playerTwo.controller != null) playerTwo.controller.ExportState(ref stateBuffer[idx].p2Snapshot);
    }

    private void LoadGameState(int tick)
    {
        int idx = tick % ROLLBACK_WINDOW;
        simState.sharedDepthAxis = stateBuffer[idx].sharedDepthAxis;
        simState.isRoundOver = stateBuffer[idx].isRoundOver;
        simState.postMatchDelayTicks = stateBuffer[idx].postMatchDelayTicks;
        
        roundTimer.ImportState(stateBuffer[idx]);
        
        if (playerOne.controller != null) playerOne.controller.ImportState(stateBuffer[idx].p1Snapshot);
        if (playerTwo.controller != null) playerTwo.controller.ImportState(stateBuffer[idx].p2Snapshot);
    }

    private void Resimulate(int from, int to)
    {
        simState.isResimulating = true;
        LoadGameState(Mathf.Max(0, from - 1));
        
        InputFlags[] p1Buffer = syncController.GetP1InputBuffer();
        InputFlags[] p2Buffer = syncController.GetP2InputBuffer();

        for (int t = from; t < to; t++)
        {
            int idx = t % ROLLBACK_WINDOW;
            RunTick(new PlayerInput { flags = p1Buffer[idx] }, new PlayerInput { flags = p2Buffer[idx] });
            SaveGameState(t);
        }
        simState.isResimulating = false;
    }

    private void SetupPlayer(PlayerSessionContext context, Vector3 spawnPos)
    {
        if (context.characterData == null) return;

        context.instance = Instantiate(context.characterData.characterPrefab, spawnPos, Quaternion.identity);
        context.renderer = context.instance.GetComponent<PlayerRenderer>();
        context.controller = new PlayerController();
        context.controller.Initialize(spawnPos, context.characterData);
        context.controller.GetPhysics().SetGlobalGravity(envSettings.globalGravity);
        if (context.renderer != null) context.renderer.InitializeRenderer(context.controller, context.characterData.animationMap.stateMap, context.characterData.effectTable);
    }

    private void SyncVisuals()
    {
        if (playerOne.renderer != null) playerOne.renderer.UpdateRenderer();
        if (playerTwo.renderer != null) playerTwo.renderer.UpdateRenderer();

        if (roundTimer != null && uiBindings.roundTimerDisplay != null)
        {
            uiBindings.roundTimerDisplay.SetNumber(roundTimer.GetRemainingSeconds());
        }
    }

    private void HandleHitSpark(PlayerController target, Vector3 point, EffectType effect)
    {
        if (simState.isResimulating) return;
        PlayerSessionContext ctx = (target == playerOne.controller) ? playerOne : playerTwo;
        if (ctx.renderer != null) ctx.renderer.PlayHitSpark(point, effect);
    }

    private void ProcessOfflineTick()
    {
        bool isP1Right = uiBindings.cameraManager.IsPlayerOneOnRightSide();
        PlayerInput p1 = inputProvider.GetCurrentInput(simState.currentTick, 0, !isP1Right);
        PlayerInput p2 = inputProvider.GetCurrentInput(simState.currentTick, 1, isP1Right);
        ProcessTick(p1, p2);
    }
}