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
        if (hasCustomBinding)
        {
            return customBinding;
        }

        return isPlayerOne ? InputBinding.GetDefaultP1() : InputBinding.GetDefaultP2();
    }
}

public struct GameStateSnapshot
{
    public int tick;
    public PlayerSnapshot p1Snapshot;
    public PlayerSnapshot p2Snapshot;
    public FPVector3 sharedDepthAxis;
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
    [SerializeField] private NetworkSessionManager networkSession;
    [SerializeField] private bool isDebugRollbackEnabled = false;
    [SerializeField] private int debugRollbackFrames = 5;
    [SerializeField] private int debugRollbackInterval = 30;

    private const int ROLLBACK_WINDOW = 60;

    private GameStateSnapshot[] stateBuffer;
    private InputFlags[] p1InputBuffer;
    private InputFlags[] p2InputBuffer;
    private LocalInputProvider inputProvider;
    private GameSimulationCore simulationCore;
    
    private int currentTick;
    private int latestConfirmedTick;
    private bool isSimulationRunning;
    private bool isRoundOver;
    private bool isResimulating;
    private FPVector3 sharedDepthAxis;

    private Dictionary<int, ulong> localHashBuffer;
    private int lastHashedTick;
    private const int SYNC_VERIFY_INTERVAL = 60;
    private bool isDesyncDetected;
    public bool GetIsDesyncDetected() => isDesyncDetected;
    public int GetCurrentTick() => currentTick;
    public PlayerState_Type GetP1State() => playerOne.controller != null ? playerOne.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.controller != null ? playerOne.controller.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwo.controller != null ? playerTwo.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.controller != null ? playerTwo.controller.GetPosition() : Vector3.zero;
    public PlayerController GetPlayerOneController() => playerOne.controller;
    public PlayerController GetPlayerTwoController() => playerTwo.controller;


    private void Awake()
    {
        Time.fixedDeltaTime = 1f / 60f;
        Application.targetFrameRate = 120;

        simulationCore = new GameSimulationCore();
        simulationCore.Initialize(playerCollisionMinDistance);

        InitializeMatch(false);

        bool hasNetworkSession = networkSession != null;
        if (hasNetworkSession)
        {
            networkSession.OnConnectionEstablished += () => InitializeMatch(true);
        }
    }

    private void FixedUpdate()
    {
        if (!isSimulationRunning) return;

        bool hasNetworkSession = networkSession != null;
        bool isNetworkActive = hasNetworkSession && networkSession.GetIsInitialized();

        if (hasNetworkSession)
        {
            networkSession.UpdateNetwork();
        }

        bool isOfflineMode = !isNetworkActive || !networkSession.GetIsConnected();
        if (isOfflineMode)
        {
            ProcessOfflineTick();
            return;
        }

        ProcessOnlineTick();
        VerifySyncState();
    }


    private void TriggerDebugRollback()
    {
        int rollbackTargetTick = currentTick - debugRollbackFrames;
        Resimulate(rollbackTargetTick, currentTick);
    }

    private void InitializeRollbackBuffers()
    {
        stateBuffer = new GameStateSnapshot[ROLLBACK_WINDOW];
        p1InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        p2InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        localHashBuffer = new Dictionary<int, ulong>();

        latestConfirmedTick = 0;
        lastHashedTick = -1;
        isResimulating = false;
        isDesyncDetected = false;
        sharedDepthAxis = new FPVector3(new FP64(0), new FP64(0), FP64.FromFloat(1f));
    }

    private void InitializeMatch(bool isNetworkReset)
    {
        currentTick = 0;
        isRoundOver = false;

        InitializeRollbackBuffers();

        if (isNetworkReset && networkSession != null)
        {
            networkSession.ClearBuffer();
        }

        bool hasP1Instance = playerOne.instance != null;
        if (hasP1Instance) Destroy(playerOne.instance);

        bool hasP2Instance = playerTwo.instance != null;
        if (hasP2Instance) Destroy(playerTwo.instance);

        bool isInputProviderMissing = inputProvider == null;
        if (isInputProviderMissing)
        {
            InputBinding p1Final = playerOne.GetBinding(true);
            InputBinding p2Final = playerTwo.GetBinding(false);
            inputProvider = new LocalInputProvider(p1Final, p2Final);
        }

        SetupPlayer(playerOne, p1SpawnPos);
        SetupPlayer(playerTwo, p2SpawnPos);

        bool hasBothControllers = playerOne.controller != null && playerTwo.controller != null;
        if (hasBothControllers)
        {
            playerOne.controller.SetTarget(playerTwo.controller);
            playerTwo.controller.SetTarget(playerOne.controller);

            playerOne.controller.GetCombat().OnDefeated -= HandlePlayerDefeated;
            playerTwo.controller.GetCombat().OnDefeated -= HandlePlayerDefeated;
            playerOne.controller.GetCombat().OnDefeated += HandlePlayerDefeated;
            playerTwo.controller.GetCombat().OnDefeated += HandlePlayerDefeated;

            bool isP1UIValid = p1HealthBar != null;
            if (isP1UIValid) p1HealthBar.Initialize(playerOne.controller.GetCombat(), false);

            bool isP2UIValid = p2HealthBar != null;
            if (isP2UIValid) p2HealthBar.Initialize(playerTwo.controller.GetCombat(), true);
        }

        bool canSetCamera = cameraManager != null;
        if (canSetCamera)
        {
            cameraManager.SetTargetPlayers(playerOne.instance, playerTwo.instance);
        }

        isSimulationRunning = true;

        SaveGameState(currentTick);
    }

