using System;

public struct FP64
{
    public const int fractionalBits = 16;
    public const long oneRaw = 1L << fractionalBits;

    public long rawValue;

    public FP64(long rawValue)
    {
        this.rawValue = rawValue;
    }

    public static FP64 FromFloat(float value)
    {
        return new FP64((long)Math.Round(value * oneRaw));
    }

    public float ToFloat()
    {
        return (float)rawValue / oneRaw;
    }

    public static FP64 Abs(FP64 a)
    {
        return new FP64(Math.Abs(a.rawValue));
    }

    public static FP64 Max(FP64 a, FP64 b)
    {
        return a.rawValue > b.rawValue ? a : b;
    }

    public static FP64 Min(FP64 a, FP64 b)
    {
        return a.rawValue < b.rawValue ? a : b;
    }

    public static FP64 operator +(FP64 a, FP64 b)
    {
        return new FP64(a.rawValue + b.rawValue);
    }

    public static FP64 operator -(FP64 a, FP64 b)
    {
        return new FP64(a.rawValue - b.rawValue);
    }

    public static FP64 operator *(FP64 a, FP64 b)
    {
        return new FP64((a.rawValue * b.rawValue) >> fractionalBits);
    }
}