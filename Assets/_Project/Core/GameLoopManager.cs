using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

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

public class GameLoopManager : MonoBehaviour
{
    [Header("Camera Manager")]
    [SerializeField] private CameraManager cameraManager;

    [Header("Global Settings")]
    [SerializeField] private float playerCollisionMinDistance = 1.0f;
    [SerializeField] private float globalGravity = 0.02f;

    [Header("Hit Feedback Settings")]
    [SerializeField] private List<HitFeedbackData> hitFeedbackTable = new();

    [Header("Players")]
    [SerializeField] private PlayerSessionContext playerOne;
    [SerializeField] private PlayerSessionContext playerTwo;

    [SerializeField] private Vector3 p1SpawnPos = new Vector3(-2, 0, 0);
    [SerializeField] private Vector3 p2SpawnPos = new Vector3(2, 0, 0);

    private LocalInputProvider inputProvider;
    private int currentTick;
    private bool isSimulationRunning;

    public int GetCurrentTick() => currentTick;
    public PlayerState_Type GetP1State() => playerOne.controller != null ? playerOne.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.controller != null ? playerOne.controller.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwo.controller != null ? playerTwo.controller.GetStateMachine().GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.controller != null ? playerTwo.controller.GetPosition() : Vector3.zero;
    public PlayerController GetPlayerOneController() => playerOne.controller;
    public PlayerController GetPlayerTwoController() => playerTwo.controller;

    private void Awake()
    {
        InitializePlayers();
        
        bool hasCameraManager = cameraManager != null;
        if(hasCameraManager)
        {
            cameraManager.SetTargetPlayers(playerOne.instance, playerTwo.instance);
        }
    }

    private void InitializePlayers()
    {
        InputBinding p1Final = playerOne.GetBinding(true);
        InputBinding p2Final = playerTwo.GetBinding(false);

        inputProvider = new LocalInputProvider(p1Final, p2Final);

        SetupPlayer(playerOne, p1SpawnPos);
        SetupPlayer(playerTwo, p2SpawnPos);

        bool hasBothControllers = playerOne.controller != null && playerTwo.controller != null;
        if (hasBothControllers)
        {
            playerOne.controller.SetTarget(playerTwo.controller);
            playerTwo.controller.SetTarget(playerOne.controller);
        }

        currentTick = 0;
        isSimulationRunning = true;
    }

    private void SetupPlayer(PlayerSessionContext context, Vector3 spawnPos)
    {
        bool isDataInvalid = context.characterData == null || context.characterData.characterPrefab == null;
        if (isDataInvalid) return;

        context.instance = Instantiate(context.characterData.characterPrefab, spawnPos, Quaternion.identity);
        context.renderer = context.instance.GetComponent<PlayerRenderer>();

        context.controller = new PlayerController();
        context.controller.Initialize(
            spawnPos, 
            context.characterData.config, 
            context.characterData.commandList, 
            context.characterData.comboTree
        );
        context.controller.GetPhysics().SetGlobalGravity(globalGravity);

        bool hasRenderer = context.renderer != null;
        if (hasRenderer)
        {
            context.renderer.InitializeRenderer(
                context.controller, 
                context.characterData.hitAnimMap, 
                context.characterData.effectTable
            );
        }
    }

