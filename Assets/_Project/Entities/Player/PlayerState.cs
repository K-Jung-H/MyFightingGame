using System;

[Flags]
public enum PlayerState_Type
{
    None = 0,
    Idle = 1 << 0,
    Stun = 1 << 1,
    Walking = 1 << 2,
    Running = 1 << 3,
    Sprinting = 1 << 4,
    Attacking = 1 << 5,
    StandHit = 1 << 6,
    AirHit = 1 << 7,
    Knockdown = 1 << 8,
    WakeUp = 1 << 9
}