using System;

[Flags]
public enum InputFlags : byte
{
    None = 0,
    Up = 1 << 0,
    Down = 1 << 1,
    Forward = 1 << 2,
    Back = 1 << 3,
    LP = 1 << 4,
    RP = 1 << 5,
    LK = 1 << 6,
    RK = 1 << 7
}

public struct PlayerInput
{
    public int frame;
    public InputFlags flags;
}

public static class PacketManager
{
    public static InputFlags CreateFlags(bool isUp, bool isDown, bool isForward, bool isBack, bool isLP, bool isRP, bool isLK, bool isRK)
    {
        InputFlags currentFlags = InputFlags.None;

        if (isUp) currentFlags |= InputFlags.Up;
        if (isDown) currentFlags |= InputFlags.Down;
        if (isForward) currentFlags |= InputFlags.Forward;
        if (isBack) currentFlags |= InputFlags.Back;
        if (isLP) currentFlags |= InputFlags.LP;
        if (isRP) currentFlags |= InputFlags.RP;
        if (isLK) currentFlags |= InputFlags.LK;
        if (isRK) currentFlags |= InputFlags.RK;

        return currentFlags;
    }
}