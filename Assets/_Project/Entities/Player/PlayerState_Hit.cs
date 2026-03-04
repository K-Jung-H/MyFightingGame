using UnityEngine;

public abstract class HitStateBase : PlayerStateBase
{
    protected HurtInfo currentHurtInfo;

    public HitStateBase(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override void Enter()
    {
        currentHurtInfo = stateMachine.GetCurrentHurtInfo();
        
        stateMachine.ClearComboSequence();
        stateMachine.ClearCurrentAction();
        stateMachine.ClearInputBuffer();
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

        bool isFromStand = stateMachine.GetPosition().y <= 0f;
        if (isFromStand)
        {
            Vector3 horizontalPushback = currentHurtInfo.pushbackVector;
            horizontalPushback.y = 0f;
            stateMachine.ApplyPushback(horizontalPushback);
        }

        Debug.Log("Enter Stun");
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isStunningComplete = currentFrame >= stunningDuration;
        
        if (isStunningComplete)
        {
            stateMachine.TransitionTo(PlayerState_Type.LayingDown);
            Debug.Log("Exit Stun");

        }
    }
}

public class AirHitState : HitStateBase
{
    public AirHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.AirHit;

    public override void Enter()
    {
        base.Enter();
    }

    public override void UpdateTick(PlayerInput input)
    {
        bool isGrounded = stateMachine.GetPosition().y <= 0f && stateMachine.GetVelocity().y <= 0f;

        if (isGrounded)
        {
            Vector3 currentPos = stateMachine.GetPosition();
            currentPos.y = 0f;
            stateMachine.SetPosition(currentPos);
            
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

        float currentFallSpeed = stateMachine.GetVelocity().y;
        isBouncing = currentFallSpeed <= config.GetBounceVelocityThreshold();

        if (isBouncing)
        {
            smashDuration = config.GetGroundSmashBounceFrames();
            Vector3 currentVelocity = stateMachine.GetVelocity();
            currentVelocity.y = Mathf.Abs(currentFallSpeed) * config.GetBounceVelocityMultiplier();
            stateMachine.SetVelocity(currentVelocity);
        }
        else
        {
            smashDuration = config.GetGroundSmashLayFrames();
            stateMachine.SetVelocity(Vector3.zero);
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
    public LayingDownState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.LayingDown;

    public override void Enter()
    {
        base.Enter();

        stateMachine.SetVelocity(Vector3.zero);
    }

    public override void UpdateTick(PlayerInput input)
    {
        bool isInputDetected = input.flags != 0;
        
        if (isInputDetected)
        {
            WakeUp_Type selectedType = EvaluateWakeUpInput(input);
            stateMachine.SetWakeUpType(selectedType);
            stateMachine.TransitionTo(PlayerState_Type.WakeUp);
        }
    }

    private WakeUp_Type EvaluateWakeUpInput(PlayerInput input)
    {
        bool isAttackPressed = (input.flags & InputFlags.LightAttack) != 0;
        if (isAttackPressed)
        {
            return WakeUp_Type.Attack;
        }

        bool isLeftPressed = (input.flags & InputFlags.Left) != 0;
        if (isLeftPressed)
        {
            return WakeUp_Type.RollLeft;
        }

        bool isRightPressed = (input.flags & InputFlags.Right) != 0;
        if (isRightPressed)
        {
            return WakeUp_Type.RollRight;
        }

        bool isForwardPressed = (input.flags & InputFlags.Up) != 0;
        if (isForwardPressed)
        {
            return WakeUp_Type.RollForward;
        }

        bool isBackwardPressed = (input.flags & InputFlags.Down) != 0;
        if (isBackwardPressed)
        {
            return WakeUp_Type.RollBackward;
        }

        return WakeUp_Type.InPlace;
    }
}

public class WakeUpState : PlayerStateBase
{
    private int wakeUpDuration;

    public WakeUpState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.WakeUp;

    public override void Enter()
    {
        WakeUp_Type currentType = stateMachine.GetWakeUpType();
        wakeUpDuration = config.GetWakeUpFrames(currentType);
        
        stateMachine.SetVelocity(Vector3.zero);
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isWakeUpComplete = currentFrame >= wakeUpDuration;

        if (isWakeUpComplete)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
        }
    }
}