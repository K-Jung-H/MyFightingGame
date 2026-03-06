using UnityEngine;

public class IdleState : PlayerStateBase
{
    public IdleState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Idle;

    public override void UpdateTick(PlayerInput input)
    {
        bool isDownPressed = (input.flags & InputFlags.Down) != 0;
        if (isDownPressed)
        {
            stateMachine.TransitionTo(PlayerState_Type.Crouching);
            return;
        }

        bool hasMovementInput = GetRawInputVector(input.flags) != Vector3.zero;
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
        bool hasNoInput = GetRawInputVector(input.flags) == Vector3.zero;
        if (hasNoInput)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }
        
        bool isDownPressed = (input.flags & InputFlags.Down) != 0;
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
        bool hasNoInput = GetRawInputVector(input.flags) == Vector3.zero;
        if (hasNoInput)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }

        bool isDownPressed = (input.flags & InputFlags.Down) != 0;
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
        Vector3 inputVector = GetRawInputVector(input.flags);
        bool hasNoInput = inputVector == Vector3.zero;
        
        if (hasNoInput)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }

        bool isDownPressed = (input.flags & InputFlags.Down) != 0;
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
        InputFlags triggeredFlags = controller.currentInput.flags;
        bool isUpTriggered = (triggeredFlags & InputFlags.Up) != 0;
        bool isDownTriggered = (triggeredFlags & InputFlags.Down) != 0;

        if (isUpTriggered)
        {
            stepDirection = 1f;
        }
        else if (isDownTriggered)
        {
            stepDirection = -1f;
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        Vector3 newPos = physics.GetPosition() + physics.GetDepthAxis() * (stepDirection * config.sideStepSpeed);
        physics.SetPosition(newPos);

        bool isHoldingUp = stepDirection > 0 && (input.flags & InputFlags.Up) != 0;
        bool isHoldingDown = stepDirection < 0 && (input.flags & InputFlags.Down) != 0;

        bool isPastCancelWindow = stateMachine.GetStateFrameCounter() > 10;
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
        bool isHoldingUp = (input.flags & InputFlags.Up) != 0;
        bool isHoldingDown = (input.flags & InputFlags.Down) != 0;

        bool isNoDirectionHeld = !isHoldingUp && !isHoldingDown;
        if (isNoDirectionHeld)
        {
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return;
        }

        float currentDirection = isHoldingUp ? 1f : -1f;
        Vector3 newPos = physics.GetPosition() + physics.GetDepthAxis() * (currentDirection * config.sideWalkSpeed);
        physics.SetPosition(newPos);
    }
}