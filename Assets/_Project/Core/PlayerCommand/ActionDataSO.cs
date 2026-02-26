using UnityEngine;

[CreateAssetMenu(fileName = "NewActionData", menuName = "ScriptableObjects/ActionData")]
public class ActionDataSO : ScriptableObject
{
    public AnimationClip animationClip;
    public string animationStateName;
    public AnimationFrameData frameData;
}