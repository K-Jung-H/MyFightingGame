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
    public StandHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.StandHit;

    public override void Enter()
    {
        base.Enter();
        
        bool isGuardHit = currentHurtInfo.targetHurtState == HurtState_Type.GuardHit;
        float pushbackMultiplier = isGuardHit ? 0.5f : 1.0f;
        
        Vector3 horizontalPushback = currentHurtInfo.pushbackVector;
        horizontalPushback.y = 0f;
        stateMachine.ApplyPushback(horizontalPushback * pushbackMultiplier);
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isStunCompleted = currentFrame >= currentHurtInfo.hurtStunFrames;
        
        if (isStunCompleted)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
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
        
        Vector3 horizontalPushback = currentHurtInfo.pushbackVector;
        horizontalPushback.y = 0f;
        stateMachine.ApplyPushback(horizontalPushback);
    }

    public override void UpdateTick(PlayerInput input)
    {
        Vector3 currentPos = stateMachine.GetPosition();
        float currentYVelocity = stateMachine.GetYVelocity();
        
        bool isGrounded = currentPos.y <= 0f && currentYVelocity <= 0f;
        if (isGrounded)
        {
            stateMachine.TransitionTo(PlayerState_Type.Knockdown);
        }
    }
}

public class KnockdownState : HitStateBase
{
    private int knockdownFrames = 30;

    public KnockdownState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Knockdown;

    public override void Enter()
    {
        base.Enter();
        
        bool isFromStand = stateMachine.GetPosition().y <= 0f;
        if (isFromStand)
        {
            Vector3 horizontalPushback = currentHurtInfo.pushbackVector;
            horizontalPushback.y = 0f;
            stateMachine.ApplyPushback(horizontalPushback);
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isKnockdownCompleted = currentFrame >= knockdownFrames;
        
        if (isKnockdownCompleted)
        {
            stateMachine.TransitionTo(PlayerState_Type.WakeUp);
        }
    }
}

public class WakeUpState : HitStateBase
{
    private int wakeupFrames = 20;

    public WakeUpState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.WakeUp;

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        bool isWakeUpCompleted = currentFrame >= wakeupFrames;
        
        if (isWakeUpCompleted)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
        }
    }
}