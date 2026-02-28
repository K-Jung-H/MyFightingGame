using UnityEngine;

public abstract class PlayerStateBase
{
    protected PlayerStateMachine stateMachine;
    protected PlayerConfigSO config;

    public PlayerStateBase(PlayerStateMachine sm, PlayerConfigSO cfg)
    {
        stateMachine = sm;
        config = cfg;
    }

    public abstract PlayerState_Type GetStateType();
    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void UpdateTick(PlayerInput input);
}

public class IdleState : PlayerStateBase
{
    public IdleState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Idle;

    public override void UpdateTick(PlayerInput input)
    {
        bool hasMovementInput = stateMachine.GetRawInputVector(input.flags) != Vector3.zero;
        if (hasMovementInput)
        {
            stateMachine.TransitionTo(PlayerState_Type.Walking);
        }
    }
}

public class WalkingState : PlayerStateBase
{
    public WalkingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Walking;

    public override void UpdateTick(PlayerInput input)
    {
        stateMachine.ProcessMovementLogic(input);
    }
}

public class RunningState : PlayerStateBase
{
    public RunningState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Running;

    public override void UpdateTick(PlayerInput input)
    {
        stateMachine.ProcessMovementLogic(input);

        bool isForward = (input.flags & InputFlags.Up) != 0;
        if (isForward)
        {
            stateMachine.IncrementRunningForwardFrames();
            
            bool isSprintThresholdMet = stateMachine.GetRunningForwardFrames() >= config.autoSprintFrames;
            if (isSprintThresholdMet)
            {
                stateMachine.TransitionTo(PlayerState_Type.Sprinting);
            }
        }
    }
}

public class SprintingState : PlayerStateBase
{
    public SprintingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Sprinting;

    public override void UpdateTick(PlayerInput input)
    {
        stateMachine.ProcessMovementLogic(input);

        bool isForward = (input.flags & InputFlags.Up) != 0;
        if (!isForward)
        {
            stateMachine.TransitionTo(PlayerState_Type.Running);
        }
    }
}

public class StunState : PlayerStateBase
{
    public StunState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Stun;

    public override void UpdateTick(PlayerInput input) { }
}

public class AttackingState : PlayerStateBase
{
    private ActionDataSO currentActionData;

    public AttackingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Attacking;

    public override void Enter()
    {
        currentActionData = stateMachine.GetCurrentActionData();
    }

    public override void UpdateTick(PlayerInput input)
    {
        bool isAttackingStateValid = stateMachine.GetCurrentState() == PlayerState_Type.Attacking;
        if (!isAttackingStateValid) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        int totalFrames = currentActionData.frameData.logicData.totalFrames;

        bool isActionCompleted = currentFrame >= totalFrames;
        if (isActionCompleted)
        {
            stateMachine.ClearComboSequence();
            stateMachine.ClearCurrentAction();
            stateMachine.TransitionTo(PlayerState_Type.Idle);
        }
    }

    public int GetCancelWindow()
    {
        bool hasValidFrameData = currentActionData != null && currentActionData.frameData != null;
        if (hasValidFrameData)
        {
            return currentActionData.frameData.logicData.cancelWindowStartFrame;
        }
        return 999;
    }
}