    private void SetupPlayer(PlayerSessionContext context, Vector3 spawnPos)
    {
        bool isDataInvalid = context.characterData == null || context.characterData.characterPrefab == null;
        if (isDataInvalid) return;

        context.instance = Instantiate(context.characterData.characterPrefab, spawnPos, Quaternion.identity);
        context.renderer = context.instance.GetComponent<PlayerRenderer>();

        context.controller = new PlayerController();
        context.controller.Initialize(spawnPos, context.characterData);
        context.controller.GetPhysics().SetGlobalGravity(globalGravity);

        bool hasRenderer = context.renderer != null;
        if (hasRenderer)
        {
            context.renderer.InitializeRenderer(
                context.controller,
                context.characterData.animationMap.stateMap,
                context.characterData.effectTable
            );
        }
    }

    private void HandlePlayerDefeated(PlayerController defeatedPlayer)
    {
        if (isRoundOver) return;

        isRoundOver = true;

        bool isPlayerOneDefeated = defeatedPlayer == playerOne.controller;

        if (isPlayerOneDefeated)
        {
            TriggerRoundEnd(playerTwo);
        }
        else
        {
            TriggerRoundEnd(playerOne);
        }
    }

    private void TriggerRoundEnd(PlayerSessionContext winner)
    {
        bool isWinnerValid = winner != null && winner.controller != null;
        if (isWinnerValid)
        {
            winner.controller.GetStateMachine().TransitionTo(PlayerState_Type.Win, true);
        }
    }

    private void ProcessOfflineTick()
    {
        bool isP1OnRight = cameraManager.IsPlayerOneOnRightSide();
        PlayerInput p1Local = inputProvider.GetCurrentInput(currentTick, 0, !isP1OnRight);
        PlayerInput p2Local = inputProvider.GetCurrentInput(currentTick, 1, isP1OnRight);

        ProcessTick(p1Local, p2Local);

        bool shouldForceRollback = isDebugRollbackEnabled && (currentTick % debugRollbackInterval == 0) && currentTick > debugRollbackFrames;
        if (shouldForceRollback)
        {
            TriggerDebugRollback();
        }
    }

    private void ProcessOnlineTick()
    {
        bool isServer = networkSession.GetIsServer();
        int localPlayerIndex = isServer ? 0 : 1;
        bool isP1OnRight = cameraManager.IsPlayerOneOnRightSide();
        bool isLocalFacingRight = isServer ? !isP1OnRight : isP1OnRight;

        PlayerInput localInput = inputProvider.GetCurrentInput(currentTick, localPlayerIndex, isLocalFacingRight);
        networkSession.BroadcastLocalInput(currentTick, localInput.flags);

        int bufferIndex = currentTick % ROLLBACK_WINDOW;
        if (isServer) p1InputBuffer[bufferIndex] = localInput.flags;
        else p2InputBuffer[bufferIndex] = localInput.flags;

        InputFlags predictedRemote = InputFlags.None;
        bool hasPreviousTick = currentTick > 0;
        if (hasPreviousTick)
        {
            int prevIndex = (currentTick - 1) % ROLLBACK_WINDOW;
            predictedRemote = isServer ? p2InputBuffer[prevIndex] : p1InputBuffer[prevIndex];
        }

        if (isServer) p2InputBuffer[bufferIndex] = predictedRemote;
        else p1InputBuffer[bufferIndex] = predictedRemote;

        int rollbackTick = -1;

        for (int t = latestConfirmedTick; t <= currentTick; t++)
        {
            bool hasRealInput = networkSession.TryGetRemoteInput(t, out InputFlags actualRemote);
            if (hasRealInput)
            {
                int idx = t % ROLLBACK_WINDOW;
                InputFlags predicted = isServer ? p2InputBuffer[idx] : p1InputBuffer[idx];

                bool isMismatch = predicted != actualRemote;
                if (isMismatch)
                {
                    if (isServer) p2InputBuffer[idx] = actualRemote;
                    else p1InputBuffer[idx] = actualRemote;

                    bool isFirstMismatch = rollbackTick == -1 || t < rollbackTick;
                    if (isFirstMismatch)
                    {
                        rollbackTick = t;
                    }
                }
                latestConfirmedTick = t + 1;
            }
            else
            {
                break;
            }
        }

        for (int t = lastHashedTick + 1; t < latestConfirmedTick; t++)
        {
            bool isVerifyTick = t % SYNC_VERIFY_INTERVAL == 0;
            if (isVerifyTick)
            {
                int idx = t % ROLLBACK_WINDOW;
                ulong stateHash = StateHashUtility.ComputeHash(stateBuffer[idx]);
                localHashBuffer[t] = stateHash;
                networkSession.BroadcastSyncHash(t, stateHash);
                lastHashedTick = t;
            }
        }

        bool isRollbackNeeded = rollbackTick != -1;
        if (isRollbackNeeded)
        {
            Resimulate(rollbackTick, currentTick);
        }

        PlayerInput p1Final = new PlayerInput { flags = p1InputBuffer[bufferIndex] };
        PlayerInput p2Final = new PlayerInput { flags = p2InputBuffer[bufferIndex] };

        ProcessTick(p1Final, p2Final);
    }

