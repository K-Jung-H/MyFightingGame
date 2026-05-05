using UnityEngine;
using System;

[Serializable]
public struct FPVector3
{
    public FP64 x;
    public FP64 y;
    public FP64 z;

    public FPVector3(FP64 x, FP64 y, FP64 z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static FPVector3 FromVector3(Vector3 vec)
    {
        return new FPVector3(FP64.FromFloat(vec.x), FP64.FromFloat(vec.y), FP64.FromFloat(vec.z));
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x.ToFloat(), y.ToFloat(), z.ToFloat());
    }

    public FP64 Magnitude()
    {
        FP64 x2 = x * x;
        FP64 y2 = y * y;
        FP64 z2 = z * z;
        return FP64.Sqrt(x2 + y2 + z2);
    }

    public FPVector3 Normalized()
    {
        FP64 mag = Magnitude();
        
        if (mag.rawValue == 0)
        {
            return new FPVector3(new FP64(0), new FP64(0), new FP64(0));
        }

        return new FPVector3(x / mag, y / mag, z / mag);
    }

    public static FP64 Dot(FPVector3 a, FPVector3 b)
    {
        return (a.x * b.x) + (a.y * b.y) + (a.z * b.z);
    }

    public static FPVector3 Cross(FPVector3 a, FPVector3 b)
    {
        return new FPVector3(
            (a.y * b.z) - (a.z * b.y),
            (a.z * b.x) - (a.x * b.z),
            (a.x * b.y) - (a.y * b.x)
        );
    }

    public static FPVector3 operator +(FPVector3 a, FPVector3 b)
    {
        return new FPVector3(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    public static FPVector3 operator -(FPVector3 a, FPVector3 b)
    {
        return new FPVector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public static FPVector3 operator *(FPVector3 a, FP64 b)
    {
        return new FPVector3(a.x * b, a.y * b, a.z * b);
    }
}

public struct FPAxisSet
{
    public FPVector3 right;
    public FPVector3 up;
    public FPVector3 forward;
}