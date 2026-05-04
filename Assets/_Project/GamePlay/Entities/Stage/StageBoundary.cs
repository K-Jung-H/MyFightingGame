using System;

[Serializable]
public struct BoundaryPlane
{
    public FPVector3 Normal;
    public FP64 Distance;
    public bool isActive;
    public bool isBreakable;
}

[Serializable]
public struct StageBoundary
{
    public BoundaryPlane[] Planes;

    public int TotalWallCount => Planes != null ? Planes.Length : 0;

    public int ActiveWallCount
    {
        get
        {
            if (Planes == null) return 0;
            int count = 0;
            for (int i = 0; i < Planes.Length; i++)
            {
                if (Planes[i].isActive) count++;
            }
            return count;
        }
    }
}