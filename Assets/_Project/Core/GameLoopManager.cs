using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class PlayerSessionContext
{
    public CharacterVisual visual;
    public PlayerConfigSO config;
    public CommandListSO commandList;
    public ComboTreeSO comboTree;
    public HitAnimationMapSO hitAnimMap;
    [HideInInspector] public PlayerStateMachine stateMachine;
}

public class GameLoopManager : MonoBehaviour
{
    [Header("Global Settings")]
    [SerializeField] private float playerCollisionMinDistance = 1.0f;
    [SerializeField] private float globalGravity = 0.02f;

    [Header("Players")]
    [SerializeField] private PlayerSessionContext playerOne;
    [SerializeField] private PlayerSessionContext playerTwo;

    private LocalInputProvider inputProvider;
    private int currentTick;
    private bool isSimulationRunning;

    public int GetCurrentTick() => currentTick;
    public PlayerState_Type GetP1State() => playerOne.stateMachine != null ? playerOne.stateMachine.GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOne.stateMachine != null ? playerOne.stateMachine.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwo.stateMachine != null ? playerTwo.stateMachine.GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwo.stateMachine != null ? playerTwo.stateMachine.GetPosition() : Vector3.zero;
    public PlayerStateMachine GetPlayerOneStateMachine() => playerOne.stateMachine;
    public PlayerStateMachine GetPlayerTwoStateMachine() => playerTwo.stateMachine;


    private void Awake()
    {
        InitializePlayers();
    }


    private void InitializePlayers()
    {
        inputProvider = new LocalInputProvider();

        bool hasPlayerOneVisual = playerOne.visual != null;
        if (hasPlayerOneVisual)
        {
            playerOne.stateMachine = new PlayerStateMachine();
            playerOne.stateMachine.Initialize(new Vector3(-2, 0, 0), playerOne.config, playerOne.commandList, playerOne.comboTree);
            playerOne.stateMachine.SetGlobalGravity(globalGravity);
            
            playerOne.visual.InitializeVisual(playerOne.stateMachine, playerOne.hitAnimMap);
        }

        bool hasPlayerTwoVisual = playerTwo.visual != null;
        if (hasPlayerTwoVisual)
        {
            playerTwo.stateMachine = new PlayerStateMachine();
            playerTwo.stateMachine.Initialize(new Vector3(2, 0, 0), playerTwo.config, playerTwo.commandList, playerTwo.comboTree);
            playerTwo.stateMachine.SetGlobalGravity(globalGravity);
            
            playerTwo.visual.InitializeVisual(playerTwo.stateMachine, playerTwo.hitAnimMap);
        }

        bool hasBothStateMachines = playerOne.stateMachine != null && playerTwo.stateMachine != null;
        if (hasBothStateMachines)
        {
            playerOne.stateMachine.SetTarget(playerTwo.stateMachine);
            playerTwo.stateMachine.SetTarget(playerOne.stateMachine);
        }

        currentTick = 0;
        isSimulationRunning = true;
    }


    private void Update()
    {
        if (isSimulationRunning && inputProvider != null)
        {
            inputProvider.AccumulateInputFlags();
        }

        bool isDebugAttackPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
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
        bool isPlayerOneValid = playerOne.stateMachine != null;
        if (isPlayerOneValid)
        {
            PlayerInput p1Input = inputProvider.GetCurrentInput(currentTick, 0);
            playerOne.stateMachine.UpdateTick(p1Input);
        }

        bool isPlayerTwoValid = playerTwo.stateMachine != null;
        if (isPlayerTwoValid)
        {
            PlayerInput p2Input = inputProvider.GetCurrentInput(currentTick, 1);
            playerTwo.stateMachine.UpdateTick(p2Input);
        }

        ResolveAttacks(playerOne.stateMachine, playerTwo.stateMachine);
        ResolveAttacks(playerTwo.stateMachine, playerOne.stateMachine);

        ResolvePlayerCollision();

        SyncVisuals();

        currentTick++;
    }


