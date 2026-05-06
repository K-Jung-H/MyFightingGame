using UnityEngine;

[CreateAssetMenu(fileName = "NewActionData", menuName = "ScriptableObjects/ActionData")]
public class ActionDataSO : ScriptableObject
{
    public AnimationClip animationClip;
    public string animationStateName;
    public AnimationFrameData frameData;

    public FPHitboxEvent[] GetFPHitboxEvents()
    {
        if (frameData == null || frameData.hitboxEvents == null)
        {
            return new FPHitboxEvent[0];
        }
        return frameData.hitboxEvents;
    }

    public FPRootMotionData[] GetFPRootMotionPath()
    {
        if (frameData == null || frameData.rootMotionPath == null)
        {
            return new FPRootMotionData[0];
        }
        return frameData.rootMotionPath;
    }
}