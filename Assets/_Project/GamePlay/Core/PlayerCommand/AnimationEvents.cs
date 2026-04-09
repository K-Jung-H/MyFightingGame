using UnityEngine;

[System.Serializable]
public struct HurtboxEvent
{
    public int startFrame;
    public int endFrame;
    public Hurtbox_Type hurtboxType;
}

[System.Serializable]
public struct HitboxEvent
{
    public string markerName; 
    public int activeStartFrame;
    public CollisionBox[] boxPath;
    public int hitGroupID;
    public Attack_Height attackHeight;
    public Attack_Type attackType;
    public HurtState_Type targetHurtState;
    public int damage;
    public int hitstunFrames;
    public int blockStunFrames;
    public Vector3 localPushbackVector;
    public bool isHardKnockdown;
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
    public bool useRootRotation;
    public bool isHoming;
}