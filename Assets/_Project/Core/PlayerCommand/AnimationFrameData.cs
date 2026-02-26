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

public enum Hitbox_Type
{
    Normal,
    Airborne,
    KnockBack,
    Crash
}


[System.Serializable]
public struct HitboxEvent
{
    public int activeStartFrame;
    public CollisionBox[] boxPath;
    public int hitGroupID;
    public Hitbox_Type hitboxType;
    public int damage;
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
}

