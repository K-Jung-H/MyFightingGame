using UnityEngine;

public class FPTransformSync : MonoBehaviour
{
    public Transform visualTransform;

    private FPKinematicBody kinematicBody;
    private bool isInitialized;

    private void Awake()
    {
        isInitialized = false;
    }

    private void Update()
    {
        if (isInitialized)
        {
            SyncVisuals();
        }
    }

    public void InitializeBody(Vector3 startPosition, Vector3 initialDepthAxis)
    {
        kinematicBody = new FPKinematicBody();
        kinematicBody.currentPosition = FPVector3.FromVector3(startPosition);
        kinematicBody.depthAxis = FPVector3.FromVector3(initialDepthAxis);
        kinematicBody.gravity = FP64.FromFloat(0.5f);
        kinematicBody.groundYPosition = FP64.FromFloat(0f);
        kinematicBody.jumpForce = FP64.FromFloat(10f);
        kinematicBody.moveSpeed = FP64.FromFloat(5f);
        kinematicBody.isGrounded = true;
        kinematicBody.isMoving = false;

        isInitialized = true;
    }

    public void TickPhysics(float rawHorizontalInput)
    {
        FP64 horizontalInput = FP64.FromFloat(rawHorizontalInput);
        kinematicBody.ApplyMovement(horizontalInput);
        kinematicBody.UpdatePhysics();
    }

    public ref FPKinematicBody GetBodyReference()
    {
        return ref kinematicBody;
    }

    private void SyncVisuals()
    {
        Vector3 targetPosition = kinematicBody.currentPosition.ToVector3();
        visualTransform.position = Vector3.Lerp(visualTransform.position, targetPosition, Time.deltaTime * 15f);
    }
}