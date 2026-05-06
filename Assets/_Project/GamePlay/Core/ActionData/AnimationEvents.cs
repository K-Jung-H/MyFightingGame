using UnityEngine;

[System.Serializable]
public struct HurtboxEvent
{
    public int startFrame;
    public int endFrame;
    public Hurtbox_Type hurtboxType;
}

[System.Serializable]
public struct VfxEvent
{
    public string markerName; 
    public int startFrame;
    public int endFrame;
    public int intervalFrames;
    public EffectType effectType;
    public HumanBodyBones targetBone;
    public Vector3 localPositionOffset;
    public Quaternion localRotationOffset;
    public bool isAttached;
}

[System.Serializable]
public struct ActionLogicData
{
    public int totalFrames;
    public int startupFrames;
    public int recoveryFrames;
    public int cancelWindowStartFrame;

    public bool useRootMotion;
    public bool isHoming;
}