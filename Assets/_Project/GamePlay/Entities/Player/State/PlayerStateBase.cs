using UnityEngine;

public abstract class PlayerStateBase
{
    protected PlayerStateMachine stateMachine;
    protected PlayerConfigSO config;
    protected PlayerController controller;
    protected PlayerPhysics physics;
    protected PlayerCombat combat;
    protected PlayerActionController actionController;

    protected FP64 cachedWalkSpeed;
    protected FP64 cachedRunSpeed;
    protected FP64 cachedSprintSpeed;
    protected FP64 cachedCrouchWalkSpeed;
    
    protected FP64 cachedSideStepSpeed;
    protected FP64 cachedSideWalkSpeed;
    protected FP64 cachedBounceThreshold;
    protected FP64 cachedBounceMultiplier;

    public PlayerStateBase(PlayerStateMachine sm, PlayerConfigSO cfg)
    {
        stateMachine = sm;
        config = cfg;
        controller = sm.GetController();
        physics = controller.GetPhysics();
        combat = controller.GetCombat();
        actionController = controller.GetActionController();

        cachedWalkSpeed = FP64.FromFloat(config.walkSpeed);
        cachedRunSpeed = FP64.FromFloat(config.runSpeed);
        cachedSprintSpeed = FP64.FromFloat(config.sprintSpeed);
        cachedCrouchWalkSpeed = FP64.FromFloat(config.crouchWalkSpeed);

        cachedSideStepSpeed = FP64.FromFloat(config.sideStepSpeed);
        cachedSideWalkSpeed = FP64.FromFloat(config.sideWalkSpeed);
        cachedBounceThreshold = FP64.FromFloat(config.GetBounceVelocityThreshold());
        cachedBounceMultiplier = FP64.FromFloat(config.GetBounceVelocityMultiplier());
    }

    public abstract PlayerState_Type GetStateType();
    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void UpdateTick(PlayerInput input);

    public virtual int GetCancelWindow()
    {
        return 999;
    }

    public FPVector3 GetFPRawInputVector(InputFlags flags)
    {
        FPVector3 inputVector = new FPVector3(new FP64(0), new FP64(0), new FP64(0));

        bool isUpPressed = (flags & InputFlags.Up) != 0;
        bool isDownPressed = (flags & InputFlags.Down) != 0;
        bool isForwardPressed = (flags & InputFlags.Forward) != 0;
        bool isBackPressed = (flags & InputFlags.Back) != 0;

        FP64 oneFP = new FP64(65536);

        if (isForwardPressed) inputVector.z = inputVector.z + oneFP;
        if (isBackPressed) inputVector.z = inputVector.z - oneFP;

        FP64 depthMultiplier = controller.invertDepthAxis ? new FP64(-65536) : oneFP;

        if (isUpPressed) inputVector.x = inputVector.x + depthMultiplier;
        if (isDownPressed) inputVector.x = inputVector.x - depthMultiplier;

        bool isMagnitudeZero = inputVector.x.rawValue == 0 && inputVector.z.rawValue == 0;
        if (isMagnitudeZero) return new FPVector3(new FP64(0), new FP64(0), new FP64(0));

        return inputVector.Normalized();
    }

    protected void ProcessMovementLogic(PlayerInput input)
    {
        FPVector3 inputDir = GetFPRawInputVector(input.flags);
        FPVector3 lookDir = physics.GetFPLookDirection();
        FPVector3 depthAxis = physics.GetFPDepthAxis();

        FP64 currentMoveSpeed = cachedWalkSpeed;
        PlayerState_Type currentState = stateMachine.GetCurrentState();

        bool isRunning = currentState == PlayerState_Type.Running;
        bool isSprinting = currentState == PlayerState_Type.Sprinting;

        if (isRunning) currentMoveSpeed = cachedRunSpeed;
        else if (isSprinting) currentMoveSpeed = cachedSprintSpeed;

        FPVector3 lateralMove = depthAxis * inputDir.x;
        FPVector3 forwardMove = lookDir * inputDir.z;

        FPVector3 moveVelocity = (forwardMove + lateralMove).Normalized() * currentMoveSpeed;
        moveVelocity.y = physics.GetFPVelocity().y;

        physics.SetFPVelocity(moveVelocity);

        bool hasMovement = inputDir.x.rawValue != 0 || inputDir.z.rawValue != 0;
        if (hasMovement)
        {
            FPVector3 currentDir = new FPVector3(moveVelocity.x, new FP64(0), moveVelocity.z).Normalized();
            physics.SetFPCurrentDirection(currentDir);
        }
    }

    protected void ProcessCrouchMovementLogic(PlayerInput input)
    {
        FPVector3 inputDir = GetFPRawInputVector(input.flags);
        FPVector3 lookDir = physics.GetFPLookDirection();

        FPVector3 moveVelocity = (lookDir * inputDir.z).Normalized() * cachedCrouchWalkSpeed;
        moveVelocity.y = physics.GetFPVelocity().y;

        physics.SetFPVelocity(moveVelocity);

        bool hasZMovement = inputDir.z.rawValue != 0;
        if (hasZMovement)
        {
            FPVector3 flatVelocity = new FPVector3(moveVelocity.x, new FP64(0), moveVelocity.z);
            physics.SetFPCurrentDirection(flatVelocity.Normalized());
        }
    }
}