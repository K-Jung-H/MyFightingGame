using System;

[Serializable]
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

    public static FP64 Sqrt(FP64 a)
    {
        if (a.rawValue <= 0) return new FP64(0);
        
        ulong num = (ulong)a.rawValue;
        ulong res = 0;
        ulong bit = 1UL << 62;
        
        while (bit > num)
        {
            bit >>= 2;
        }
        
        while (bit != 0)
        {
            if (num >= res + bit)
            {
                num -= res + bit;
                res = (res >> 1) + bit;
            }
            else
            {
                res >>= 1;
            }
            bit >>= 2;
        }
        
        return new FP64((long)res << (fractionalBits / 2));
    }
    
    public static FP64 operator -(FP64 value)
    {
        return new FP64(-value.rawValue);
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

    public static FP64 operator /(FP64 a, FP64 b)
    {
        if (b.rawValue == 0) return new FP64(0);
        long temp = a.rawValue << fractionalBits;
        return new FP64(temp / b.rawValue);
    }
}