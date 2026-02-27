using UnityEngine;
using UnityEngine.InputSystem;

public class GameLoopManager : MonoBehaviour
{
    [Header("Global Settings")]
    [SerializeField] private float playerCollisionMinDistance = 1.0f;

    [Header("Player 1 Settings")]
    [SerializeField] private CharacterVisual playerOneVisual;
    [SerializeField] private PlayerConfigSO playerOneConfig;
    [SerializeField] private CommandListSO playerOneCommandList;
    [SerializeField] private ComboTreeSO playerOneComboTree;

    [Header("Player 2 Settings")]
    [SerializeField] private CharacterVisual playerTwoVisual;
    [SerializeField] private PlayerConfigSO playerTwoConfig;
    [SerializeField] private CommandListSO playerTwoCommandList;
    [SerializeField] private ComboTreeSO playerTwoComboTree;

    private LocalInputProvider inputProvider;
    private PlayerStateMachine playerOneStateMachine;
    private PlayerStateMachine playerTwoStateMachine;
    
    private int currentTick;
    private bool isSimulationRunning;

    public int GetCurrentTick() => currentTick;
    public PlayerState_Type GetP1State() => playerOneStateMachine != null ? playerOneStateMachine.GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP1Pos() => playerOneStateMachine != null ? playerOneStateMachine.GetPosition() : Vector3.zero;
    public PlayerState_Type GetP2State() => playerTwoStateMachine != null ? playerTwoStateMachine.GetCurrentState() : PlayerState_Type.Idle;
    public Vector3 GetP2Pos() => playerTwoStateMachine != null ? playerTwoStateMachine.GetPosition() : Vector3.zero;
    public PlayerStateMachine GetPlayerOneStateMachine() => playerOneStateMachine;
    public PlayerStateMachine GetPlayerTwoStateMachine() => playerTwoStateMachine;

    private void Awake()
    {
        InitializePlayers();
    }

