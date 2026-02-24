using System;

[Flags]
public enum InputFlags : byte
{
    None = 0,
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    LightAttack = 1 << 4,
    HeavyAttack = 1 << 5,
    Jump = 1 << 6,
    Guard = 1 << 7
}

public struct PlayerInput
{
    public int frame;
    public InputFlags flags;
}

public static class PacketManager
{
    public static InputFlags CreateFlags(bool isUp, bool isDown, bool isLeft, bool isRight, bool isLight, bool isHeavy)
    {
        InputFlags currentFlags = InputFlags.None;

        if (isUp) currentFlags |= InputFlags.Up;
        if (isDown) currentFlags |= InputFlags.Down;
        if (isLeft) currentFlags |= InputFlags.Left;
        if (isRight) currentFlags |= InputFlags.Right;
        if (isLight) currentFlags |= InputFlags.LightAttack;
        if (isHeavy) currentFlags |= InputFlags.HeavyAttack;

        return currentFlags;
    }


    public static byte[] EncodeInput(PlayerInput playerInput)
    {
        byte[] packetBytes = new byte[5];
        byte[] frameBytes = BitConverter.GetBytes(playerInput.frame);
        
        Buffer.BlockCopy(frameBytes, 0, packetBytes, 0, 4);
        packetBytes[4] = (byte)playerInput.flags;
        
        return packetBytes;
    }

    public static PlayerInput DecodeInput(byte[] packetBytes)
    {
        PlayerInput decodedInput = new PlayerInput();
        
        decodedInput.frame = BitConverter.ToInt32(packetBytes, 0);
        decodedInput.flags = (InputFlags)packetBytes[4];
        
        return decodedInput;
    }
}