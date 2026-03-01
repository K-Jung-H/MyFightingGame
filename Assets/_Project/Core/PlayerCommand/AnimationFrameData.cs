using UnityEngine;

[System.Serializable]
public struct CollisionBox
{
    public Vector3 localPosition;
    public Vector3 extents;
}

public enum Hurtbox_Type
{
    Standing,
    Crouching,
    Airborne,
    Invincible,
} 

public enum Attack_Type
{
    Normal,
    Crash
}

public enum HurtState_Type
{
    StandHit,
    AirHit,
    KnockDown,
    GroundHit,
    GuardHit
}

public enum EffectType
{
    None = 0,
    Hit,
    ChargeSparkLight,
    ChargeSparkHeavy,
}

[System.Serializable]
public struct HurtboxPreset
{
    public Hurtbox_Type type;
    public CollisionBox[] boxes;
}

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
    public int activeStartFrame;
    public CollisionBox[] boxPath;
    public int hitGroupID;
    public Attack_Type attackType;
    public HurtState_Type targetHurtState;
    public int damage;
    public int hitstunFrames;
    public Vector3 localPushbackVector;
    public bool isHardKnockdown;
}

[System.Serializable]
public struct VfxEvent
{
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
}

[System.Serializable]
public class AnimationFrameData
{
    public ActionLogicData logicData;
    public HitboxEvent[] hitboxEvents;
    public HurtboxEvent[] hurtboxEvents;
    public VfxEvent[] vfxEvents;
}

public struct HurtInfo
{
    public int damage;
    public int hurtStunFrames;
    public Vector3 pushbackVector;
    public HurtState_Type targetHurtState;
    public bool isHardKnockdown;
}