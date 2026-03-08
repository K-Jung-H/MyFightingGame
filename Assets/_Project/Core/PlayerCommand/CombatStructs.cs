using UnityEngine;

[System.Serializable]
public struct CollisionBox
{
    public Vector3 localPosition;
    public Vector3 extents;
}

[System.Serializable]
public struct HitFeedbackData
{
    public Attack_Type attackType;
    public int hitstopFrames;
    public float cameraShakeIntensity;
}

[System.Serializable]
public struct RootMotionData
{
    public Vector3 deltaPosition;
    public Quaternion deltaRotation;
}

[System.Serializable]
public struct HurtboxPreset
{
    public Hurtbox_Type type;
    public CollisionBox[] boxes;
}

public struct HurtInfo
{
    public int damage;
    public int hurtStunFrames;
    public Vector3 pushbackVector;
    public HurtState_Type targetHurtState;
    public bool isHardKnockdown;
}