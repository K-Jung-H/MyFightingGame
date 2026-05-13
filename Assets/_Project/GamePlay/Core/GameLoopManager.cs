using System.Data;
using UnityEngine;

public enum RoundPhase
{
    PreRound,
    Fighting,
    PostRound
}

[System.Serializable]
public class PlayerSessionContext
{
    public CharacterDataSO characterData;
    [HideInInspector] public InputBinding customBinding;
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

[System.Serializable]
public struct MatchScoreContext
{
    public int currentRound;
    public int maxRounds;
    public int requiredRoundWins;
    public int p1RoundWins;
    public int p2RoundWins;
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

public struct NetworkSyncState
{
    public int latestConfirmedTick;
    public bool isSoftStalling;
    public bool isDesyncDetected;
    public int consecutiveDesyncCount;
    public int currentPingMs;
    public int lastHashedTick;
}

public unsafe struct SimulationState
{
    public int currentTick;
    public bool isSimulationRunning;
    public bool isResimulating;
    public bool isCameraFlipped;
    public RoundPhase currentPhase;
    public int phaseDelayTicks;
    public FPVector3 sharedDepthAxis;
    public FP64 simulationScale;
    public FP64 timeAccumulator;
    public bool isLogicStep;
    public uint stageActiveWallBitmask;
    public fixed int wallDurabilities[32];
}

public unsafe struct GameStateSnapshot
{
    public int tick;
    public PlayerSnapshot p1Snapshot;
    public PlayerSnapshot p2Snapshot;
    public FPVector3 sharedDepthAxis;
    public int currentTimerFrames;
    public bool isTimerPaused;
    public RoundPhase currentPhase;
    public int phaseDelayTicks;
    public FP64 simulationScale;
    public FP64 timeAccumulator;
    public MatchScoreContext scoreContext;
    public uint stageActiveWallBitmask;
    public fixed int wallDurabilities[32];
}


public class GameLoopManager : MonoBehaviour
{
    [Header("Core Dependencies")]
    [SerializeField] private PlayingUI_Manager playingUI;
    [SerializeField] private PlayerSessionContext playerOne;
    [SerializeField] private PlayerSessionContext playerTwo;

    [Header("Stage & Rule Data")]
    [SerializeField] private GameRuleConfigSO ruleConfig;
    [SerializeField] private GameStageDataSO currentStageData;
    private StageWallAnimationController visualWallController;
    private GameObject spawnedStageVisual;

    private CameraManager cameraManager;

    [Header("Public States (For Logic Classes)")]
    public NetworkSyncController syncController = new NetworkSyncController();
    public MatchScoreContext scoreContext;
    public ConnectionFlowState connectionState;
    public SimulationState simState;
    public LocalInputProvider inputProvider;
    
    public const int ROLLBACK_WINDOW = 60;
    public const int SYNC_VERIFY_INTERVAL = 60;
    public const float SYNC_TIMEOUT_LIMIT = 10f;
    public const float P2P_CONNECT_TIMEOUT_LIMIT = 5f;

    private FP64 climaxRecoveryStepFP;
    private GameStateSnapshot[] stateBuffer;
    private GameSimulationCore simulationCore;
    private RoundTimerManager roundTimer;
    private RoundReferee roundReferee;
    private IGameModeLogic currentLogic;

    public bool isShowAllHUD = false;

    private FP64 cachedClimaxSlowMoScale;
    private readonly FP64 FP64_ONE = new FP64(65536);

    private void Awake()
    {
        if (MatchDataManager.P1CharacterData != null) playerOne.characterData = MatchDataManager.P1CharacterData;
        if (MatchDataManager.P2CharacterData != null) playerTwo.characterData = MatchDataManager.P2CharacterData;

        Time.fixedDeltaTime = 1f / 60f;
        Application.targetFrameRate = 120;

        roundReferee = new RoundReferee();
        roundReferee.Initialize(ruleConfig);

        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnGameStartReceived += HandleServerGameStart;
            ServerNetworkManager.Instance.OnMatchAbortedReceived += HandleMatchAborted; 
            ServerNetworkManager.Instance.OnRoundVerifiedReceived += HandleRoundVerified;
            ServerNetworkManager.Instance.OnRematchSyncReceived += HandleRematchSync;
        }