    private void ProcessTick(PlayerInput p1Input, PlayerInput p2Input)
    {
        int bufferIndex = currentTick % ROLLBACK_WINDOW;
        p1InputBuffer[bufferIndex] = p1Input.flags;
        p2InputBuffer[bufferIndex] = p2Input.flags;

        RunTick(p1Input, p2Input);
        SaveGameState(currentTick);

        currentTick++;
    }

    private void SaveGameState(int tick)
    {
        int index = tick % ROLLBACK_WINDOW;
        stateBuffer[index].tick = tick;
        stateBuffer[index].sharedDepthAxis = sharedDepthAxis;

        bool isP1Valid = playerOne.controller != null;
        if (isP1Valid) playerOne.controller.ExportState(ref stateBuffer[index].p1Snapshot);

        bool isP2Valid = playerTwo.controller != null;
        if (isP2Valid) playerTwo.controller.ExportState(ref stateBuffer[index].p2Snapshot);
    }

    private void LoadGameState(int tick)
    {
        int index = tick % ROLLBACK_WINDOW;
        GameStateSnapshot snapshot = stateBuffer[index];
        sharedDepthAxis = snapshot.sharedDepthAxis;

        bool isP1Valid = playerOne.controller != null;
        if (isP1Valid) playerOne.controller.ImportState(snapshot.p1Snapshot);

        bool isP2Valid = playerTwo.controller != null;
        if (isP2Valid) playerTwo.controller.ImportState(snapshot.p2Snapshot);
    }

    private void Resimulate(int fromTick, int toTick)
    {
        isResimulating = true;

        int loadTick = Mathf.Max(0, fromTick - 1);
        LoadGameState(loadTick);

        int simulatedTick = loadTick + 1;
        while (simulatedTick < toTick)
        {
            int index = simulatedTick % ROLLBACK_WINDOW;
            PlayerInput p1Input = new PlayerInput { flags = p1InputBuffer[index] };
            PlayerInput p2Input = new PlayerInput { flags = p2InputBuffer[index] };

            RunTick(p1Input, p2Input);
            SaveGameState(simulatedTick);
            simulatedTick++;
        }

        isResimulating = false;
    }
    private void VerifySyncState()
    {
        List<int> verifiedTicks = new List<int>();

        foreach (var kvp in localHashBuffer)
        {
            int targetTick = kvp.Key;
            ulong localHash = kvp.Value;

            bool hasRemoteHash = networkSession.TryGetRemoteHash(targetTick, out ulong remoteHash);
            if (hasRemoteHash)
            {
                bool isDesynced = localHash != remoteHash;
                if (isDesynced)
                {
                    TriggerDesyncError(targetTick, localHash, remoteHash);
                }
                verifiedTicks.Add(targetTick);
            }
        }

        foreach (int tick in verifiedTicks)
        {
            localHashBuffer.Remove(tick);
        }
    }

    private void TriggerDesyncError(int tick, ulong localHash, ulong remoteHash)
    {
        isDesyncDetected = true;
        Debug.LogError($"[DESYNC DETECTED] Tick: {tick} | Local Hash: {localHash} | Remote Hash: {remoteHash}");
    }

    private void RunTick(PlayerInput p1Input, PlayerInput p2Input)
    {
        bool isMatchEnded = isRoundOver;
        if (isMatchEnded)
        {
            p1Input.flags = InputFlags.None;
            p2Input.flags = InputFlags.None;
        }

        simulationCore.SimulateFrame(playerOne.controller, playerTwo.controller, p1Input, p2Input, ref sharedDepthAxis, HandleHitSpark);

        if (!isResimulating)
        {
            SyncVisuals();
        }
    }

    private void HandleHitSpark(PlayerController targetController, Vector3 hitPoint, EffectType effectType)
    {
        if (isResimulating) return;

        bool isP1Target = targetController == playerOne.controller;
        PlayerSessionContext targetContext = isP1Target ? playerOne : playerTwo;

        bool hasRenderer = targetContext.renderer != null;
        if (hasRenderer)
        {
            targetContext.renderer.PlayHitSpark(hitPoint, effectType);
        }
    }

    private void SyncVisuals()
    {
        UpdatePlayerVisual(playerOne);
        UpdatePlayerVisual(playerTwo);
    }

    private void UpdatePlayerVisual(PlayerSessionContext context)
    {
        bool isInvalidContext = context == null || context.renderer == null;
        if (isInvalidContext) return;

        context.renderer.UpdateRenderer();
    }
}