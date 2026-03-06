using UnityEngine;

public abstract class HitStateBase : PlayerStateBase
{
    protected HurtInfo currentHurtInfo;

    public HitStateBase(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override void Enter()
    {
        currentHurtInfo = combat.GetCurrentHurtInfo();
        stateMachine.ClearCurrentAction();
    }
}

public class StandHitState : HitStateBase
{
    private int hitstunDuration;

    public StandHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.StandHit;

    public override void Enter()
    {
        base.Enter();
        hitstunDuration = currentHurtInfo.hurtStunFrames;
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isHitstunComplete = currentFrame >= hitstunDuration;

        if (isHitstunComplete)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
        }
    }
}

public class StunningState : HitStateBase
{
    private int stunningDuration;

    public StunningState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Stunning;

    public override void Enter()
    {
        base.Enter();
        stunningDuration = config.GetStunningFrames();

        bool isFromStand = physics.GetPosition().y <= 0f;
        if (isFromStand)
        {
            Vector3 horizontalPushback = currentHurtInfo.pushbackVector;
            horizontalPushback.y = 0f;
            physics.ApplyPushback(horizontalPushback);
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isStunningComplete = currentFrame >= stunningDuration;
        
        if (isStunningComplete)
        {
            stateMachine.TransitionTo(PlayerState_Type.LayingDown);
        }
    }
}

public class AirHitState : HitStateBase
{
    public AirHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.AirHit;

    public override void UpdateTick(PlayerInput input)
    {
        bool isFallingAndGrounded = physics.GetIsGrounded() && physics.GetVelocity().y <= 0f;

        if (isFallingAndGrounded)
        {
            stateMachine.TransitionTo(PlayerState_Type.GroundSmash);
        }
    }
}

public class GroundSmashState : HitStateBase
{
    private int smashDuration;
    private bool isBouncing;

    public GroundSmashState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.GroundSmash;

    public override void Enter()
    {
        base.Enter();
        
        float impactFallSpeed = physics.GetVelocity().y;
        isBouncing = impactFallSpeed <= config.GetBounceVelocityThreshold();

        if (isBouncing)
        {
            smashDuration = config.GetGroundSmashBounceFrames();
            Vector3 currentVelocity = physics.GetVelocity();
            currentVelocity.y = Mathf.Abs(impactFallSpeed) * config.GetBounceVelocityMultiplier();
            physics.SetVelocity(currentVelocity);
        }
        else
        {
            smashDuration = config.GetGroundSmashLayFrames();
            Vector3 zeroVelocity = Vector3.zero;
            zeroVelocity.y = physics.GetVelocity().y;
            physics.SetVelocity(zeroVelocity);
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isSmashComplete = currentFrame >= smashDuration;

        if (isSmashComplete)
        {
            if (isBouncing)
            {
                stateMachine.TransitionTo(PlayerState_Type.AirHit);
            }
            else
            {
                stateMachine.TransitionTo(PlayerState_Type.LayingDown);
            }
        }
    }
}

public class LayingDownState : HitStateBase
{
    private bool isFromRoll;

    public LayingDownState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.LayingDown;

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

public class WakeUpState : HitStateBase
{
    private WakeUp_Type scheduledWakeUpType;
    private int wakeUpDuration;

    public WakeUpState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.WakeUp;

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
        wakeUpDuration = config.GetWakeUpFrames(scheduledWakeUpType);
        
        Vector3 zeroVelocity = Vector3.zero;
        zeroVelocity.y = physics.GetVelocity().y;
        physics.SetVelocity(zeroVelocity);
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isActionComplete = currentFrame >= wakeUpDuration;

        if (isActionComplete)
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