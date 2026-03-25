using UnityEngine;
using System.Collections;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Transform playerOne;
    [SerializeField] private Transform playerTwo;
    [SerializeField] private bool isReverseView;
    [SerializeField] private float heightOffset = 1.5f;
    [SerializeField] private float distanceOffset = 5.0f;
    [SerializeField] private float zoomSensitivity = 0.6f;
    [SerializeField] private float minDistance = 4.0f;
    [SerializeField] private float maxDistance = 12.0f;
    [SerializeField] private float movementSmoothTime = 0.15f;
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private float zoomLerpSpeed = 5.0f;
    [SerializeField] private float defaultShakeMagnitude = 0.1f;
    [SerializeField] private float defaultShakeDuration = 0.2f;

    private float targetZoomMultiplier = 1.0f;
    private float currentZoomMultiplier = 1.0f;
    private float currentOrbitAngle;
    private float orbitAngleVelocity;
    private Vector3 currentVelocity;
    private Vector3 shakeOffset;

    private void LateUpdate()
    {
        bool isTargetMissing = playerOne == null || playerTwo == null;
        if (isTargetMissing) return;
        
        UpdateZoomMultiplier();
        UpdateCameraTransform();
    }

    public void SetTargetPlayers(GameObject p1, GameObject p2)
    {
        playerOne = p1.transform;
        playerTwo = p2.transform;
    }

    // Phase 4: 외부(GameLoopManager)에서 진영 데이터에 따라 카메라 반전을 명령하는 메서드
    public void SetCameraFlip(bool isFlipped)
    {
        isReverseView = isFlipped;
    }

    public void TriggerEventZoom(float multiplier, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(HandleZoomDuration(multiplier, duration));
    }

    public void TriggerShake(float magnitude = -1f, float duration = -1f)
    {
        float shakeMagnitude = magnitude < 0 ? defaultShakeMagnitude : magnitude;
        float shakeDuration = duration < 0 ? defaultShakeDuration : duration;
        StartCoroutine(ProcessShake(shakeMagnitude, shakeDuration));
    }

    private void UpdateZoomMultiplier()
    {
        currentZoomMultiplier = Mathf.Lerp(currentZoomMultiplier, targetZoomMultiplier, Time.deltaTime * zoomLerpSpeed);
    }

    public bool IsPlayerOneOnRightSide()
    {
        bool isTargetMissing = playerOne == null || playerTwo == null;
        if (isTargetMissing) return false;

        Vector3 toP1 = playerOne.position - transform.position;
        Vector3 toP2 = playerTwo.position - transform.position;

        float dotP1 = Vector3.Dot(transform.right, toP1);
        float dotP2 = Vector3.Dot(transform.right, toP2);

        return dotP1 > dotP2;
    }

    private void UpdateCameraTransform()
    {
        Vector3 positionP1 = playerOne.position;
        Vector3 positionP2 = playerTwo.position;
        Vector3 centerPosition = Vector3.Lerp(positionP1, positionP2, 0.5f);

        Vector3 directionP1ToP2 = positionP2 - positionP1;
        directionP1ToP2.y = 0f;

        // isReverseView 값에 따라 카메라가 바라보는 기준 각도를 180도(90도 -> -90도) 반전합니다.
        float targetAngle = Mathf.Atan2(directionP1ToP2.x, directionP1ToP2.z) * Mathf.Rad2Deg;
        targetAngle += isReverseView ? -90f : 90f;

        currentOrbitAngle = Mathf.SmoothDampAngle(currentOrbitAngle, targetAngle, ref orbitAngleVelocity, rotationSmoothTime);
        Vector3 cameraOffsetDirection = Quaternion.Euler(0, currentOrbitAngle, 0) * Vector3.forward;

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

    private IEnumerator HandleZoomDuration(float multiplier, float duration)
    {
        targetZoomMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        targetZoomMultiplier = 1.0f;
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
}