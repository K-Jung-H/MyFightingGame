using UnityEngine;

public class IdleState : PlayerStateBase
{
    public IdleState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Idle;

    public override void Enter()
    {
        base.Enter();
        FPVector3 vel = physics.GetFPVelocity();
        vel.x = new FP64(0);
        vel.z = new FP64(0);
        physics.SetFPVelocity(vel);
    }
    
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
        FPVector3 vel = physics.GetFPVelocity();
        vel.x = new FP64(0);
        vel.z = new FP64(0);
        physics.SetFPVelocity(vel);
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
    private FP64 stepDirection;

    public SideStepState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.SideStep;
    public void SetStepDirection(FP64 dir) => stepDirection = dir;
    public FP64 GetStepDirection() => stepDirection;
    
    public override void Enter()
    {
        actionController.ClearAllBuffers();
        stepDirection = new FP64(0);
        InputStateTracker tracker = controller.GetTracker();

        bool isUpTriggered = tracker.IsHeld(InputFlags.Up);
        bool isDownTriggered = tracker.IsHeld(InputFlags.Down);

        FP64 depthMultiplier = controller.invertDepthAxis ? FP64.FromFloat(-1f) : FP64.FromFloat(1f);

        if (isUpTriggered) stepDirection = depthMultiplier;
        else if (isDownTriggered) stepDirection = FP64.FromFloat(-1f) * depthMultiplier;

        bool isFallbackNeeded = stepDirection.rawValue == 0;
        if (isFallbackNeeded) stepDirection = depthMultiplier;
    }

    public override void UpdateTick(PlayerInput input)
    {
        FP64 speedFP = FP64.FromFloat(config.sideStepSpeed);
        
        FPVector3 moveVelocity = physics.GetFPDepthAxis() * (stepDirection * speedFP);
        moveVelocity.y = physics.GetFPVelocity().y;
        physics.SetFPVelocity(moveVelocity);

        FP64 depthMultiplier = controller.invertDepthAxis ? FP64.FromFloat(-1f) : FP64.FromFloat(1f);
        FP64 logicalDirection = stepDirection * depthMultiplier;

        InputStateTracker tracker = controller.GetTracker();
        bool isHoldingUp = logicalDirection.rawValue > 0 && tracker.IsHeld(InputFlags.Up);
        bool isHoldingDown = logicalDirection.rawValue < 0 && tracker.IsHeld(InputFlags.Down);

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

        FP64 depthMultiplier = controller.invertDepthAxis ? FP64.FromFloat(-1f) : FP64.FromFloat(1f);
        FP64 currentDirFP = isHoldingDown ? (FP64.FromFloat(-1f) * depthMultiplier) : depthMultiplier;
        FP64 speedFP = FP64.FromFloat(config.sideWalkSpeed);

        FPVector3 moveVelocity = physics.GetFPDepthAxis() * (currentDirFP * speedFP);
        moveVelocity.y = physics.GetFPVelocity().y;
        physics.SetFPVelocity(moveVelocity);
    }
}