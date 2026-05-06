using UnityEngine;

[System.Serializable]
public struct HitFeedbackData
{
    public Attack_Type attackType;
    public int hitstopFrames;
    public float cameraShakeIntensity;
}


public struct HurtInfo
{
    public int damage;
    public int hurtStunFrames;
    public FPVector3 pushbackVector;
    public HurtState_Type targetHurtState;
    public bool isHardKnockdown;
    public Attack_Height attackHeight;
}

[System.Serializable]
public struct FPRootMotionData
{
    public FPVector3 deltaPosition;
}

[System.Serializable]
public struct FPCollisionBox
{
    public FPVector3 localPosition;
    public FPVector3 extents;
}


[System.Serializable]
public struct HurtboxPreset
{
    public Hurtbox_Type type;
    public FPCollisionBox[] boxes;
}

[System.Serializable]
public struct FPHitboxEvent
{
    public string markerName;
    public int activeStartFrame;
    public FPCollisionBox[] boxPath;
    public int hitGroupID;
    public Attack_Height attackHeight;
    public Attack_Type attackType;
    public HurtState_Type targetHurtState;
    public int damage;
    public int hitstunFrames;
    public int blockStunFrames;
    public FPVector3 localPushbackVector;
    public bool isHardKnockdown;
}