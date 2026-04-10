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
    private bool isCached = false;

    public FPHitboxEvent[] GetCachedFPHitboxEvents()
    {
        if (!isCached)
        {
            CacheFPData();
        }
        return cachedFPHitboxEvents;
    }

    private void CacheFPData()
    {
        if (frameData == null || frameData.hitboxEvents == null)
        {
            cachedFPHitboxEvents = new FPHitboxEvent[0];
            isCached = true;
            return;
        }

        cachedFPHitboxEvents = new FPHitboxEvent[frameData.hitboxEvents.Length];

        for (int i = 0; i < frameData.hitboxEvents.Length; i++)
        {
            HitboxEvent evt = frameData.hitboxEvents[i];
            FPHitboxEvent fpEvt = new FPHitboxEvent();

            fpEvt.markerName = evt.markerName;
            fpEvt.activeStartFrame = evt.activeStartFrame;
            fpEvt.hitGroupID = evt.hitGroupID;
            fpEvt.attackHeight = evt.attackHeight;
            fpEvt.attackType = evt.attackType;
            fpEvt.targetHurtState = evt.targetHurtState;
            fpEvt.damage = evt.damage;
            fpEvt.hitstunFrames = evt.hitstunFrames;
            fpEvt.blockStunFrames = evt.blockStunFrames;
            fpEvt.localPushbackVector = FPVector3.FromVector3(evt.localPushbackVector);
            fpEvt.isHardKnockdown = evt.isHardKnockdown;

            if (evt.boxPath != null)
            {
                fpEvt.boxPath = new FPCollisionBox[evt.boxPath.Length];
                for (int j = 0; j < evt.boxPath.Length; j++)
                {
                    FPCollisionBox box = new FPCollisionBox();
                    box.localPosition = FPVector3.FromVector3(evt.boxPath[j].localPosition);
                    box.extents = FPVector3.FromVector3(evt.boxPath[j].extents);
                    fpEvt.boxPath[j] = box;
                }
            }

            cachedFPHitboxEvents[i] = fpEvt;
        }

        isCached = true;
    }
}