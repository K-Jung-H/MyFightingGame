using System;

[Flags]
public enum PlayerState_Type
{
    None = 0,
    Idle = 1 << 0,
    Walking = 1 << 1,
    Running = 1 << 2,
    Sprinting = 1 << 3,
    Attacking = 1 << 4,
    StandHit = 1 << 5,
    AirHit = 1 << 6,
    Stunning = 1 << 7,
    GroundSmash = 1 << 8,
    LayingDown = 1 << 9,
    WakeUp = 1 << 10
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