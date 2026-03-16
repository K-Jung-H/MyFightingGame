using UnityEngine;

public class PlayerPhysics : ISnapshotSync
{
    private FPVector3 position;
    private FPVector3 velocity;
    private FPVector3 depthAxis;
    private FPVector3 currentDirection;
    private FPVector3 lookDirection;
    private FP64 globalGravity;
    private bool isGrounded;
    private bool isRootMotionActiveThisFrame;
    private PlayerConfigSO config;
    private FP64 lastImpactFallSpeed;
    private FP64 cachedGravityScale;

    public void ExportState(ref PlayerSnapshot snapshot)
    {
        snapshot.position = this.position;
        snapshot.velocity = this.velocity;
        snapshot.depthAxis = this.depthAxis;
        snapshot.currentDirection = this.currentDirection;
        snapshot.lookDirection = this.lookDirection;
        snapshot.isGrounded = this.isGrounded;
        snapshot.isRootMotionActiveThisFrame = this.isRootMotionActiveThisFrame;
        snapshot.lastImpactFallSpeed = this.lastImpactFallSpeed;
    }

    public void ImportState(PlayerSnapshot snapshot)
    {
        this.position = snapshot.position;
        this.velocity = snapshot.velocity;
        this.depthAxis = snapshot.depthAxis;
        this.currentDirection = snapshot.currentDirection;
        this.lookDirection = snapshot.lookDirection;
        this.isGrounded = snapshot.isGrounded;
        this.isRootMotionActiveThisFrame = snapshot.isRootMotionActiveThisFrame;
        this.lastImpactFallSpeed = snapshot.lastImpactFallSpeed;
    }

    public void Initialize(Vector3 startPosition, PlayerConfigSO playerConfig)
    {
        position = FPVector3.FromVector3(startPosition);
        velocity = new FPVector3(new FP64(0), new FP64(0), new FP64(0));
        depthAxis = FPVector3.FromVector3(Vector3.forward);
        currentDirection = FPVector3.FromVector3(Vector3.forward);
        lookDirection = FPVector3.FromVector3(Vector3.forward);
        config = playerConfig;
        lastImpactFallSpeed = new FP64(0);
        cachedGravityScale = FP64.FromFloat(config.gravityScale);
    }

    public void UpdateLookDirection(ITargetable targetEntity, PlayerState_Type currentState, bool isHoming = false)
    {
        bool isLookUpdateDisabled = targetEntity == null || (currentState == PlayerState_Type.Attacking && !isHoming);
        if (isLookUpdateDisabled) return;

        FPVector3 targetPos = FPVector3.FromVector3(targetEntity.GetPosition());
        FPVector3 diff = targetPos - position;
        diff.y = new FP64(0);

        bool isTargetValid = (diff.x.rawValue != 0) || (diff.z.rawValue != 0);
        if (isTargetValid)
        {
            lookDirection = diff.Normalized();
        }
    }

    public void ProcessPhysicsTick()
    {
        FP64 deceleration = globalGravity * cachedGravityScale;

        velocity.x = MoveTowards(velocity.x, new FP64(0), deceleration);
        velocity.z = MoveTowards(velocity.z, new FP64(0), deceleration);

        if (!isRootMotionActiveThisFrame)
        {
            velocity.y = velocity.y - deceleration;
        }

        position = position + velocity;
        isGrounded = position.y.rawValue <= 0;

        if (isGrounded)
        {
            bool isFalling = velocity.y.rawValue < 0;
            if (isFalling)
            {
                lastImpactFallSpeed = velocity.y;
                velocity.y = new FP64(0);
            }
            position.y = new FP64(0);
        }
    }

    public void ResetRootMotionFlag()
    {
        isRootMotionActiveThisFrame = false;
    }

    public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        FPVector3 fpDeltaPos = FPVector3.FromVector3(deltaPosition);

        FPVector3 upVector = new FPVector3(new FP64(0), FP64.FromFloat(1f), new FP64(0));
        FPVector3 rightDirection = FPVector3.Cross(upVector, lookDirection);
        
        FPVector3 worldDeltaPos = (rightDirection * fpDeltaPos.x) + (upVector * fpDeltaPos.y) + (lookDirection * fpDeltaPos.z);
        
        position = position + worldDeltaPos;
        isRootMotionActiveThisFrame = true;
        velocity.y = new FP64(0);
    }

    private FP64 MoveTowards(FP64 current, FP64 target, FP64 maxDelta)
    {
        if (target.rawValue > current.rawValue)
        {
            FP64 result = current + maxDelta;
            return result.rawValue > target.rawValue ? target : result;
        }
        else
        {
            FP64 result = current - maxDelta;
            return result.rawValue < target.rawValue ? target : result;
        }
    }

    public Vector3 GetPosition() => position.ToVector3();
    public Vector3 GetVelocity() => velocity.ToVector3();
    public Vector3 GetDepthAxis() => depthAxis.ToVector3();
    public Vector3 GetLookDirection() => lookDirection.ToVector3();
    public Vector3 GetCurrentDirection() => currentDirection.ToVector3();
    public float GetLastImpactFallSpeed() => lastImpactFallSpeed.ToFloat();
    public bool GetIsGrounded() => isGrounded;

    public void SetPosition(Vector3 newPos) => position = FPVector3.FromVector3(newPos);
    public void SetVelocity(Vector3 newVelocity) => velocity = FPVector3.FromVector3(newVelocity);
    public void SetDepthAxis(Vector3 axis) => depthAxis = FPVector3.FromVector3(axis);
    public void SetCurrentDirection(Vector3 dir) => currentDirection = FPVector3.FromVector3(dir);
    public void SetGlobalGravity(float gravity) => globalGravity = FP64.FromFloat(gravity);
    public void ApplyPushback(Vector3 pushVector) => position = position + FPVector3.FromVector3(pushVector);

    public FPVector3 GetFPPosition() => position;
    public FPVector3 GetFPVelocity() => velocity;
    public FPVector3 GetFPDepthAxis() => depthAxis;
    public FPVector3 GetFPLookDirection() => lookDirection;
    public FPVector3 GetFPCurrentDirection() => currentDirection;
    public FP64 GetFPLastImpactFallSpeed() => lastImpactFallSpeed;

    public void SetFPVelocity(FPVector3 newVelocity) => velocity = newVelocity;
    public void SetFPDepthAxis(FPVector3 axis) => depthAxis = axis;
    public void SetFPCurrentDirection(FPVector3 dir) => currentDirection = dir;
    public void ApplyFPPushback(FPVector3 pushVector) => position = position + pushVector;
}