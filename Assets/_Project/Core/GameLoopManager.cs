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
}

public class GameLoopManager : MonoBehaviour
{
    [Header("Camera Manager")]
    [SerializeField] private CameraManager cameraManager;

    [Header("Global Settings")]
    [SerializeField] private float playerCollisionMinDistance = 1.0f;
    [SerializeField] private float globalGravity = 0.02f;

    [Header("Players")]
    [SerializeField] private PlayerSessionContext playerOne;
    [SerializeField] private PlayerSessionContext playerTwo;

    [SerializeField] private Vector3 p1SpawnPos = new Vector3(-2, 0, 0);
    [SerializeField] private Vector3 p2SpawnPos = new Vector3(2, 0, 0);

    [Header("UI Managers")]
    [SerializeField] private HealthBarController p1HealthBar;
    [SerializeField] private HealthBarController p2HealthBar;

    [Header("Network")]
    [SerializeField] private NetworkSessionManager networkSession;

    [Header("Rollback Debug")]
    [SerializeField] private bool isDebugRollbackEnabled = false;
    [SerializeField] private int debugRollbackFrames = 5;
    [SerializeField] private int debugRollbackInterval = 30;

    private const int ROLLBACK_WINDOW = 60;
    
    private GameStateSnapshot[] stateBuffer;
    private InputFlags[] p1InputBuffer;
    private InputFlags[] p2InputBuffer;
    
    private LocalInputProvider inputProvider;
    private int currentTick;
    private int latestConfirmedTick;
    
    private bool isSimulationRunning;
    private bool isRoundOver;
    private bool isResimulating;

    public int GetCurrentTick() => currentTick;
    public PlayerState_Type GetP1State() => playerOne.controller != null ? playerOne.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.controller != null ? playerOne.controller.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwo.controller != null ? playerTwo.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.controller != null ? playerTwo.controller.GetPosition() : Vector3.zero;
    public PlayerController GetPlayerOneController() => playerOne.controller;
    public PlayerController GetPlayerTwoController() => playerTwo.controller;
    private void TriggerDebugRollback()
    {
        int rollbackTargetTick = currentTick - debugRollbackFrames;
        
        Resimulate(rollbackTargetTick, currentTick);
    }

    private void Awake()
    {
        InitializeMatch(false);

        bool hasNetworkSession = networkSession != null;
        if (hasNetworkSession)
        {
            networkSession.OnConnectionEstablished += () => InitializeMatch(true);
        }
    }