    private void InitializePlayers()
    {
        inputProvider = new LocalInputProvider();

        if (playerOneVisual != null)
        {
            playerOneStateMachine = new PlayerStateMachine();
            playerOneStateMachine.Initialize(
                new Vector3(-2, 0, 0), 
                playerOneConfig, 
                playerOneCommandList, 
                playerOneComboTree
                );
        }

        if (playerTwoVisual != null)
        {
            playerTwoStateMachine = new PlayerStateMachine();
            playerTwoStateMachine.Initialize(
                new Vector3(2, 0, 0), 
                playerTwoConfig, 
                playerTwoCommandList, 
                playerTwoComboTree
                );
        }

        if (playerOneStateMachine != null)
        {
            playerOneStateMachine.SetTarget(playerTwoStateMachine);
        }

        if (playerTwoStateMachine != null)
        {
            playerTwoStateMachine.SetTarget(playerOneStateMachine);
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

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
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
        if (playerOneStateMachine != null)
        {
            PlayerInput p1Input = inputProvider.GetCurrentInput(currentTick, 0);
            playerOneStateMachine.UpdateTick(p1Input);
        }

        if (playerTwoStateMachine != null)
        {
            PlayerInput p2Input = inputProvider.GetCurrentInput(currentTick, 1);
            playerTwoStateMachine.UpdateTick(p2Input);
        }

        ResolveAttacks(playerOneStateMachine, playerTwoStateMachine);
        ResolveAttacks(playerTwoStateMachine, playerOneStateMachine);

        ResolvePlayerCollision();

        SyncVisuals();

        currentTick++;
    }

    public void DebugDealDamageToBoth()
    {
        if (playerOneStateMachine == null || playerTwoStateMachine == null) return;

        HurtInfo debugHurt = new HurtInfo 
        { 
            damage = 1, 
            hurtStunFrames = 20,
            pushbackVector = Vector3.zero,
            targetHurtState = HurtState_Type.StandHit,
            isHardKnockdown = false
        };

        playerOneStateMachine.ApplyHit(debugHurt);
        playerTwoStateMachine.ApplyHit(debugHurt);

        Debug.Log("[Debug] 양 플레이어에게 1 데미지를 가했습니다.");
    }

    private void ResolveAttacks(PlayerStateMachine attacker, PlayerStateMachine defender)
    {
        if (attacker.GetCurrentState() != PlayerState_Type.Attacking) return;
        
        ActionDataSO attackerAction = attacker.GetCurrentActionData();
        if (attackerAction == null || attackerAction.frameData.hitboxEvents == null) return;

        Hurtbox_Type defenderHurtboxType = defender.GetCurrentHurtboxType();
        CollisionBox[] defenderBoxes = defender.GetPlayerConfig().GetHurtboxBoxes(defenderHurtboxType);

        bool isHit = HitboxManager.EvaluateHit(
            attacker.GetPosition(), attacker.GetLookDirection(), attackerAction.frameData.hitboxEvents, attacker.GetStateFrameCounter(),
            defender.GetPosition(), defender.GetLookDirection(), defenderBoxes,
            out HitboxEvent hitEvent, out string debugReason
        );

        if (!isHit)
        {
            // if (!debugReason.Contains("현재 프레임"))
            // {
            //     string logAttackerName = attacker == playerOneStateMachine ? "Player 1" : "Player 2";
            //     Debug.LogWarning($"[Hit 판정 실패] {logAttackerName}의 공격 실패 사유: {debugReason} | Defender State: {defender.GetCurrentState()}");
            // }
            return;
        }

        if (isHit && !attacker.HasAlreadyHit(hitEvent.hitGroupID))
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

            string attackerName = attacker == playerOneStateMachine ? "Player 1" : "Player 2";
            string defenderName = defender == playerOneStateMachine ? "Player 1" : "Player 2";
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
        if (playerOneStateMachine == null || playerTwoStateMachine == null) return;

        Vector3 p1Pos = playerOneStateMachine.GetPosition();
        Vector3 p2Pos = playerTwoStateMachine.GetPosition();
        
        Vector3 diff = p1Pos - p2Pos;
        diff.y = 0;
        float distanceSqr = diff.sqrMagnitude;

        if (distanceSqr < playerCollisionMinDistance * playerCollisionMinDistance && distanceSqr > 0.0001f)
        {
            float distance = Mathf.Sqrt(distanceSqr);
            float totalPushDist = playerCollisionMinDistance - distance;
            Vector3 pushDir = diff / distance;

            PlayerState_Type p1State = playerOneStateMachine.GetCurrentState();
            PlayerState_Type p2State = playerTwoStateMachine.GetCurrentState();

            float w1 = GetPushbackWeight(p1State);
            float w2 = GetPushbackWeight(p2State);
            float totalWeight = w1 + w2;

            if (totalWeight <= 0.0001f)
            {
                w1 = 0.5f;
                w2 = 0.5f;
                totalWeight = 1.0f;
            }

            float p1Ratio = w1 / totalWeight;
            float p2Ratio = w2 / totalWeight;

            playerOneStateMachine.ApplyPushback(pushDir * (totalPushDist * p1Ratio));
            playerTwoStateMachine.ApplyPushback(-pushDir * (totalPushDist * p2Ratio));
        }
    }

    private void SyncVisuals()
    {
        UpdatePlayerVisual(playerOneStateMachine, playerOneVisual);
        UpdatePlayerVisual(playerTwoStateMachine, playerTwoVisual);
    }

    private void UpdatePlayerVisual(PlayerStateMachine sm, CharacterVisual visual)
    {
        if (sm == null || visual == null) return;

        int finalHash = 0;
        bool isFinalTrigger = false;
        int stateFrame = sm.GetStateFrameCounter();
        PlayerState_Type currentState = sm.GetCurrentState();

        if (currentState == PlayerState_Type.Hit && stateFrame == 1)
        {
            finalHash = sm.GetAnimationHash(sm.GetCurrentHurtInfo().targetHurtState.ToString());
            isFinalTrigger = true;
        }
        else if (sm.CheckAndConsumeCommandAction(out int commandHash))
        {
            finalHash = commandHash;
            isFinalTrigger = true;
        }
        else if (currentState == PlayerState_Type.Attacking && stateFrame == 1)
        {
            finalHash = sm.GetCurrentAttackTriggerHash();
            if (finalHash != 0) isFinalTrigger = true;
        }

        visual.SyncWithLogic(
            sm.GetPosition(), 
            currentState, 
            sm.GetCurrentSpeed(),
            sm.GetDirection(),
            sm.GetLookDirection(),
            isFinalTrigger,
            finalHash
        );
    }
}