using UnityEngine;

public class IdleState : PlayerStateBase
{
    public IdleState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Idle;

    public override void UpdateTick(PlayerInput input)
    {
        InputStateTracker tracker = controller.GetTracker();
        bool isDownPressed = tracker.IsHeld(InputFlags.Down);
        if (isDownPressed)
        {
            stateMachine.TransitionTo(PlayerState_Type.Crouching);
            return;
        }

        bool hasForwardBackInput = tracker.IsHeld(InputFlags.Forward) || tracker.IsHeld(InputFlags.Back);
        if (hasForwardBackInput)
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
        InputStateTracker tracker = controller.GetTracker();
        bool hasForwardBackInput = tracker.IsHeld(InputFlags.Forward) || tracker.IsHeld(InputFlags.Back);
        if (!hasForwardBackInput)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }
        
        bool isDownPressed = tracker.IsHeld(InputFlags.Down);
        if (isDownPressed)
        {
            stateMachine.TransitionTo(PlayerState_Type.Crouching);
            return;
        }

        ProcessMovementLogic(input);
    }
}

public class RunningState : PlayerStateBase
{
    private int runningForwardFrames;

    public RunningState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Running;

    public override void Enter()
    {
        base.Enter();
        runningForwardFrames = 0;
    }

    public override void UpdateTick(PlayerInput input)
    {
        InputStateTracker tracker = controller.GetTracker();
        bool hasForwardBackInput = tracker.IsHeld(InputFlags.Forward) || tracker.IsHeld(InputFlags.Back);
        if (!hasForwardBackInput)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }

        bool isDownPressed = tracker.IsHeld(InputFlags.Down);
        if (isDownPressed)
        {
            stateMachine.TransitionTo(PlayerState_Type.Crouching);
            return;
        }

        ProcessMovementLogic(input);

        runningForwardFrames++;
        bool isSprintThresholdReached = runningForwardFrames >= config.GetAutoSprintFrames(); 
        if (isSprintThresholdReached)
        {
            stateMachine.TransitionTo(PlayerState_Type.Sprinting);
        }
    }
}

public class SprintingState : PlayerStateBase
{
    public SprintingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Sprinting;

    public override void UpdateTick(PlayerInput input)
    {
        InputStateTracker tracker = controller.GetTracker();
        bool hasForwardBackInput = tracker.IsHeld(InputFlags.Forward) || tracker.IsHeld(InputFlags.Back);
        
        if (!hasForwardBackInput)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }

        bool isDownPressed = tracker.IsHeld(InputFlags.Down);
        if (isDownPressed)
        {
            stateMachine.TransitionTo(PlayerState_Type.Crouching);
            return;
        }

        ProcessMovementLogic(input);
    }
}

public class CrouchingState : PlayerStateBase
{
    public CrouchingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Crouching;

    public override void Enter()
    {
        Vector3 zeroVelocity = Vector3.zero;
        zeroVelocity.y = physics.GetVelocity().y;
        physics.SetVelocity(zeroVelocity);
    }

    public override void UpdateTick(PlayerInput input)
    {
        bool isDownPressed = (input.flags & InputFlags.Down) != 0;
        bool isStandRequested = !isDownPressed;

        if (isStandRequested)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }

        ProcessCrouchMovementLogic(input);
    }
}

public class SideStepState : PlayerStateBase
{
    private float stepDirection;

    public SideStepState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.SideStep;

    public override void Enter()
    {
        stepDirection = 0f;
        InputStateTracker tracker = controller.GetTracker();

        bool isUpTriggered = tracker.IsHeld(InputFlags.Up);
        bool isDownTriggered = tracker.IsHeld(InputFlags.Down);

        if (isUpTriggered) stepDirection = -1f;
        else if (isDownTriggered) stepDirection = 1f;

        bool isFallbackNeeded = stepDirection == 0f;
        if (isFallbackNeeded) stepDirection = 1f;
    }

    public override void UpdateTick(PlayerInput input)
    {
        Vector3 moveVelocity = physics.GetDepthAxis() * (stepDirection * config.sideStepSpeed);
        moveVelocity.y = physics.GetVelocity().y;
        physics.SetVelocity(moveVelocity);

        InputStateTracker tracker = controller.GetTracker();
        bool isHoldingUp = stepDirection < 0 && tracker.IsHeld(InputFlags.Up);
        bool isHoldingDown = stepDirection > 0 && tracker.IsHeld(InputFlags.Down);

        int cancelFrame = config.sideStepFrames > 2 ? config.sideStepFrames / 2 : 1;
        bool isPastCancelWindow = stateMachine.GetStateFrameCounter() >= cancelFrame;

        if (isPastCancelWindow && (isHoldingUp || isHoldingDown))
        {
            stateMachine.TransitionTo(PlayerState_Type.SideWalk);
            return;
        }

        bool isStepFinished = stateMachine.GetStateFrameCounter() >= config.sideStepFrames;
        if (isStepFinished)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
        }
    }
}

public class SideWalkState : PlayerStateBase
{
    public SideWalkState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.SideWalk;

    public override void UpdateTick(PlayerInput input)
    {
        InputStateTracker tracker = controller.GetTracker();
        bool isHoldingUp = tracker.IsHeld(InputFlags.Up);
        bool isHoldingDown = tracker.IsHeld(InputFlags.Down);

        bool isNoDirectionHeld = !isHoldingUp && !isHoldingDown;
        if (isNoDirectionHeld)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }

        float currentDirection = isHoldingDown ? 1f : -1f;
        Vector3 moveVelocity = physics.GetDepthAxis() * (currentDirection * config.sideWalkSpeed);
        moveVelocity.y = physics.GetVelocity().y;
        physics.SetVelocity(moveVelocity);
    }
}