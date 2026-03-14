using UnityEngine;
using System;

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

    public FPVector3 Normalized()
    {
        double xf = x.ToFloat();
        double yf = y.ToFloat();
        double zf = z.ToFloat();
        double mag = Math.Sqrt(xf * xf + yf * yf + zf * zf);

        bool isZero = mag < 0.0001;
        if (isZero)
        {
            return new FPVector3(new FP64(0), new FP64(0), new FP64(0));
        }

        return new FPVector3(
            FP64.FromFloat((float)(xf / mag)),
            FP64.FromFloat((float)(yf / mag)),
            FP64.FromFloat((float)(zf / mag))
        );
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