    private void InitializeRollbackBuffers()
    {
        stateBuffer = new GameStateSnapshot[ROLLBACK_WINDOW];
        p1InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        p2InputBuffer = new InputFlags[ROLLBACK_WINDOW];
        
        latestConfirmedTick = 0;
        isResimulating = false;
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
            TriggerRoundEnd(playerTwo, playerOne);
        }
        else
        {
            TriggerRoundEnd(playerOne, playerTwo);
        }
    }

    private void TriggerRoundEnd(PlayerSessionContext winner, PlayerSessionContext loser)
    {        
        bool isWinnerValid = winner != null && winner.controller != null;
        if (isWinnerValid)
        {
            winner.controller.GetStateMachine().TransitionTo(PlayerState_Type.Win, true);
        }
    }

    private void Update()
    {
        bool isUpdateValid = isSimulationRunning && inputProvider != null && cameraManager != null;
        if (isUpdateValid)
        {
            Vector3 currentDepthAxis = cameraManager.GetDepthAxis();
            
            bool isP1Valid = playerOne.controller != null;
            if (isP1Valid)
            {
                playerOne.controller.GetPhysics().SetDepthAxis(currentDepthAxis);
            }
            
            bool isP2Valid = playerTwo.controller != null;
            if (isP2Valid)
            {
                playerTwo.controller.GetPhysics().SetDepthAxis(currentDepthAxis);
            }
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
    }

    private void ProcessOfflineTick()
    {
        PlayerInput p1Local = inputProvider.GetCurrentInput(currentTick, 0, !cameraManager.IsPlayerOneOnRightSide());
        PlayerInput p2Local = inputProvider.GetCurrentInput(currentTick, 1, cameraManager.IsPlayerOneOnRightSide());
        
        int bufferIndex = currentTick % ROLLBACK_WINDOW;
        p1InputBuffer[bufferIndex] = p1Local.flags;
        p2InputBuffer[bufferIndex] = p2Local.flags;

        RunTick(p1Local, p2Local);
        SaveGameState(currentTick);
        
        currentTick++;

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
        else          p2InputBuffer[bufferIndex] = localInput.flags;

        InputFlags predictedRemote = InputFlags.None;
        bool hasPreviousTick = currentTick > 0;
        if (hasPreviousTick)
        {
            int prevIndex = (currentTick - 1) % ROLLBACK_WINDOW;
            predictedRemote = isServer ? p2InputBuffer[prevIndex] : p1InputBuffer[prevIndex];
        }

        if (isServer) p2InputBuffer[bufferIndex] = predictedRemote;
        else          p1InputBuffer[bufferIndex] = predictedRemote;

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
                    else          p1InputBuffer[idx] = actualRemote;

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

        bool isRollbackNeeded = rollbackTick != -1;
        if (isRollbackNeeded)
        {
            Resimulate(rollbackTick, currentTick);
        }

        PlayerInput p1Final = new PlayerInput { flags = p1InputBuffer[bufferIndex] };
        PlayerInput p2Final = new PlayerInput { flags = p2InputBuffer[bufferIndex] };
        
        RunTick(p1Final, p2Final);
        SaveGameState(currentTick);
        
        currentTick++;
    }

    private void SaveGameState(int tick)
    {
        int index = tick % ROLLBACK_WINDOW;
        stateBuffer[index].tick = tick;
        
        bool isP1Valid = playerOne.controller != null;
        if (isP1Valid) playerOne.controller.ExportState(ref stateBuffer[index].p1Snapshot);
        
        bool isP2Valid = playerTwo.controller != null;
        if (isP2Valid) playerTwo.controller.ExportState(ref stateBuffer[index].p2Snapshot);
    }

    private void LoadGameState(int tick)
    {
        int index = tick % ROLLBACK_WINDOW;
        GameStateSnapshot snapshot = stateBuffer[index];

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

    private void RunTick(PlayerInput p1Input, PlayerInput p2Input)
    {
        bool isMatchEnded = isRoundOver;
        if (isMatchEnded)
        {
            p1Input.flags = InputFlags.None;
            p2Input.flags = InputFlags.None;
        }
        
        if (playerOne.controller != null) playerOne.controller.UpdateTick(p1Input);
        if (playerTwo.controller != null) playerTwo.controller.UpdateTick(p2Input);

        ResolveAttacks(playerOne, playerTwo);
        ResolveAttacks(playerTwo, playerOne);
        ResolvePlayerCollision();
        
        if (!isResimulating)
        {
            SyncVisuals();
        }
    }

    private void ResolveAttacks(PlayerSessionContext attackerContext, PlayerSessionContext defenderContext)
    {
        if (!IsValidAttackAttempt(attackerContext.controller, out ActionDataSO attackerAction)) return;

        CollisionBox[] defenderBoxes = defenderContext.controller.GetConfig().GetHurtboxBoxes(Hurtbox_Type.Standing);

        bool isHit = HitboxManager.EvaluateHit(
            attackerContext.controller.GetPosition(), attackerContext.controller.GetPhysics().GetLookDirection(), attackerAction.frameData.hitboxEvents, attackerContext.controller.GetStateMachine().GetStateFrameCounter(),
            defenderContext.controller.GetPosition(), defenderContext.controller.GetPhysics().GetLookDirection(), defenderBoxes,
            out HitboxEvent hitEvent, out Vector3 hitPoint, out string debugReason
        );

        if (isHit)
        {
            ProcessSuccessfulHit(attackerContext, defenderContext, hitEvent, hitPoint);
        }
    }

    private bool IsValidAttackAttempt(PlayerController attacker, out ActionDataSO actionData)
    {
        actionData = attacker.GetStateMachine().GetCurrentActionData();
        bool isAttacking = attacker.GetStateMachine().GetCurrentState() == PlayerState_Type.Attacking;
        bool hasValidData = actionData != null && actionData.frameData.hitboxEvents != null;
        
        return isAttacking && hasValidData;
    }

    private void ProcessSuccessfulHit(PlayerSessionContext attackerContext, PlayerSessionContext defenderContext, HitboxEvent hitEvent, Vector3 hitPoint)
    {
        PlayerController attacker = attackerContext.controller;
        PlayerController defender = defenderContext.controller;

        bool isAlreadyHit = attacker.GetCombat().HasAlreadyHit(hitEvent.hitGroupID);
        if (isAlreadyHit) return;

        attacker.GetCombat().RegisterHitGroup(hitEvent.hitGroupID);

        Vector3 worldPushback = CalculateWorldPushback(attacker.GetPhysics().GetLookDirection(), hitEvent.localPushbackVector);
        
        HitboxEvent worldSpaceHitEvent = hitEvent;
        worldSpaceHitEvent.localPushbackVector = worldPushback;
        
        EvaluationResult hitResult = defender.GetCombat().ProcessIncomingHit(worldSpaceHitEvent, defender);

        bool isHitEvaded = hitResult.isEvaded;
        if (isHitEvaded) return;

        int hitstopFrames = hitResult.feedbackData.hitstopFrames;
        if (hitstopFrames > 0)
        {
            attacker.GetCombat().ApplyHitstop(hitstopFrames);
            defender.GetCombat().ApplyHitstop(hitstopFrames);
        }

        bool isAttackBlocked = hitResult.targetState == PlayerState_Type.StandBlock || hitResult.targetState == PlayerState_Type.CrouchBlock;
        if (!isAttackBlocked && defenderContext.renderer != null && !isResimulating)
        {
            defenderContext.renderer.PlayHitSpark(hitPoint, EffectType.Hit);
        }
    }

    private Vector3 CalculateWorldPushback(Vector3 lookDirection, Vector3 localPushback)
    {
        Vector3 rightDirection = Vector3.Cross(Vector3.up, lookDirection);
        return (lookDirection * localPushback.z) + (Vector3.up * localPushback.y) + (rightDirection * localPushback.x);
    }

    private float GetPushbackWeight(PlayerState_Type state)
    {
        bool isSprinting = state == PlayerState_Type.Sprinting;
        bool isRunning = state == PlayerState_Type.Running;
        bool isWalking = state == PlayerState_Type.Walking;

        if (isSprinting) return 0.0f;
        if (isRunning) return 0.2f;
        if (isWalking) return 0.5f;
        return 1.0f;
    }

    private void ResolvePlayerCollision()
    {
        bool isInvalidControllers = playerOne.controller == null || playerTwo.controller == null;
        if (isInvalidControllers) return;

        Vector3 p1Pos = playerOne.controller.GetPosition();
        Vector3 p2Pos = playerTwo.controller.GetPosition();
        
        Vector3 diff = p1Pos - p2Pos;
        diff.y = 0;
        float distanceSqr = diff.sqrMagnitude;

        bool isOverlapping = distanceSqr < playerCollisionMinDistance * playerCollisionMinDistance && distanceSqr > 0.0001f;
        if (isOverlapping)
        {
            float distance = Mathf.Sqrt(distanceSqr);
            float totalPushDist = playerCollisionMinDistance - distance;
            Vector3 pushDir = diff / distance;

            PlayerState_Type p1State = playerOne.controller.GetStateMachine().GetCurrentState();
            PlayerState_Type p2State = playerTwo.controller.GetStateMachine().GetCurrentState();

            float w1 = GetPushbackWeight(p1State);
            float w2 = GetPushbackWeight(p2State);
            float totalWeight = w1 + w2;

            bool isWeightTooSmall = totalWeight <= 0.0001f;
            if (isWeightTooSmall)
            {
                w1 = 0.5f;
                w2 = 0.5f;
                totalWeight = 1.0f;
            }

            float p1Ratio = w1 / totalWeight;
            float p2Ratio = w2 / totalWeight;

            playerOne.controller.GetPhysics().ApplyPushback(pushDir * (totalPushDist * p1Ratio));
            playerTwo.controller.GetPhysics().ApplyPushback(-pushDir * (totalPushDist * p2Ratio));
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