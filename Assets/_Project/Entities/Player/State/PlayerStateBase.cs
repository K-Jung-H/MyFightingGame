using UnityEngine;

public abstract class PlayerStateBase
{
    protected PlayerStateMachine stateMachine;
    protected PlayerConfigSO config;
    protected PlayerController controller;
    protected PlayerPhysics physics;
    protected PlayerCombat combat;
    protected PlayerActionController actionController;

    public PlayerStateBase(PlayerStateMachine sm, PlayerConfigSO cfg)
    {
        stateMachine = sm;
        config = cfg;
        controller = sm.GetController();
        physics = controller.GetPhysics();
        combat = controller.GetCombat();
        actionController = controller.GetActionController();
    }

    public abstract PlayerState_Type GetStateType();
    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void UpdateTick(PlayerInput input);

    public virtual int GetCancelWindow()
    {
        return 999;
    }

    public Vector3 GetRawInputVector(InputFlags flags)
    {
        Vector3 inputVector = Vector3.zero;

        bool isUpPressed = (flags & InputFlags.Up) != 0;
        bool isDownPressed = (flags & InputFlags.Down) != 0;
        bool isForwardPressed = (flags & InputFlags.Forward) != 0;
        bool isBackPressed = (flags & InputFlags.Back) != 0;

        if (isForwardPressed) inputVector.z += 1f;
        if (isBackPressed) inputVector.z -= 1f;

        if (isUpPressed) inputVector.x -= 1f;
        if (isDownPressed) inputVector.x += 1f;

        bool isMagnitudeZero = inputVector.sqrMagnitude == 0f;
        if (isMagnitudeZero) return Vector3.zero;

        return inputVector.normalized;
    }


    protected void ProcessMovementLogic(PlayerInput input)
    {
        Vector3 inputDir = GetRawInputVector(input.flags);
        inputDir.x = 0f; 

        Vector3 lookDir = physics.GetLookDirection();
        Vector3 depthAxis = physics.GetDepthAxis();

        float currentMoveSpeed = config.walkSpeed;
        PlayerState_Type currentState = stateMachine.GetCurrentState();

        bool isRunning = currentState == PlayerState_Type.Running;
        bool isSprinting = currentState == PlayerState_Type.Sprinting;

        if (isRunning) currentMoveSpeed = config.runSpeed;
        else if (isSprinting) currentMoveSpeed = config.sprintSpeed;

        Vector3 lateralMove = depthAxis * -inputDir.x;
        Vector3 forwardMove = lookDir * inputDir.z;

        Vector3 moveVelocity = (forwardMove + lateralMove).normalized * currentMoveSpeed;
        moveVelocity.y = physics.GetVelocity().y;

        physics.SetVelocity(moveVelocity);

        bool hasMovement = inputDir != Vector3.zero;
        if (hasMovement)
        {
            physics.SetCurrentDirection(new Vector3(moveVelocity.x, 0f, moveVelocity.z).normalized);
        }
    }
    protected void ProcessCrouchMovementLogic(PlayerInput input)
    {
        Vector3 inputDir = GetRawInputVector(input.flags);
        Vector3 lookDir = physics.GetLookDirection();

        Vector3 moveVelocity = (lookDir * inputDir.z).normalized * config.crouchWalkSpeed;
        moveVelocity.y = physics.GetVelocity().y;

        physics.SetVelocity(moveVelocity);

        bool hasZMovement = inputDir.z != 0f;
        if (hasZMovement)
        {
            Vector3 flatVelocity = new Vector3(moveVelocity.x, 0f, moveVelocity.z);
            physics.SetCurrentDirection(flatVelocity.normalized);
        }
    }
}