using UnityEngine;
using System.Collections;

public class CameraManager : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform playerOne;
    [SerializeField] private Transform playerTwo;

    [Header("Camera Control Settings")]
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
        float m = magnitude < 0 ? defaultShakeMagnitude : magnitude;
        float d = duration < 0 ? defaultShakeDuration : duration;
        StartCoroutine(ProcessShake(m, d));
    }

    private IEnumerator ProcessShake(float magnitude, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            shakeOffset = new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (playerOne == null || playerTwo == null) return;
        
        UpdateZoomMultiplier();
        UpdateCameraTransform();
    }

    private void UpdateZoomMultiplier()
    {
        currentZoomMultiplier = Mathf.Lerp(currentZoomMultiplier, targetZoomMultiplier, Time.deltaTime * zoomLerpSpeed);
    }

    private void UpdateCameraTransform()
    {
        Vector3 p1Pos = playerOne.position;
        Vector3 p2Pos = playerTwo.position;
        Vector3 groundCenter = Vector3.Lerp(p1Pos, p2Pos, 0.5f);
        groundCenter.y = 0; 

        float horizontalDistance = Vector2.Distance(new Vector2(p1Pos.x, p1Pos.z), new Vector2(p2Pos.x, p2Pos.z));
        Vector3 viewDirection = Vector3.back;

        float desiredDistance = ((horizontalDistance * zoomSensitivity) + distanceOffset) * currentZoomMultiplier;
        desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);

        Vector3 targetPos = groundCenter + (viewDirection * desiredDistance);
        targetPos.y = heightOffset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, movementSmoothTime);
        transform.position += transform.TransformDirection(shakeOffset);

        Vector3 lookAtPoint = groundCenter;
        lookAtPoint.y = heightOffset * 0.8f; 
        
        Quaternion targetRot = Quaternion.LookRotation(lookAtPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime / rotationSmoothTime);
    }
}