        if (playingUI != null)
        {
            playingUI.InitializeUI();
            playingUI.BindMatchResultAction(HandleMatchEndAction);
        }
    }

    private void Start()
    {
        BattleType currentBattle = GameFlowManager.Instance.currentBattleType;
        
        if (currentBattle == BattleType.Training) currentLogic = new TrainingModeLogic();
        else if (currentBattle == BattleType.OnlineBattle) currentLogic = new OnlineModeLogic();
        else currentLogic = new OfflineModeLogic();

        currentLogic.Initialize(this);
        currentLogic.StartGame();
    }

    private void FixedUpdate()
    {
        if (currentLogic != null)
        {
            currentLogic.ProcessFixedUpdate();
        }
    }

    private void OnGUI()
    {
        isShowAllHUD = GUI.Toggle(new Rect(10f, 250f, 150f, 20f), isShowAllHUD, " Toggle All Debug HUD");

        if (isShowAllHUD && currentLogic != null)
        {
            currentLogic.OnGUI();
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

        if (playingUI != null)
        {
            playingUI.UnbindMatchResultAction(HandleMatchEndAction);
        }

        if (connectionState.currentP2PNetwork != null)
        {
            Destroy(connectionState.currentP2PNetwork.gameObject);
        }

        if (simulationCore != null)
        {
            simulationCore.HandleWallBreak -= OnWallBroken;
        }
    }

    public int GetCurrentTick() => simState.currentTick;
    public RoundTimerManager GetRoundTimer() => roundTimer;
    public StageBoundary GetStageBoundary()
    {
        if (simulationCore == null) return new StageBoundary();
        
        return simulationCore.GetRuntimeBoundary(simState.stageActiveWallBitmask);
    }
    
    public PlayerController GetPlayerOneController() => playerOne.controller;
    public PlayerController GetPlayerTwoController() => playerTwo.controller;

    public PlayerState_Type GetP1State() => playerOne.controller != null ? playerOne.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.controller != null ? playerOne.controller.GetPosition() : Vector3.zero;
    
    public PlayerState_Type GetP2State() => playerTwo.controller != null ? playerTwo.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.controller != null ? playerTwo.controller.GetPosition() : Vector3.zero;

    public bool GetIsP1VisuallyOnLeft()
    {
        if (playerOne.controller == null || playerTwo.controller == null) return true;

        FPVector3 p1Pos = playerOne.controller.GetFPPosition(); 
        FPVector3 p2Pos = playerTwo.controller.GetFPPosition();
        FPVector3 diff = p2Pos - p1Pos;
        
        FPVector3 upVector = new FPVector3(new FP64(0), FP64_ONE, new FP64(0));
        FPVector3 cameraRight = FPVector3.Cross(upVector, simState.sharedDepthAxis);
        FP64 dotProduct = FPVector3.Dot(diff, cameraRight);
        
        bool isLeft = dotProduct.rawValue > 0;
        
        return simState.isCameraFlipped ? !isLeft : isLeft;
    }

    private void OnWallBroken(int wallIndex, FPVector3 normal, float explosionForce)
    {
        if (visualWallController == null) return;

        visualWallController.SetWallVisualActive(wallIndex, false);

        if (!simState.isResimulating)
        {
            visualWallController.ActivateDebrisWithForce(wallIndex, normal.ToVector3(), explosionForce);
        }
    }

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

    public void InitializeMatch()
    {
        stateBuffer = new GameStateSnapshot[ROLLBACK_WINDOW];

        DetermineCameraFlipState();

        cachedClimaxSlowMoScale = ruleConfig.FP_ClimaxSlowMoScale;

        int timeLimit = RoomStateManager.Instance != null ? RoomStateManager.Instance.roomModel.roundTimeLimit : 99;
        int maxRds = RoomStateManager.Instance != null ? RoomStateManager.Instance.roomModel.maxRounds : 3;

        scoreContext.p1RoundWins = 0; 
        scoreContext.p2RoundWins = 0;
        scoreContext.maxRounds = maxRds; 
        scoreContext.requiredRoundWins = (maxRds / 2) + 1;
        scoreContext.currentRound = 1; 

        roundTimer = new RoundTimerManager();

        if (playingUI != null)
        {
            playingUI.SetupWinCounter(scoreContext.requiredRoundWins);
        }

        StageBoundary initialBoundary = currentStageData != null ? currentStageData.boundary : new StageBoundary();

        if (spawnedStageVisual != null) 
        {
            Destroy(spawnedStageVisual);
        }

        if (currentStageData != null && currentStageData.visualPrefab != null)
        {
            spawnedStageVisual = Instantiate(currentStageData.visualPrefab, Vector3.zero, Quaternion.identity);
            
            visualWallController = spawnedStageVisual.GetComponent<StageWallAnimationController>();
            if (visualWallController != null)
            {
                visualWallController.PreWarmDebris();
            }
        }

        cameraManager = Camera.main != null ? Camera.main.GetComponent<CameraManager>() : null;
        if (cameraManager != null && currentStageData != null)
        {
            cameraManager.InitializeBounds(currentStageData.cameraBoundsList);
        }

        simulationCore = new GameSimulationCore();
        simulationCore.Initialize(initialBoundary, ruleConfig);
        simulationCore.HandleWallBreak += OnWallBroken;

        simState.stageActiveWallBitmask = CreateInitialWallBitmask(initialBoundary);
        
        unsafe
        {
            for (int i = 0; i < initialBoundary.Planes.Length; i++)
            {
                if (initialBoundary.Planes[i].isBreakable)
                    simState.wallDurabilities[i] = initialBoundary.Planes[i].durability;
                else
                    simState.wallDurabilities[i] = 0;
            }
        }

        simState.isResimulating = false;
        simState.sharedDepthAxis = new FPVector3(new FP64(0), new FP64(0), FP64_ONE);

        if (playerOne.instance != null) Destroy(playerOne.instance);
        if (playerTwo.instance != null) Destroy(playerTwo.instance);

        InputBinding leftBinding = MatchDataManager.LeftKeyBindPreset != null ? MatchDataManager.LeftKeyBindPreset.bindingData : InputBinding.GetDefaultP1();
        InputBinding rightBinding = MatchDataManager.RightKeyBindPreset != null ? MatchDataManager.RightKeyBindPreset.bindingData : InputBinding.GetDefaultP2();

        if (GameFlowManager.Instance.currentBattleType == BattleType.OnlineBattle)
        {
            int localSlot = RoomStateManager.Instance != null ? RoomStateManager.Instance.GetLocalPlayerSlot() : 0;
            if (localSlot == 0) playerOne.customBinding = leftBinding;
            else playerTwo.customBinding = leftBinding;
        }
        else
        {
            playerOne.customBinding = leftBinding;
            playerTwo.customBinding = rightBinding;
        }

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

        bool p1InvertDepth = false; 
        bool p2InvertDepth = false;

        if (RoomStateManager.Instance != null)
        {
            int p1Side = RoomStateManager.Instance.roomModel.p1PreferredSide;
            int p2Side = RoomStateManager.Instance.roomModel.p2PreferredSide;
            p1InvertDepth = p1Side == 1; 
            p2InvertDepth = p2Side == 0;
        }

        SetupPlayer(playerOne, ruleConfig.p1SpawnPos, p1InvertDepth);
        SetupPlayer(playerTwo, ruleConfig.p2SpawnPos, p2InvertDepth);

        if (playerOne.controller != null && playerTwo.controller != null)
        {
            playerOne.controller.SetTarget(playerTwo.controller);
            playerTwo.controller.SetTarget(playerOne.controller);
            
            if (playingUI != null)
            {
                playingUI.InitializeHealthBars(playerOne.controller, playerTwo.controller, simState.isCameraFlipped);
            }
        }

        if (playingUI != null)
        {
            playingUI.SetCameraTargets(playerOne.instance, playerTwo.instance);
            playingUI.SetCameraFlip(simState.isCameraFlipped);
        }

        ResetForNextRound();
    }

    private uint CreateInitialWallBitmask(StageBoundary boundary)
    {
        uint mask = 0;
        if (boundary.Planes != null)
        {
            for (int i = 0; i < boundary.Planes.Length; i++)
            {
                if (boundary.Planes[i].isActive)
                {
                    mask |= (1u << i);
                }
            }
        }
        return mask;
    }

    private void SetupPlayer(PlayerSessionContext context, Vector3 spawnPos, bool invertDepth)
    {
        if (context.characterData == null) return;
        
        context.instance = Instantiate(context.characterData.characterPrefab, spawnPos, Quaternion.identity);
        context.renderer = context.instance.GetComponent<PlayerRenderer>();
        context.controller = new PlayerController();
        context.controller.Initialize(spawnPos, context.characterData, invertDepth);
        context.controller.GetPhysics().SetGlobalGravity(ruleConfig.globalGravity);
        
        if (context.renderer != null)
        {
            context.renderer.InitializeRenderer(context.controller, context.characterData.animationMap.stateMap, context.characterData.effectTable);
        }
    }

    private void ResetForNextRound()
    {
        simState.currentTick = 0;

        if (visualWallController != null)
        {
            visualWallController.ResetAllDebris();
        }
        
        if (GameFlowManager.Instance.currentBattleType == BattleType.Training)
        {
            simState.currentPhase = RoundPhase.Fighting;
            simState.phaseDelayTicks = 0;
        }
        else
        {
            simState.currentPhase = RoundPhase.PreRound;
            simState.phaseDelayTicks = ruleConfig.preRoundDelayFrames;
        }

        simState.simulationScale = FP64_ONE;
        simState.timeAccumulator = new FP64(0);
        simState.sharedDepthAxis = new FPVector3(new FP64(0), new FP64(0), FP64_ONE);
        
        long recoveryTicksLong = (long)Mathf.Max(1, ruleConfig.climaxRecoveryFrames);
        long slowMoRaw = cachedClimaxSlowMoScale.rawValue;
        climaxRecoveryStepFP = new FP64((FP64_ONE.rawValue - slowMoRaw) / recoveryTicksLong);

        scoreContext.currentRound++;

        if (playerOne.controller != null && playerTwo.controller != null)
        {
            playerOne.controller.GetPhysics().SetPosition(ruleConfig.p1SpawnPos); 
            playerTwo.controller.GetPhysics().SetPosition(ruleConfig.p2SpawnPos);

            playerOne.controller.ResetForNewRound();
            playerTwo.controller.ResetForNewRound();
        }

        int timeLimit = RoomStateManager.Instance != null ? RoomStateManager.Instance.roomModel.roundTimeLimit : 99;
        roundTimer.InitializeTimer(timeLimit);
        
        if (connectionState.currentP2PNetwork != null)
        {
            connectionState.currentP2PNetwork.ClearBuffer();
        }
        
        syncController.ResetForNextRound();
        System.Array.Clear(stateBuffer, 0, stateBuffer.Length);
        
        SaveGameState(0);
        simState.isSimulationRunning = true;
    }

    public void ResetTrainingState()
    {
        if (playerOne.controller != null && playerTwo.controller != null)
        {
            playerOne.controller.GetPhysics().SetPosition(ruleConfig.p1SpawnPos); 
            playerTwo.controller.GetPhysics().SetPosition(ruleConfig.p2SpawnPos);

            playerOne.controller.ResetForNewRound();
            playerTwo.controller.ResetForNewRound();
            
            simState.timeAccumulator = new FP64(0);
            simState.simulationScale = FP64_ONE;
            simState.sharedDepthAxis = new FPVector3(new FP64(0), new FP64(0), FP64_ONE);
        }
    }

    public void SetupP2PConnection(string peerIp)
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

    public void ProcessP2PHandshake()
    {
        if (connectionState.currentP2PNetwork != null)
        {
            connectionState.currentP2PNetwork.PumpNetworkTick();
            
            if (connectionState.currentP2PNetwork.GetIsConnected())
            {
                connectionState.isWaitingForP2PConnection = false;
                if (ServerNetworkManager.Instance != null) ServerNetworkManager.Instance.SendHandshake();
            }
        }
    }

    public void TriggerDesyncError()
    {
        if (!simState.isSimulationRunning) return;
        simState.isSimulationRunning = false; 
        
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendMatchEndAction(MatchEndActionType.ReturnToMenu);
        }
        GameFlowManager.Instance.ChangeScene(GameSceneType.OnlineMatchedRoom);
    }

