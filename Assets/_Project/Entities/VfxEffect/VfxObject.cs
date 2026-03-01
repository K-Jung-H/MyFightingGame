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

    public void Play(VfxClipSO clip, Transform target, Vector3 offset, Quaternion rotOffset, bool attached)
    {
        currentClip = clip;
        attachedTarget = target;
        localOffset = offset;
        localRotationOffset = rotOffset;
        isAttached = attached;
        
        transform.localScale = Vector3.one * currentClip.scale;
        
        timer = 0f;
        currentFrameIndex = 0;
        isPlaying = true;
        spriteRenderer.sprite = currentClip.frames[0];
        
        bool isDetachedWithTarget = !isAttached && attachedTarget != null;
        if (isDetachedWithTarget)
        {
            transform.position = attachedTarget.position + attachedTarget.rotation * localOffset;
        }
        
        UpdateTransform();
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!isPlaying) return;
        UpdateTransform();
        UpdateAnimation();
    }

    private void UpdateTransform()
    {
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