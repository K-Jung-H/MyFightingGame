public struct FPKinematicBody
{
    public FPVector3 currentPosition;
    public FPVector3 currentVelocity;
    public FPVector3 depthAxis;
    public FP64 gravity;
    public FP64 groundYPosition;
    public FP64 jumpForce;
    public FP64 moveSpeed;
    public bool isGrounded;
    public bool isMoving;

    public void UpdatePhysics()
    {
        ApplyGravity();
        UpdatePosition();
        CheckGroundCollision();
    }

    public void ApplyMovement(FP64 horizontalInput)
    {
        FPVector3 moveDirection = depthAxis * horizontalInput;
        currentVelocity.x = moveDirection.x * moveSpeed;
        currentVelocity.z = moveDirection.z * moveSpeed;
        isMoving = horizontalInput.rawValue != 0;
    }

    public void ApplyJump()
    {
        if (isGrounded)
        {
            currentVelocity.y = jumpForce;
            isGrounded = false;
        }
    }

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            currentVelocity.y = currentVelocity.y - gravity;
        }
    }

    private void UpdatePosition()
    {
        currentPosition = currentPosition + currentVelocity;
    }

    private void CheckGroundCollision()
    {
        bool isHitGround = currentPosition.y.rawValue <= groundYPosition.rawValue;
        if (isHitGround)
        {
            currentPosition.y = groundYPosition;
            currentVelocity.y = FP64.Zero;
            isGrounded = true;
        }
    }
}