    private void HandleServerGameStart()
    {
        connectionState.isWaitingForServerSync = false;
        
        if (playingUI != null) 
        {
            playingUI.HideMatchResult();
        }
        
        InitializeMatch();
        simState.isSimulationRunning = true;
    }

    public void HandleMatchAborted(GameSceneType targetScene)
    {
        simState.isSimulationRunning = false;
        simState.currentPhase = RoundPhase.PostRound;
    }

    private void HandleRoundVerified()
    {
        PrepareNextRoundOrEndMatch();
    }

    private void HandleRematchSync(bool isP1Ready, bool isP2Ready)
    {
        if (playingUI != null)
        {
            playingUI.UpdateRematchSync(isP1Ready, isP2Ready, simState.isCameraFlipped);
        }
    }

    public void ProcessTick(PlayerInput p1, PlayerInput p2)
    {
        RunTick(p1, p2);
        SaveGameState(simState.currentTick);
        simState.currentTick++;
    }

    private void RunTick(PlayerInput p1, PlayerInput p2)
    {
        if (simState.currentPhase == RoundPhase.PreRound)
        {
            p1.flags = InputFlags.None; 
            p2.flags = InputFlags.None;
        }
        else if (simState.currentPhase == RoundPhase.PostRound)
        {
            int p1Hp = playerOne.controller != null ? playerOne.controller.GetCombat().GetCurrentHealth() : 0;
            int p2Hp = playerTwo.controller != null ? playerTwo.controller.GetCombat().GetCurrentHealth() : 0;
            int timeFrames = roundTimer.GetCurrentFrames();
            
            bool p1Wins = p1Hp > 0 && (p2Hp <= 0 || (timeFrames <= 0 && p1Hp > p2Hp));
            bool p2Wins = p2Hp > 0 && (p1Hp <= 0 || (timeFrames <= 0 && p2Hp > p1Hp));
            
            if (!p1Wins) p1.flags = InputFlags.None;
            if (!p2Wins) p2.flags = InputFlags.None;
        }
        
        if (simState.currentPhase == RoundPhase.Fighting && currentLogic != null && currentLogic.ShouldUpdateTimer())
        {
            roundTimer.UpdateTick();
        }
        
        if (playerOne.controller != null && playerTwo.controller != null)
        {
            bool isClimax = roundReferee.CheckClimaxCondition(playerOne.controller, playerTwo.controller);
            
            if (isClimax)
            {
                simState.simulationScale = cachedClimaxSlowMoScale;
            }
            else if (simState.simulationScale.rawValue < FP64_ONE.rawValue)
            {
                simState.simulationScale += climaxRecoveryStepFP;
                if (simState.simulationScale.rawValue > FP64_ONE.rawValue)
                {
                    simState.simulationScale = FP64_ONE;
                }
            }

            simState.timeAccumulator += simState.simulationScale;
            simState.isLogicStep = false;

            while (simState.timeAccumulator.rawValue >= FP64_ONE.rawValue)
            {
                simState.isLogicStep = true;
                simState.timeAccumulator -= FP64_ONE;
            }

            simulationCore.SimulateFrame(playerOne.controller, playerTwo.controller, p1, p2, ref simState, HandleHitSpark);
        }

        UpdateRoundPhase();
        
        if (!simState.isResimulating)
        {
            SyncVisuals();
        }
    }

