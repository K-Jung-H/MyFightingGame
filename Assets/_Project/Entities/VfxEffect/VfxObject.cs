using UnityEngine;
using System;

public class VfxObject : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private VfxClipSO currentClip;
    private Transform attachedTarget;
    private Vector3 localOffset;
    private Quaternion localRotationOffset;

    private bool isAttached;
    private bool isPositionStatic;
    private float timer;
    private int currentFrameIndex;
    private bool isPlaying;
    private Transform mainCameraTransform;

    public event Action<VfxObject, VfxClipSO> OnPlaybackFinished;

    private void Awake()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        bool hasMainCamera = Camera.main != null;
        if (hasMainCamera)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    public void PlayAttached(VfxClipSO clip, Transform target, Vector3 offset, Quaternion rotOffset, bool attached)
    {
        currentClip = clip;
        attachedTarget = target;
        localOffset = offset;
        localRotationOffset = rotOffset;
        isAttached = attached;
        isPositionStatic = false;

        InitializePlayback();

        bool isDetachedWithTarget = !isAttached && attachedTarget != null;
        if (isDetachedWithTarget)
        {
            transform.position = attachedTarget.position + attachedTarget.rotation * localOffset;
        }

        UpdateTransform();
    }

    public void PlayAtPosition(VfxClipSO clip, Vector3 position, Quaternion rotation)
    {
        currentClip = clip;
        attachedTarget = null;
        isAttached = false;
        isPositionStatic = true;

        transform.position = position;
        transform.rotation = rotation;

        InitializePlayback();
    }

    private void Update()
    {
        if (!isPlaying) return;
        UpdateTransform();
        UpdateAnimation();
    }

    private void InitializePlayback()
    {
        transform.localScale = Vector3.one * currentClip.scale;
        timer = 0f;
        currentFrameIndex = 0;
        isPlaying = true;
        spriteRenderer.sprite = currentClip.frames[0];
        gameObject.SetActive(true);
    }

    private void UpdateTransform()
    {
        if (isPositionStatic) return;

        bool hasAttachedTarget = isAttached && attachedTarget != null;
        if (hasAttachedTarget)
        {
            transform.position = attachedTarget.position + attachedTarget.rotation * localOffset;
        }

        bool shouldFaceCamera = currentClip.faceCamera && mainCameraTransform != null;
        if (shouldFaceCamera)
        {
            transform.rotation = mainCameraTransform.rotation * localRotationOffset;
        }
        else if (hasAttachedTarget)
        {
            transform.rotation = attachedTarget.rotation * localRotationOffset;
        }
    }

    private void UpdateAnimation()
    {
        timer += Time.deltaTime;
        float frameDuration = 1f / currentClip.frameRate;

        bool isNextFrameReady = timer >= frameDuration;
        if (isNextFrameReady)
        {
            timer -= frameDuration;
            currentFrameIndex++;

            bool isAnimationFinished = currentFrameIndex >= currentClip.frames.Length;
            if (isAnimationFinished)
            {
                if (currentClip.isLooping)
                {
                    currentFrameIndex = 0;
                }
                else
                {
                    StopPlayback();
                    return;
                }
            }

            spriteRenderer.sprite = currentClip.frames[currentFrameIndex];
        }
    }

    public void StopPlayback()
    {
        isPlaying = false;
        gameObject.SetActive(false);
        OnPlaybackFinished?.Invoke(this, currentClip);
    }
}