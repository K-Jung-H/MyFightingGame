using UnityEngine;

public enum HitState_Type
{
    StandHit,
    AirHit,
    KnockDown,
    GroundHit,
    GuardHit
}

public struct HitInfo
{
    public int damage;
    public int hitstunFrames;
    public Vector3 pushbackVector;
    public HitState_Type hitType;
    public bool isHardKnockdown;
}