    private void UpdateRoundPhase()
    {
        if (simState.currentPhase == RoundPhase.PreRound)
        {
            if (simState.phaseDelayTicks > 0)
            {
                simState.phaseDelayTicks--;
            }
            else
            {
                simState.currentPhase = RoundPhase.Fighting;
            }
        }
        else if (simState.currentPhase == RoundPhase.Fighting)
        {
            if (currentLogic != null && currentLogic.ShouldCheckRoundEnd())
            {
                CheckRoundEndCondition();
            }
        }
        else if (simState.currentPhase == RoundPhase.PostRound)
        {
            if (simState.phaseDelayTicks > 0)
            {
                simState.phaseDelayTicks--;
                
                if (simState.phaseDelayTicks == 0)
                {
                    EvaluateRoundResult(out int winnerSlot);
                    
                    if (!simState.isResimulating)
                    {
                        if (GameFlowManager.Instance.currentBattleType == BattleType.OnlineBattle)
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
    }

    private void CheckRoundEndCondition()
    {
        int timeFrames = roundTimer.GetCurrentFrames();
        bool isOver = roundReferee.IsRoundOver(playerOne.controller, playerTwo.controller, timeFrames);

        if (isOver)
        {
            simState.currentPhase = RoundPhase.PostRound;
            simState.phaseDelayTicks = ruleConfig.postRoundDelayFrames;
        }
    }

    private void EvaluateRoundResult(out int winnerSlot)
    {
        int timeFrames = roundTimer.GetCurrentFrames();
        winnerSlot = roundReferee.DetermineWinnerSlot(playerOne.controller, playerTwo.controller, timeFrames);

        if (winnerSlot == -1)
        {
            SetDrawState();
        }
        else if (winnerSlot == 0)
        {
            scoreContext.p1RoundWins++; 
            SetWinLossState(playerOne, playerTwo);
        }
        else if (winnerSlot == 1)
        {
            scoreContext.p2RoundWins++; 
            SetWinLossState(playerTwo, playerOne);
        }

        if (playingUI != null)
        {
            int leftWins = simState.isCameraFlipped ? scoreContext.p2RoundWins : scoreContext.p1RoundWins;
            int rightWins = simState.isCameraFlipped ? scoreContext.p1RoundWins : scoreContext.p2RoundWins;
            playingUI.UpdateWinCounter(leftWins, rightWins);
        }
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
        stateBuffer[idx].currentPhase = simState.currentPhase; 
        stateBuffer[idx].phaseDelayTicks = simState.phaseDelayTicks;
        stateBuffer[idx].simulationScale = simState.simulationScale; 
        stateBuffer[idx].timeAccumulator = simState.timeAccumulator;
        stateBuffer[idx].scoreContext = scoreContext;
        stateBuffer[idx].stageActiveWallBitmask = simState.stageActiveWallBitmask;

        unsafe
        {
            for (int i = 0; i < 32; i++)
            {
                stateBuffer[idx].wallDurabilities[i] = simState.wallDurabilities[i];
            }
        }

        roundTimer.ExportState(ref stateBuffer[idx]);
        
        if (playerOne.controller != null) playerOne.controller.ExportState(ref stateBuffer[idx].p1Snapshot);
        if (playerTwo.controller != null) playerTwo.controller.ExportState(ref stateBuffer[idx].p2Snapshot);
    }

    private void LoadGameState(int tick)
    {
        int idx = tick % ROLLBACK_WINDOW;
        simState.sharedDepthAxis = stateBuffer[idx].sharedDepthAxis; 
        simState.currentPhase = stateBuffer[idx].currentPhase;
        simState.phaseDelayTicks = stateBuffer[idx].phaseDelayTicks; 
        simState.simulationScale = stateBuffer[idx].simulationScale;
        simState.timeAccumulator = stateBuffer[idx].timeAccumulator;
        scoreContext = stateBuffer[idx].scoreContext;
        simState.stageActiveWallBitmask = stateBuffer[idx].stageActiveWallBitmask;

        unsafe
        {
            for (int i = 0; i < 32; i++)
            {
                simState.wallDurabilities[i] = stateBuffer[idx].wallDurabilities[i];
            }
        }

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
        int actualRealTick = simState.currentTick;
        
        for (int t = from; t < to; t++)
        {
            int idx = t % ROLLBACK_WINDOW;
            RunTick(new PlayerInput { flags = p1Buffer[idx] }, new PlayerInput { flags = p2Buffer[idx] });
            SaveGameState(t);
        }
        simState.currentTick = actualRealTick;
        simState.isResimulating = false;
    }

    private void DetermineCameraFlipState()
    {
        if (RoomStateManager.Instance != null)
        {
            int slot = RoomStateManager.Instance.GetLocalPlayerSlot();
            RoomStateModel roomState = RoomStateManager.Instance.roomModel;
            int mySide = (slot == 0) ? roomState.p1PreferredSide : roomState.p2PreferredSide;
            
            simState.isCameraFlipped = (slot == 0 && mySide == 1) || (slot == 1 && mySide == 0);

            if (playingUI != null)
            {
                playingUI.SetCameraFlip(simState.isCameraFlipped);
            }
        }
    }

    private void SyncVisuals()
    {
        float currentVisualScale = (float)simState.simulationScale.rawValue / (float)FP64_ONE.rawValue;
        
        if (visualWallController != null && currentStageData != null)
        {
            for (int i = 0; i < currentStageData.boundary.Planes.Length; i++)
            {
                bool isWallActive = (simState.stageActiveWallBitmask & (1u << i)) != 0;
                visualWallController.SetWallVisualActive(i, isWallActive);
            }
        }

        if (cameraManager != null)
        {
            cameraManager.UpdateWallBitmask(simState.stageActiveWallBitmask);
            cameraManager.UpdateDepthAxis(simState.sharedDepthAxis.ToVector3());
        }

        if (VfxManager.Instance != null)
        {
            VfxManager.Instance.SetGlobalScale(currentVisualScale);
        }
        
        if (playerOne.renderer != null) playerOne.renderer.UpdateRenderer(currentVisualScale);
        if (playerTwo.renderer != null) playerTwo.renderer.UpdateRenderer(currentVisualScale);

        if (playingUI != null && roundTimer != null)
        {
            playingUI.UpdateRoundTimer(roundTimer.GetRemainingSeconds());
            playingUI.SyncBannerState(simState.currentPhase, simState.phaseDelayTicks, roundTimer.GetCurrentFrames());
        }
    }

    private void HandleHitSpark(PlayerController target, Vector3 point, EffectType effect)
    {
        if (simState.isResimulating) return;
        
        PlayerSessionContext ctx = (target == playerOne.controller) ? playerOne : playerTwo;
        if (ctx.renderer != null)
        {
            ctx.renderer.PlayHitSpark(point, effect);
        }
    }

    public void HideMatchResultUI() 
    { 
        if (playingUI != null) playingUI.HideMatchResult(); 
    }

    private void ProcessFinalMatchEnd()
    {
        if (playingUI != null)
        {
            playingUI.ShowMatchResult(scoreContext.p1RoundWins, scoreContext.p2RoundWins, scoreContext.requiredRoundWins, connectionState.localPlayerSlot);
        }
    }

    private void HandleMatchEndAction(MatchEndActionType actionType)
    {
        if (currentLogic != null)
        {
            currentLogic.HandleMatchEndAction(actionType);
        }
    }

    private void ReportRoundEndToServer(int winnerSlot)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendRoundEndReport(winnerSlot, scoreContext.p1RoundWins, scoreContext.p2RoundWins);
        }
    }
}