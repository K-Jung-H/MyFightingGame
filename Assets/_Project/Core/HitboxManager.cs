using UnityEngine;

public static class HitboxManager
{
    public static bool EvaluateHit(
        FPVector3 attackerPos, FPVector3 attackerDir, HitboxEvent[] hitboxEvents, int attackerFrame,
        FPVector3 defenderPos, FPVector3 defenderDir, CollisionBox[] defenderHurtboxes,
        out HitboxEvent successfulHit, out FPVector3 hitPoint, out string debugReason)
    {
        successfulHit = default;
        hitPoint = new FPVector3(new FP64(0), new FP64(0), new FP64(0));
        debugReason = string.Empty;

        if (defenderHurtboxes == null || defenderHurtboxes.Length == 0)
        {
            debugReason = "HurtboxMissing";
            return false;
        }

        if (hitboxEvents == null || hitboxEvents.Length == 0)
        {
            debugReason = "HitboxMissing";
            return false;
        }

        FPVector3[] axesA = GetAxesFromDirection(attackerDir);
        FPVector3[] axesB = GetAxesFromDirection(defenderDir);

        bool hasActiveBoxThisFrame = false;
        bool isIntersectionFailed = false;

        foreach (var evt in hitboxEvents)
        {
            if (TryGetActiveAttackBox(evt, attackerFrame, out CollisionBox attackBox))
            {
                hasActiveBoxThisFrame = true;

                FPVector3 fpAttackLocal = FPVector3.FromVector3(attackBox.localPosition);
                FPVector3 fpAttackExtents = FPVector3.FromVector3(attackBox.extents);
                FPVector3 worldCenterA = attackerPos + TransformLocalToWorld(fpAttackLocal, axesA);

                for (int i = 0; i < defenderHurtboxes.Length; i++)
                {
                    FPVector3 fpHurtLocal = FPVector3.FromVector3(defenderHurtboxes[i].localPosition);
                    FPVector3 fpHurtExtents = FPVector3.FromVector3(defenderHurtboxes[i].extents);
                    FPVector3 worldCenterB = defenderPos + TransformLocalToWorld(fpHurtLocal, axesB);

                    if (fpAttackExtents.x.rawValue == 0 && fpAttackExtents.y.rawValue == 0 && fpAttackExtents.z.rawValue == 0)
                    {
                        debugReason = "ZeroExtents";
                        continue;
                    }

                    if (CheckOBBIntersection(worldCenterA, fpAttackExtents, axesA, worldCenterB, fpHurtExtents, axesB))
                    {
                        successfulHit = evt;
                        hitPoint = CalculateIntersectionCenter(worldCenterA, fpAttackExtents, worldCenterB, fpHurtExtents);
                        return true;
                    }
                    else
                    {
                        isIntersectionFailed = true;
                    }
                }
            }
        }

        if (!hasActiveBoxThisFrame)
        {
            debugReason = "NoActiveBox";
        }
        else if (isIntersectionFailed && string.IsNullOrEmpty(debugReason))
        {
            debugReason = "OBBMiss";
        }

        return false;
    }

    private static FPVector3[] GetAxesFromDirection(FPVector3 dir)
    {
        FPVector3 up = FPVector3.FromVector3(Vector3.up);
        bool isZeroDir = dir.x.rawValue == 0 && dir.y.rawValue == 0 && dir.z.rawValue == 0;
        FPVector3 forward = isZeroDir ? FPVector3.FromVector3(Vector3.forward) : dir;
        FPVector3 right = FPVector3.Cross(up, forward);
        return new FPVector3[] { right, up, forward };
    }

    private static FPVector3 TransformLocalToWorld(FPVector3 localPos, FPVector3[] axes)
    {
        FPVector3 rightPart = axes[0] * localPos.x;
        FPVector3 upPart = axes[1] * localPos.y;
        FPVector3 forwardPart = axes[2] * localPos.z;
        return rightPart + upPart + forwardPart;
    }

