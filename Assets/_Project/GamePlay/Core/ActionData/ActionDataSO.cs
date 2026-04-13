using UnityEngine;

[CreateAssetMenu(fileName = "NewActionData", menuName = "ScriptableObjects/ActionData")]
public class ActionDataSO : ScriptableObject
{
    public AnimationClip animationClip;
    public string animationStateName;
    public AnimationFrameData frameData;

    [System.NonSerialized]
    private FPHitboxEvent[] cachedFPHitboxEvents;

    [System.NonSerialized]
    private FPRootMotionData[] cachedFPRootMotionPath;
    
    [System.NonSerialized]
    private bool isCached = false;

    public FPHitboxEvent[] GetCachedFPHitboxEvents()
    {
        if (!isCached) CacheFPData();
        return cachedFPHitboxEvents;
    }

    public FPRootMotionData[] GetCachedFPRootMotionPath()
    {
        if (!isCached) CacheFPData();
        return cachedFPRootMotionPath;
    }

    private void CacheFPData()
    {
        if (frameData == null)
        {
            cachedFPHitboxEvents = new FPHitboxEvent[0];
            cachedFPRootMotionPath = new FPRootMotionData[0];
            isCached = true;
            return;
        }

        if (frameData.hitboxEvents != null)
        {
            cachedFPHitboxEvents = new FPHitboxEvent[frameData.hitboxEvents.Length];
            for (int i = 0; i < frameData.hitboxEvents.Length; i++)
            {
                HitboxEvent evt = frameData.hitboxEvents[i];
                FPHitboxEvent fpEvt = new FPHitboxEvent
                {
                    markerName = evt.markerName,
                    activeStartFrame = evt.activeStartFrame,
                    hitGroupID = evt.hitGroupID,
                    attackHeight = evt.attackHeight,
                    attackType = evt.attackType,
                    targetHurtState = evt.targetHurtState,
                    damage = evt.damage,
                    hitstunFrames = evt.hitstunFrames,
                    blockStunFrames = evt.blockStunFrames,
                    localPushbackVector = FPVector3.FromVector3(evt.localPushbackVector),
                    isHardKnockdown = evt.isHardKnockdown
                };

                if (evt.boxPath != null)
                {
                    fpEvt.boxPath = new FPCollisionBox[evt.boxPath.Length];
                    for (int j = 0; j < evt.boxPath.Length; j++)
                    {
                        fpEvt.boxPath[j] = new FPCollisionBox
                        {
                            localPosition = FPVector3.FromVector3(evt.boxPath[j].localPosition),
                            extents = FPVector3.FromVector3(evt.boxPath[j].extents)
                        };
                    }
                }
                cachedFPHitboxEvents[i] = fpEvt;
            }
        }
        else
        {
            cachedFPHitboxEvents = new FPHitboxEvent[0];
        }

        if (frameData.rootMotionPath != null)
        {
            cachedFPRootMotionPath = new FPRootMotionData[frameData.rootMotionPath.Length];
            for (int i = 0; i < frameData.rootMotionPath.Length; i++)
            {
                cachedFPRootMotionPath[i] = new FPRootMotionData
                {
                    deltaPosition = FPVector3.FromVector3(frameData.rootMotionPath[i].deltaPosition)
                };
            }
        }
        else
        {
            cachedFPRootMotionPath = new FPRootMotionData[0];
        }

        isCached = true;
    }
}