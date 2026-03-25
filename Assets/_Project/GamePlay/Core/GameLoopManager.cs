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
    
    [SerializeField] private bool isDebugRollbackEnabled = false;
    [SerializeField] private int debugRollbackFrames = 5;
    [SerializeField] private int debugRollbackInterval = 30;
    
    [SerializeField] private int maxRollbackFrames = 7;
    [SerializeField] private int inputDelayFrames = 2;
    [SerializeField] private float postMatchDelaySeconds = 3.0f;

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
    private bool isWaitingForGameStart;
    private bool isCameraFlipped;
    private FPVector3 sharedDepthAxis;

    private Dictionary<int, ulong> localHashBuffer;
    private int lastHashedTick;
    private bool isDesyncDetected;
    private int postMatchDelayTicks;

    private void Awake()
    {
        if (MatchDataManager.P1CharacterData != null) playerOne.characterData = MatchDataManager.P1CharacterData;
        if (MatchDataManager.P2CharacterData != null) playerTwo.characterData = MatchDataManager.P2CharacterData;

        Time.fixedDeltaTime = 1f / 60f;
        Application.targetFrameRate = 120;

        simulationCore = new GameSimulationCore();
        simulationCore.Initialize(playerCollisionMinDistance);

        NetworkSessionManager.Instance.OnPeerAddressReceived += HandlePeerConnection;
        NetworkSessionManager.Instance.OnGameStartReceived += HandleGameStartCommand;

        bool isOnline = GameFlowManager.Instance.currentMode != ConnectionMode.Offline;
        if (isOnline)
        {
            isWaitingForGameStart = true;
        }
        else
        {
            InitializeMatch(false);
        }
    }

    private void Start()
    {
        DetermineCameraFlipState();

        bool isOnline = GameFlowManager.Instance.currentMode != ConnectionMode.Offline;
        if (isOnline)
        {
            IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
            int localSlot = session.GetLocalPlayerSlot();

            if (localSlot == 1)
            {
                NetworkSessionManager.Instance.StartP2PListen();
            }

            NetworkSessionManager.Instance.SendHandshake();
        }
    }

    private void OnDestroy()
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnPeerAddressReceived -= HandlePeerConnection;
            NetworkSessionManager.Instance.OnGameStartReceived -= HandleGameStartCommand;
        }
    }

    private void FixedUpdate()
    {
        bool isOnline = GameFlowManager.Instance.currentMode != ConnectionMode.Offline;

        if (isOnline)
        {
            NetworkSessionManager.Instance.UpdateNetwork();
        }

        if (isWaitingForGameStart || !isSimulationRunning) return;

        if (isOnline)
        {
            if (!NetworkSessionManager.Instance.GetIsConnected()) return;

            ProcessOnlineTick();
            VerifySyncState();
        }
        else
        {
            ProcessOfflineTick();
        }
    }

    public bool GetIsStalling() => (currentTick - latestConfirmedTick) > maxRollbackFrames;
    public bool GetIsDesyncDetected() => isDesyncDetected;
    public int GetCurrentTick() => currentTick;
    public RoundTimerManager GetRoundTimer() => roundTimer;
    public PlayerState_Type GetP1State() => playerOne.controller != null ? playerOne.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.controller != null ? playerOne.controller.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwo.controller != null ? playerTwo.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.controller != null ? playerTwo.controller.GetPosition() : Vector3.zero;
    public PlayerController GetPlayerOneController() => playerOne.controller;
    public PlayerController GetPlayerTwoController() => playerTwo.controller;

    private void DetermineCameraFlipState()
    {
        IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
        if (session != null)
        {
            int localSlot = session.GetLocalPlayerSlot();
            RoomStateModel roomState = session.GetRoomState();
            
            int mySide = (localSlot == 0) ? roomState.p1PreferredSide : roomState.p2PreferredSide;
            isCameraFlipped = (localSlot == 0 && mySide == 1) || (localSlot == 1 && mySide == 0);

            if (cameraManager != null)
            {
                cameraManager.SetCameraFlip(isCameraFlipped);
            }
        }
    }

    private void HandleGameStartCommand()
    {
        isWaitingForGameStart = false;
        InitializeMatch(true);
    }

    private void InitializeMatch(bool isNetworkReset)
    {
        currentTick = 0;
        isRoundOver = false;
        isDesyncDetected = false;
        isResimulating = false;
        lastHashedTick = -1;
        latestConfirmedTick = 0;

        postMatchDelayTicks = Mathf.RoundToInt(postMatchDelaySeconds * 60f);

        stateBuffer = new GameStateSnapshot[ROLLBACK_WINDOW];
        p1InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        p2InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        localHashBuffer = new Dictionary<int, ulong>();
        sharedDepthAxis = new FPVector3(new FP64(0), new FP64(0), FP64.FromFloat(1f));

        roundTimer = new RoundTimerManager();
        roundTimer.InitializeTimer(99);

        if (isNetworkReset) NetworkSessionManager.Instance.ClearBuffer();

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

        isSimulationRunning = true;
        SaveGameState(0);
    }

    private void HandlePeerConnection(string peerIp)
    {
        IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
        int localSlot = session.GetLocalPlayerSlot();

        if (localSlot == 0)
        {
            NetworkSessionManager.Instance.ConnectToPeer(peerIp);
        }
    }

    private void ProcessOnlineTick()
    {
        IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
        int localPlayerIndex = session.GetLocalPlayerSlot();
        bool isP1Local = (localPlayerIndex == 0);

        VerifyRemoteInputsAndRollback(isP1Local);
        BroadcastSyncHashes();

        if (currentTick - latestConfirmedTick > maxRollbackFrames)
        {
            ResendLastInput(isP1Local);
            return;
        }

        UpdateLocalInput(isP1Local, localPlayerIndex);
        PredictRemoteInput(isP1Local);

        ProcessTick(new PlayerInput { flags = p1InputBuffer[currentTick % ROLLBACK_WINDOW] }, 
                    new PlayerInput { flags = p2InputBuffer[currentTick % ROLLBACK_WINDOW] });
    }

    private void VerifyRemoteInputsAndRollback(bool isP1Local)
    {
        int rollbackTick = -1;
        InputFlags lastConfirmedRemote = InputFlags.None;

        for (int t = latestConfirmedTick; t < currentTick; t++)
        {
            if (NetworkSessionManager.Instance.TryGetRemoteInput(t, out ushort rawInput))
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

    private void UpdateLocalInput(bool isP1Local, int localIdx)
    {
        PlayerInput physicalInput = inputProvider.GetCurrentInput(currentTick, localIdx, isCameraFlipped);
        int targetTick = currentTick + inputDelayFrames;
        
        if (isP1Local) p1InputBuffer[targetTick % ROLLBACK_WINDOW] = physicalInput.flags;
        else p2InputBuffer[targetTick % ROLLBACK_WINDOW] = physicalInput.flags;
        NetworkSessionManager.Instance.SendLocalInput(targetTick, (ushort)physicalInput.flags);
    }

    private void PredictRemoteInput(bool isP1Local)
    {
        int idx = currentTick % ROLLBACK_WINDOW;
        if (NetworkSessionManager.Instance.TryGetRemoteInput(currentTick, out ushort rawInput))
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

    private void ResendLastInput(bool isP1Local)
    {
        int lastTick = Mathf.Max(0, currentTick + inputDelayFrames - 1);
        InputFlags lastInput = isP1Local ? p1InputBuffer[lastTick % ROLLBACK_WINDOW] : p2InputBuffer[lastTick % ROLLBACK_WINDOW];
        NetworkSessionManager.Instance.SendLocalInput(lastTick, (ushort)lastInput);
    }

    private void BroadcastSyncHashes()
    {
        int maxHashTick = Mathf.Min(latestConfirmedTick, currentTick);
        for (int t = lastHashedTick + 1; t < maxHashTick; t++)
        {
            if (t % SYNC_VERIFY_INTERVAL == 0)
            {
                ulong hash = StateHashUtility.ComputeHash(stateBuffer[t % ROLLBACK_WINDOW]);
                localHashBuffer[t] = hash;
                NetworkSessionManager.Instance.SendSyncHash(t, hash);
                lastHashedTick = t;
            }
        }
    }

    private void VerifySyncState()
    {
        List<int> verifiedTicks = new List<int>();
        
        foreach (var kvp in localHashBuffer)
        {
            bool hasRemoteHash = NetworkSessionManager.Instance.TryGetRemoteHash(kvp.Key, out ulong remoteHash);
            
            if (hasRemoteHash)
            {
                bool isHashMismatch = kvp.Value != remoteHash;
                
                if (isHashMismatch) 
                {
                    TriggerDesyncError(kvp.Key, kvp.Value, remoteHash);
                }
                else 
                {
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

    private void ProcessTick(PlayerInput p1, PlayerInput p2)
    {
        int idx = currentTick % ROLLBACK_WINDOW;
        p1InputBuffer[idx] = p1.flags;
        p2InputBuffer[idx] = p2.flags;
        RunTick(p1, p2);
        SaveGameState(currentTick);
        currentTick++;
    }

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
        
        simulationCore.SimulateFrame(playerOne.controller, playerTwo.controller, p1, p2, ref sharedDepthAxis, HandleHitSpark);

        UpdateMatchState();

        if (!isResimulating) SyncVisuals();
    }

    private void UpdateMatchState()
    {
        if (isRoundOver)
        {
            ProcessPostMatchDelay();
            return;
        }

        CheckRoundEndCondition();
    }

    private void CheckRoundEndCondition()
    {
        int p1Hp = playerOne.controller.GetCombat().GetCurrentHealth();
        int p2Hp = playerTwo.controller.GetCombat().GetCurrentHealth();
        int timeFrames = roundTimer.GetCurrentFrames();

        if (p1Hp > 0 && p2Hp > 0 && timeFrames > 0)
        {
            return;
        }

        isRoundOver = true;
    }

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

    private void ApplyFinalMatchResult()
    {
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

    private void ShowSceneTransitionUI()
    {
        Debug.Log("Show Scene Transition UI");
    }

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

    private void SyncVisuals()
    {
        if (playerOne.renderer != null) playerOne.renderer.UpdateRenderer();
        if (playerTwo.renderer != null) playerTwo.renderer.UpdateRenderer();

        if (roundTimer != null && roundTimerDisplay != null)
        {
            roundTimerDisplay.SetNumber(roundTimer.GetRemainingSeconds());
        }
    }

    private void TriggerDesyncError(int tick, ulong local, ulong remote)
    {
        isDesyncDetected = true;
        Debug.LogError($"[DESYNC] Tick: {tick} | Local: {local} | Remote: {remote}");
    }

    private void HandleHitSpark(PlayerController target, Vector3 point, EffectType effect)
    {
        if (isResimulating) return;
        PlayerSessionContext ctx = (target == playerOne.controller) ? playerOne : playerTwo;
        if (ctx.renderer != null) ctx.renderer.PlayHitSpark(point, effect);
    }

    private void ProcessOfflineTick()
    {
        bool isP1Right = cameraManager.IsPlayerOneOnRightSide();
        PlayerInput p1 = inputProvider.GetCurrentInput(currentTick, 0, !isP1Right);
        PlayerInput p2 = inputProvider.GetCurrentInput(currentTick, 1, isP1Right);
        ProcessTick(p1, p2);
        if (isDebugRollbackEnabled && (currentTick % debugRollbackInterval == 0) && currentTick > debugRollbackFrames) Resimulate(currentTick - debugRollbackFrames, currentTick);
    }
}