    private static FPVector3 CalculateIntersectionCenter(FPVector3 centerA, FPVector3 extentsA, FPVector3 centerB, FPVector3 extentsB)
    {
        FPVector3 minA = centerA - extentsA;
        FPVector3 maxA = centerA + extentsA;
        FPVector3 minB = centerB - extentsB;
        FPVector3 maxB = centerB + extentsB;

        FPVector3 minIntersection = new FPVector3(
            FP64.Max(minA.x, minB.x),
            FP64.Max(minA.y, minB.y),
            FP64.Max(minA.z, minB.z)
        );

        FPVector3 maxIntersection = new FPVector3(
            FP64.Min(maxA.x, maxB.x),
            FP64.Min(maxA.y, maxB.y),
            FP64.Min(maxA.z, maxB.z)
        );

        FP64 half = FP64.FromFloat(0.5f);
        return (minIntersection + maxIntersection) * half;
    }

    private static bool TryGetActiveAttackBox(HitboxEvent hitboxEvent, int currentActionFrame, out CollisionBox activeBox)
    {
        activeBox = default;
        int pathIndex = currentActionFrame - hitboxEvent.activeStartFrame;

        if (pathIndex >= 0 && hitboxEvent.boxPath != null)
        {
            if (pathIndex < hitboxEvent.boxPath.Length)
            {
                activeBox = hitboxEvent.boxPath[pathIndex];
                return true;
            }
        }

        return false;
    }

    private static bool CheckOBBIntersection(FPVector3 centerA, FPVector3 extentsA, FPVector3[] axesA, FPVector3 centerB, FPVector3 extentsB, FPVector3[] axesB)
    {
        FPVector3 v = centerB - centerA;

        for (int i = 0; i < 3; i++)
        {
            FP64 projectionA = GetExtentsComponent(extentsA, i);
            FP64 projectionB = extentsB.x * FP64.Abs(FPVector3.Dot(axesB[0], axesA[i])) +
                               extentsB.y * FP64.Abs(FPVector3.Dot(axesB[1], axesA[i])) +
                               extentsB.z * FP64.Abs(FPVector3.Dot(axesB[2], axesA[i]));

            if (FP64.Abs(FPVector3.Dot(v, axesA[i])).rawValue > (projectionA + projectionB).rawValue)
                return false;
        }

        for (int i = 0; i < 3; i++)
        {
            FP64 projectionA = extentsA.x * FP64.Abs(FPVector3.Dot(axesA[0], axesB[i])) +
                               extentsA.y * FP64.Abs(FPVector3.Dot(axesA[1], axesB[i])) +
                               extentsA.z * FP64.Abs(FPVector3.Dot(axesA[2], axesB[i]));
            FP64 projectionB = GetExtentsComponent(extentsB, i);

            if (FP64.Abs(FPVector3.Dot(v, axesB[i])).rawValue > (projectionA + projectionB).rawValue)
                return false;
        }

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                FPVector3 cross = FPVector3.Cross(axesA[i], axesB[j]);

                if (cross.x.rawValue == 0 && cross.y.rawValue == 0 && cross.z.rawValue == 0) continue;

                FP64 projectionA = extentsA.x * FP64.Abs(FPVector3.Dot(axesA[0], cross)) +
                                   extentsA.y * FP64.Abs(FPVector3.Dot(axesA[1], cross)) +
                                   extentsA.z * FP64.Abs(FPVector3.Dot(axesA[2], cross));

                FP64 projectionB = extentsB.x * FP64.Abs(FPVector3.Dot(axesB[0], cross)) +
                                   extentsB.y * FP64.Abs(FPVector3.Dot(axesB[1], cross)) +
                                   extentsB.z * FP64.Abs(FPVector3.Dot(axesB[2], cross));

                if (FP64.Abs(FPVector3.Dot(v, cross)).rawValue > (projectionA + projectionB).rawValue)
                    return false;
            }
        }

        return true;
    }

    private static FP64 GetExtentsComponent(FPVector3 extents, int index)
    {
        if (index == 0) return extents.x;
        if (index == 1) return extents.y;
        return extents.z;
    }
}