    public void DebugDealDamageToBoth()
    {
        bool isInvalidMachines = playerOne.stateMachine == null || playerTwo.stateMachine == null;
        if (isInvalidMachines) return;

        HurtInfo debugHurt = new HurtInfo 
        { 
            damage = 1, 
            hurtStunFrames = 20,
            pushbackVector = Vector3.zero,
            targetHurtState = HurtState_Type.StandHit,
            isHardKnockdown = false
        };

        playerOne.stateMachine.ApplyHit(debugHurt);
        playerTwo.stateMachine.ApplyHit(debugHurt);

        Debug.Log("[Debug] 양 플레이어에게 1 데미지를 가했습니다.");
    }


    private void ResolveAttacks(PlayerStateMachine attacker, PlayerStateMachine defender)
    {
        bool isNotAttacking = attacker.GetCurrentState() != PlayerState_Type.Attacking;
        if (isNotAttacking) return;
        
        ActionDataSO attackerAction = attacker.GetCurrentActionData();
        bool isInvalidAction = attackerAction == null || attackerAction.frameData.hitboxEvents == null;
        if (isInvalidAction) return;

        Hurtbox_Type defenderHurtboxType = defender.GetCurrentHurtboxType();
        CollisionBox[] defenderBoxes = defender.GetPlayerConfig().GetHurtboxBoxes(defenderHurtboxType);

        bool isHit = HitboxManager.EvaluateHit(
            attacker.GetPosition(), attacker.GetLookDirection(), attackerAction.frameData.hitboxEvents, attacker.GetStateFrameCounter(),
            defender.GetPosition(), defender.GetLookDirection(), defenderBoxes,
            out HitboxEvent hitEvent, out string debugReason
        );

        if (!isHit) return;

        bool isNewHit = !attacker.HasAlreadyHit(hitEvent.hitGroupID);
        if (isNewHit)
        {
            attacker.RegisterHitGroup(hitEvent.hitGroupID);
            
            Vector3 attackerLookDirection = attacker.GetLookDirection();
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
            
            defender.ApplyHit(hurtInfo);

            string attackerName = attacker == playerOne.stateMachine ? "Player 1" : "Player 2";
            string defenderName = defender == playerOne.stateMachine ? "Player 1" : "Player 2";
            Debug.Log($"[Hit 성공] {attackerName} -> {defenderName} | Damage: {hitEvent.damage} | Pushback: {worldPushback}");
        }
    }
    

    private float GetPushbackWeight(PlayerState_Type state)
    {
        switch (state)
        {
            case PlayerState_Type.Sprinting: return 0.0f;
            case PlayerState_Type.Running: return 0.2f;
            case PlayerState_Type.Walking: return 0.5f;
            case PlayerState_Type.Idle: return 1.0f;
            case PlayerState_Type.Stun: return 1.5f;
            default: return 1.0f;
        }
    }


    private void ResolvePlayerCollision()
    {
        bool isInvalidMachines = playerOne.stateMachine == null || playerTwo.stateMachine == null;
        if (isInvalidMachines) return;

        Vector3 p1Pos = playerOne.stateMachine.GetPosition();
        Vector3 p2Pos = playerTwo.stateMachine.GetPosition();
        
        Vector3 diff = p1Pos - p2Pos;
        diff.y = 0;
        float distanceSqr = diff.sqrMagnitude;

        bool isOverlapping = distanceSqr < playerCollisionMinDistance * playerCollisionMinDistance && distanceSqr > 0.0001f;
        if (isOverlapping)
        {
            float distance = Mathf.Sqrt(distanceSqr);
            float totalPushDist = playerCollisionMinDistance - distance;
            Vector3 pushDir = diff / distance;

            PlayerState_Type p1State = playerOne.stateMachine.GetCurrentState();
            PlayerState_Type p2State = playerTwo.stateMachine.GetCurrentState();

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

            playerOne.stateMachine.ApplyPushback(pushDir * (totalPushDist * p1Ratio));
            playerTwo.stateMachine.ApplyPushback(-pushDir * (totalPushDist * p2Ratio));
        }
    }


    private void SyncVisuals()
    {
        UpdatePlayerVisual(playerOne);
        UpdatePlayerVisual(playerTwo);
    }


    private void UpdatePlayerVisual(PlayerSessionContext context)
    {
        bool isInvalidContext = context == null || context.stateMachine == null || context.visual == null;
        if (isInvalidContext) return;

        context.visual.SyncTransformWithLogic();
        context.visual.EvaluateAndPlayAnimation();
    }
}