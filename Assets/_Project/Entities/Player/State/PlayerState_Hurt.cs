using UnityEngine;

public abstract class HurtStateBase : PlayerStateBase
{
    protected HurtInfo currentHurtInfo;
    protected int currentStunFrames;

    public HurtStateBase(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override void Enter()
    {
        Debug.Log($"[HurtState] Entered State: {GetStateType()}");
        currentHurtInfo = combat.GetCurrentHurtInfo();
        currentStunFrames = currentHurtInfo.hurtStunFrames;
        stateMachine.ClearCurrentAction();
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick())
        {
            return;
        }

        currentStunFrames--;

        if (currentStunFrames <= 0)
        {
            stateMachine.TransitionTo(GetRecoveryState(), true);
        }
    }

    protected abstract PlayerState_Type GetRecoveryState();
}

public class StandHitState : HurtStateBase
{
    public StandHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.StandHit;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Idle;
}

public class CrouchHitState : HurtStateBase
{
    public CrouchHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.CrouchHit;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Crouching;
}

public class StandBlockState : HurtStateBase
{
    public StandBlockState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.StandBlock;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Idle;
}

public class CrouchBlockState : HurtStateBase
{
    public CrouchBlockState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.CrouchBlock;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Crouching;
}

public class StunningState : HurtStateBase
{
    public StunningState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Stunning;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.LayingDown;

    public override void Enter()
    {
        base.Enter();
        
        currentStunFrames = config.GetStunningFrames();

        bool isFromStand = physics.GetPosition().y <= 0f;
        if (isFromStand)
        {
            Vector3 horizontalPushback = currentHurtInfo.pushbackVector;
            horizontalPushback.y = 0f;
            physics.ApplyPushback(horizontalPushback);
        }
    }
}

public class AirHitState : HurtStateBase
{
    public AirHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.AirHit;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.None;

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick()) return;

        bool isFallingAndGrounded = physics.GetIsGrounded() && physics.GetVelocity().y <= 0f;

        if (isFallingAndGrounded)
        {
            stateMachine.TransitionTo(PlayerState_Type.GroundSmash);
        }
    }
}

public class GroundSmashState : HurtStateBase
{
    private bool isBouncing;

    public GroundSmashState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.GroundSmash;

    protected override PlayerState_Type GetRecoveryState() => isBouncing ? PlayerState_Type.AirHit : PlayerState_Type.LayingDown;

    public override void Enter()
    {
        base.Enter();
        
        float impactFallSpeed = physics.GetVelocity().y;
        isBouncing = impactFallSpeed <= config.GetBounceVelocityThreshold();

        if (isBouncing)
        {
            currentStunFrames = config.GetGroundSmashBounceFrames();
            Vector3 currentVelocity = physics.GetVelocity();
            currentVelocity.y = Mathf.Abs(impactFallSpeed) * config.GetBounceVelocityMultiplier();
            physics.SetVelocity(currentVelocity);
        }
        else
        {
            currentStunFrames = config.GetGroundSmashLayFrames();
            Vector3 zeroVelocity = Vector3.zero;
            zeroVelocity.y = physics.GetVelocity().y;
            physics.SetVelocity(zeroVelocity);
        }
    }
}

public class LayingDownState : HurtStateBase
{
    private bool isFromRoll;

    public LayingDownState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.LayingDown;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.None;

    public void SetFromRoll(bool value)
    {
        isFromRoll = value;
    }

    public bool IsFromRoll()
    {
        return isFromRoll;
    }

    public override void Enter()
    {
        base.Enter();
        Vector3 zeroVelocity = Vector3.zero;
        zeroVelocity.y = physics.GetVelocity().y;
        physics.SetVelocity(zeroVelocity);
    }

    public override void Exit()
    {
        base.Exit();
        isFromRoll = false;
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick()) return;

        bool isDirectionalInputDetected = (input.flags & (InputFlags.Forward | InputFlags.Back | InputFlags.Up | InputFlags.Down)) != 0;

        if (isDirectionalInputDetected)
        {
            actionController.ClearAllBuffers();

            WakeUp_Type selectedType = EvaluateWakeUpInput(input);
            WakeUpState wakeUpState = stateMachine.GetStateObject(PlayerState_Type.WakeUp) as WakeUpState;
            
            bool isWakeUpStateValid = wakeUpState != null;
            if (isWakeUpStateValid)
            {
                wakeUpState.SetWakeUpType(selectedType);
            }
            
            stateMachine.TransitionTo(PlayerState_Type.WakeUp);
        }
    }

    private WakeUp_Type EvaluateWakeUpInput(PlayerInput input)
    {
        bool isLeftPressed = (input.flags & InputFlags.Up) != 0;
        if (isLeftPressed) return WakeUp_Type.RollLeft;

        bool isRightPressed = (input.flags & InputFlags.Down) != 0;
        if (isRightPressed) return WakeUp_Type.RollRight;

        bool isForwardPressed = (input.flags & InputFlags.Forward) != 0;
        if (isForwardPressed) return WakeUp_Type.RollForward;

        bool isBackwardPressed = (input.flags & InputFlags.Back) != 0;
        if (isBackwardPressed) return WakeUp_Type.RollBackward;

        return WakeUp_Type.InPlace;
    }
}

public class WakeUpState : HurtStateBase
{
    private WakeUp_Type scheduledWakeUpType;

    public WakeUpState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.WakeUp;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.None;

    public void SetWakeUpType(WakeUp_Type type)
    {
        scheduledWakeUpType = type;
    }

    public WakeUp_Type GetScheduledWakeUpType()
    {
        return scheduledWakeUpType;
    }

    public override void Enter()
    {
        base.Enter();
        currentStunFrames = config.GetWakeUpFrames(scheduledWakeUpType);
        
        Vector3 zeroVelocity = Vector3.zero;
        zeroVelocity.y = physics.GetVelocity().y;
        physics.SetVelocity(zeroVelocity);
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick()) return;

        currentStunFrames--;

        if (currentStunFrames <= 0)
        {
            bool isGroundRoll = scheduledWakeUpType == WakeUp_Type.RollLeft || scheduledWakeUpType == WakeUp_Type.RollRight;
            
            if (isGroundRoll)
            {
                LayingDownState layState = stateMachine.GetStateObject(PlayerState_Type.LayingDown) as LayingDownState;
                bool isLayStateValid = layState != null;
                if (isLayStateValid)
                {
                    layState.SetFromRoll(true);
                }
                
                stateMachine.TransitionTo(PlayerState_Type.LayingDown);
            }
            else
            {
                stateMachine.TransitionTo(PlayerState_Type.Idle);
            }
        }
    }
}