using UnityEngine;
using System.Collections;

public class CameraManager : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform playerOne;
    [SerializeField] private Transform playerTwo;

    [Header("Camera Control Settings")]
    [SerializeField] private bool isReverseView;
    [SerializeField] private float heightOffset = 1.5f;
    [SerializeField] private float distanceOffset = 5.0f;
    [SerializeField] private float zoomSensitivity = 0.6f;
    [SerializeField] private float minDistance = 4.0f;
    [SerializeField] private float maxDistance = 12.0f;
    [SerializeField] private float movementSmoothTime = 0.15f;
    [SerializeField] private float rotationSmoothTime = 0.1f;

    [Header("Event Zoom Settings")]
    [SerializeField] private float zoomLerpSpeed = 5.0f;

    [Header("Shake Settings")]
    [SerializeField] private float defaultShakeMagnitude = 0.1f;
    [SerializeField] private float defaultShakeDuration = 0.2f;

    private float targetZoomMultiplier = 1.0f;
    private float currentZoomMultiplier = 1.0f;
    private Vector3 currentVelocity;
    private Vector3 shakeOffset;

    public void SetTargetPlayers(GameObject playerOne, GameObject playerTwo)
    {
        this.playerOne = playerOne.transform;
        this.playerTwo = playerTwo.transform;
    }

    public void TriggerEventZoom(float multiplier, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(HandleZoomDuration(multiplier, duration));
    }

    private IEnumerator HandleZoomDuration(float multiplier, float duration)
    {
        targetZoomMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        targetZoomMultiplier = 1.0f;
    }

    public void TriggerShake(float magnitude = -1f, float duration = -1f)
    {
        float shakeMagnitude = magnitude < 0 ? defaultShakeMagnitude : magnitude;
        float shakeDuration = duration < 0 ? defaultShakeDuration : duration;
        StartCoroutine(ProcessShake(shakeMagnitude, shakeDuration));
    }

    private IEnumerator ProcessShake(float magnitude, float duration)
    {
        float elapsedTimer = 0f;
        while (elapsedTimer < duration)
        {
            float randomX = Random.Range(-1f, 1f) * magnitude;
            float randomY = Random.Range(-1f, 1f) * magnitude;
            shakeOffset = new Vector3(randomX, randomY, 0f);
            elapsedTimer += Time.deltaTime;
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }

    public bool IsPlayerOneOnRightSide()
    {
        bool isTargetMissing = playerOne == null || playerTwo == null;
        if (isTargetMissing) return true;

        Vector3 cameraRight = transform.right;
        cameraRight.y = 0f;
        cameraRight.Normalize();

        Vector3 playerOneToTwo = playerTwo.position - playerOne.position;
        playerOneToTwo.y = 0f;

        float dotResult = Vector3.Dot(playerOneToTwo, cameraRight);
        bool isPlayerOneOnLeftSide = dotResult > 0f;

        return !isPlayerOneOnLeftSide;
    }

    private void LateUpdate()
    {
        bool isTargetMissing = playerOne == null || playerTwo == null;
        if (isTargetMissing) return;
        
        UpdateZoomMultiplier();
        UpdateCameraTransform();
    }

    private void UpdateZoomMultiplier()
    {
        currentZoomMultiplier = Mathf.Lerp(currentZoomMultiplier, targetZoomMultiplier, Time.deltaTime * zoomLerpSpeed);
    }

    private void UpdateCameraTransform()
    {
        Vector3 positionP1 = playerOne.position;
        Vector3 positionP2 = playerTwo.position;
        Vector3 centerPosition = Vector3.Lerp(positionP1, positionP2, 0.5f);

        Vector3 directionP1ToP2 = positionP2 - positionP1;
        directionP1ToP2.y = 0f;

        bool isOverlapping = directionP1ToP2.sqrMagnitude < 0.001f;
        if (isOverlapping)
        {
            directionP1ToP2 = transform.right;
        }
        else
        {
            directionP1ToP2.Normalize();
        }

        Vector3 normal1 = Vector3.Cross(Vector3.up, directionP1ToP2).normalized;
        Vector3 normal2 = -normal1;

        Vector3 currentCameraDirection = transform.position - centerPosition;
        currentCameraDirection.y = 0f;
        currentCameraDirection.Normalize();

        Vector3 cameraOffsetDirection = Vector3.Dot(normal1, currentCameraDirection) > 0f ? normal1 : normal2;
        
        if (isReverseView)
        {
            cameraOffsetDirection = -cameraOffsetDirection;
        }

        float distance3D = Vector3.Distance(positionP1, positionP2);
        float desiredDistance = ((distance3D * zoomSensitivity) + distanceOffset) * currentZoomMultiplier;
        desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);

        Vector3 targetPosition = centerPosition + (cameraOffsetDirection * desiredDistance);
        targetPosition.y = centerPosition.y + heightOffset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, movementSmoothTime);
        transform.position += transform.TransformDirection(shakeOffset);

        Vector3 lookAtPoint = centerPosition;
        lookAtPoint.y = centerPosition.y + (heightOffset * 0.8f);
        
        Quaternion targetRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime / rotationSmoothTime);
    }

    public Vector3 GetDepthAxis()
    {
        Vector3 depthAxis = transform.forward;
        depthAxis.y = 0f;
        return depthAxis.normalized;
    }
}