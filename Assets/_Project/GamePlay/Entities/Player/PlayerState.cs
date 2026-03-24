using System;

[Flags]
public enum PlayerState_Type
{
    None = 0,
    Idle = 1 << 0,
    Crouching = 1 << 1,

    Walking = 1 << 2,
    Running = 1 << 3,
    Sprinting = 1 << 4,
    SideStep = 1 << 5,
    SideWalk = 1 << 6,

    Attacking = 1 << 7,
    GroundSmash = 1 << 8,

    StandBlock = 1 << 9,
    CrouchBlock = 1 << 10,

    StandHit = 1 << 11,
    CrouchHit = 1 << 12,
    AirHit = 1 << 13,
    Stunning = 1 << 14,

    LayingDown = 1 << 15,
    WakeUp = 1 << 16,

    Dead = 1 << 17, 
    
    Win = 1 << 18,
    Defeat = 1 << 19,
}

public enum WakeUp_Type
{
    InPlace,
    RollForward,
    RollBackward,
    RollLeft,
    RollRight,
    Attack
}