    private void Update()
    {
        bool isUpdateValid = isSimulationRunning && inputProvider != null && cameraManager != null;
        if (isUpdateValid)
        {
            bool isP1OnRight = cameraManager.IsPlayerOneOnRightSide();
            bool isP1FacingRight = !isP1OnRight;
            bool isP2FacingRight = isP1OnRight;

            inputProvider.AccumulateInputFlags(isP1FacingRight, isP2FacingRight);

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

        bool hasKeyboard = Keyboard.current != null;
        bool isDebugAttackPressed = hasKeyboard && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (isDebugAttackPressed)
        {
            DebugDealDamageToBoth();
        }
    }

    private void FixedUpdate()
    {
        if (isSimulationRunning)
        {
            RunTick();
        }
    }
    
    private void RunTick()
    {
        bool isPlayerOneValid = playerOne.controller != null;
        if (isPlayerOneValid)
        {
            PlayerInput p1Input = inputProvider.GetCurrentInput(currentTick, 0);
            playerOne.controller.UpdateTick(p1Input);
        }

        bool isPlayerTwoValid = playerTwo.controller != null;
        if (isPlayerTwoValid)
        {
            PlayerInput p2Input = inputProvider.GetCurrentInput(currentTick, 1);
            playerTwo.controller.UpdateTick(p2Input);
        }

        ResolveAttacks(playerOne, playerTwo);
        ResolveAttacks(playerTwo, playerOne);

        ResolvePlayerCollision();

        SyncVisuals();

        currentTick++;
    }

    public void DebugDealDamageToBoth()
    {
        bool isInvalidControllers = playerOne.controller == null || playerTwo.controller == null;
        if (isInvalidControllers) return;

        HurtInfo debugHurt = new HurtInfo 
        { 
            damage = 1, 
            hurtStunFrames = 20,
            pushbackVector = Vector3.zero,
            targetHurtState = HurtState_Type.StandHit,
            isHardKnockdown = false
        };

        playerOne.controller.GetCombat().ApplyHit(debugHurt, playerOne.controller);
        playerTwo.controller.GetCombat().ApplyHit(debugHurt, playerTwo.controller);
    }

    private HitFeedbackData GetHitFeedback(Attack_Type type)
    {
        foreach (var feedback in hitFeedbackTable)
        {
            bool isMatchingType = feedback.attackType == type;
            if (isMatchingType)
            {
                return feedback;
            }
        }
        return new HitFeedbackData { attackType = type, hitstopFrames = 0, cameraShakeIntensity = 0f };
    }

    private void ResolveAttacks(PlayerSessionContext attackerContext, PlayerSessionContext defenderContext)
    {
        PlayerController attacker = attackerContext.controller;
        PlayerController defender = defenderContext.controller;

        bool isNotAttacking = attacker.GetStateMachine().GetCurrentState() != PlayerState_Type.Attacking;
        if (isNotAttacking) return;
        
        ActionDataSO attackerAction = attacker.GetStateMachine().GetCurrentActionData();
        bool isInvalidAction = attackerAction == null || attackerAction.frameData.hitboxEvents == null;
        if (isInvalidAction) return;

        Hurtbox_Type defenderHurtboxType = Hurtbox_Type.Standing;
        CollisionBox[] defenderBoxes = defender.GetConfig().GetHurtboxBoxes(defenderHurtboxType);

        bool isHit = HitboxManager.EvaluateHit(
            attacker.GetPosition(), attacker.GetPhysics().GetLookDirection(), attackerAction.frameData.hitboxEvents, attacker.GetStateMachine().GetStateFrameCounter(),
            defender.GetPosition(), defender.GetPhysics().GetLookDirection(), defenderBoxes,
            out HitboxEvent hitEvent, out Vector3 hitPoint, out string debugReason
        );

        if (!isHit) return;

        bool isNewHit = !attacker.GetCombat().HasAlreadyHit(hitEvent.hitGroupID);
        if (isNewHit)
        {
            attacker.GetCombat().RegisterHitGroup(hitEvent.hitGroupID);
            
            Vector3 attackerLookDirection = attacker.GetPhysics().GetLookDirection();
            Vector3 rightDirection = Vector3.Cross(Vector3.up, attackerLookDirection);
            
            Vector3 worldPushback = (attackerLookDirection * hitEvent.localPushbackVector.z) + 
                                    (Vector3.up * hitEvent.localPushbackVector.y) + 
                                    (rightDirection * hitEvent.localPushbackVector.x);
            
            HurtInfo hurtInfo = new HurtInfo 
            { 
                damage = hitEvent.damage, 
                hurtStunFrames = hitEvent.hitstunFrames,
                pushbackVector = worldPushback,
                targetHurtState = hitEvent.targetHurtState,
                isHardKnockdown = hitEvent.isHardKnockdown
            };
            
            defender.GetCombat().ApplyHit(hurtInfo, defender);

            HitFeedbackData feedback = GetHitFeedback(hitEvent.attackType);
            
            bool hasHitstop = feedback.hitstopFrames > 0;
            if (hasHitstop)
            {
                attacker.GetCombat().ApplyHitstop(feedback.hitstopFrames);
                defender.GetCombat().ApplyHitstop(feedback.hitstopFrames);
            }

            bool hasDefenderRenderer = defenderContext.renderer != null;
            if (hasDefenderRenderer)
            {
                defenderContext.renderer.PlayHitSpark(hitPoint, EffectType.Hit);
            }
        }
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