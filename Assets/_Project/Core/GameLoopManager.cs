using UnityEngine;
using UnityEngine.InputSystem;

public class GameLoopManager : MonoBehaviour
{
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

        HitInfo debugHit = new HitInfo 
        { 
            damage = 1, 
            hitstunFrames = 20,
            pushbackVector = Vector3.zero,
            hitType = HitState_Type.StandHit,
            isHardKnockdown = false
        };

        playerOneStateMachine.ApplyHit(debugHit);
        playerTwoStateMachine.ApplyHit(debugHit);

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
            attacker.GetPosition(), attacker.GetDirection(), attackerAction.frameData.hitboxEvents, attacker.GetStateFrameCounter(),
            defender.GetPosition(), defender.GetDirection(), defenderBoxes,
            out HitboxEvent hitEvent
        );

        if (isHit && !attacker.HasAlreadyHit(hitEvent.hitGroupID))
        {
            attacker.RegisterHitGroup(hitEvent.hitGroupID);
            
            HitInfo hitInfo = new HitInfo 
            { 
                damage = hitEvent.damage, 
                hitstunFrames = hitEvent.damage * 2,
                pushbackVector = attacker.GetDirection() * 2f,
                hitType = HitState_Type.StandHit,
                isHardKnockdown = false
            };
            
            defender.ApplyHit(hitInfo);

            string attackerName = attacker == playerOneStateMachine ? "Player 1" : "Player 2";
            string defenderName = defender == playerOneStateMachine ? "Player 1" : "Player 2";
            Debug.Log($"[Hit] {attackerName} -> {defenderName} | Damage: {hitEvent.damage}");
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
        float minDistance = 1.0f;

        if (distanceSqr < minDistance * minDistance && distanceSqr > 0.0001f)
        {
            float distance = Mathf.Sqrt(distanceSqr);
            float totalPushDist = minDistance - distance;
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
        if (playerOneVisual != null && playerOneStateMachine != null)
        {
            bool triggerCombo = playerOneStateMachine.GetCurrentState() == PlayerState_Type.Attacking && playerOneStateMachine.GetStateFrameCounter() == 1;
            
            int comboHash = 0;
            if (triggerCombo)
            {
                comboHash = playerOneStateMachine.GetCurrentAttackTriggerHash();
                if (comboHash == 0)
                {
                    triggerCombo = false;
                }
            }

            bool triggerCommand = playerOneStateMachine.CheckAndConsumeCommandAction(out int commandHash);

            bool triggerHit = playerOneStateMachine.GetCurrentState() == PlayerState_Type.Hit && playerOneStateMachine.GetStateFrameCounter() == 1;
            int hitHash = 0;
            if (triggerHit)
            {
                hitHash = playerOneStateMachine.GetAnimationHash(playerOneStateMachine.GetCurrentHitInfo().hitType.ToString());
            }

            bool finalTrigger = triggerCombo || triggerCommand || triggerHit;
            int finalHash = triggerCommand ? commandHash : comboHash;

            playerOneVisual.SyncWithLogic(
                playerOneStateMachine.GetPosition(), 
                playerOneStateMachine.GetCurrentState(), 
                playerOneStateMachine.GetCurrentSpeed(),
                playerOneStateMachine.GetDirection(),
                playerOneStateMachine.GetLookDirection(),
                finalTrigger,
                finalHash
            );
        }

        if (playerTwoVisual != null && playerTwoStateMachine != null)
        {
            bool triggerCombo = playerTwoStateMachine.GetCurrentState() == PlayerState_Type.Attacking && playerTwoStateMachine.GetStateFrameCounter() == 1;
            
            int comboHash = 0;
            if (triggerCombo)
            {
                comboHash = playerTwoStateMachine.GetCurrentAttackTriggerHash();
                if (comboHash == 0)
                {
                    triggerCombo = false;
                }
            }

            bool triggerCommand = playerTwoStateMachine.CheckAndConsumeCommandAction(out int commandHash);

            bool triggerHit = playerTwoStateMachine.GetCurrentState() == PlayerState_Type.Hit && playerTwoStateMachine.GetStateFrameCounter() == 1;
            int hitHash = 0;
            if (triggerHit)
            {
                hitHash = playerTwoStateMachine.GetAnimationHash(playerTwoStateMachine.GetCurrentHitInfo().hitType.ToString());
            }

            bool finalTrigger = triggerCombo || triggerCommand || triggerHit;
            int finalHash = triggerCommand ? commandHash : comboHash;

            playerTwoVisual.SyncWithLogic(
                playerTwoStateMachine.GetPosition(), 
                playerTwoStateMachine.GetCurrentState(), 
                playerTwoStateMachine.GetCurrentSpeed(),
                playerTwoStateMachine.GetDirection(),
                playerTwoStateMachine.GetLookDirection(),
                finalTrigger,
                finalHash
            );
        }
    }
}