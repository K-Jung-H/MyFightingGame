using UnityEngine;

public class PlayerPhysics
{
    private Vector3 position;
    private Vector3 velocity;
    private Vector3 depthAxis;
    private Vector3 currentDirection;
    private Vector3 lookDirection;
    private float globalGravity;
    private bool isGrounded;
    private bool isRootMotionActiveThisFrame;
    private PlayerConfigSO config;

    public void Initialize(Vector3 startPosition, PlayerConfigSO playerConfig)
    {
        position = startPosition;
        velocity = Vector3.zero;
        depthAxis = Vector3.forward;
        currentDirection = Vector3.forward;
        lookDirection = Vector3.forward;
        config = playerConfig;
    }

    public void UpdateLookDirection(ITargetable targetEntity, PlayerState_Type currentState, bool isHoming = false)
    {
        bool isLookUpdateDisabled = targetEntity == null || (currentState == PlayerState_Type.Attacking && !isHoming);
        if (isLookUpdateDisabled) return;

        Vector3 diff = targetEntity.GetPosition() - position;
        diff.y = 0;

        bool isTargetValid = diff.sqrMagnitude > 0.0001f;
        if (isTargetValid)
        {
            lookDirection = diff.normalized;
        }
    }

    public void ProcessPhysicsTick()
    {
        float deceleration = globalGravity * config.gravityScale;

        velocity.x = Mathf.MoveTowards(velocity.x, 0f, deceleration);
        velocity.z = Mathf.MoveTowards(velocity.z, 0f, deceleration);

        if (!isRootMotionActiveThisFrame)
        {
            velocity.y -= deceleration;
        }

        position += velocity;
        isGrounded = position.y <= 0f;

        if (isGrounded)
        {
            bool isFalling = velocity.y < 0f;
            if (isFalling)
            {
                velocity.y = 0f;
            }
            position.y = 0f;
        }
    }

    public void ResetRootMotionFlag()
    {
        isRootMotionActiveThisFrame = false;
    }

    public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        Vector3 worldDeltaPos = Quaternion.LookRotation(lookDirection) * deltaPosition;
        position += worldDeltaPos;
        lookDirection = deltaRotation * lookDirection;
        isRootMotionActiveThisFrame = true;
        velocity.y = 0f;
    }

    public Vector3 GetPosition() => position;
    public void SetPosition(Vector3 newPos) => position = newPos;
    public Vector3 GetVelocity() => velocity;
    public void SetVelocity(Vector3 newVelocity) => velocity = newVelocity;
    public void ApplyPushback(Vector3 pushVector) => position += pushVector;
    public bool GetIsGrounded() => isGrounded;
    public void SetGlobalGravity(float gravity) => globalGravity = gravity;
    public void SetDepthAxis(Vector3 axis) => depthAxis = axis;
    public Vector3 GetDepthAxis() => depthAxis;
    public Vector3 GetLookDirection() => lookDirection;
    public Vector3 GetCurrentDirection() => currentDirection;
    public void SetCurrentDirection(Vector3 dir) => currentDirection = dir;
}