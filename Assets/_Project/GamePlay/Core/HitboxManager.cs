using UnityEngine;

public static class HitboxManager
{
    public static bool EvaluateHit(
        FPVector3 attackerPos, FPVector3 attackerDir, FPHitboxEvent[] hitboxEvents, int attackerFrame,
        FPVector3 defenderPos, FPVector3 defenderDir, FPCollisionBox[] defenderHurtboxes,
        out FPHitboxEvent successfulHit, out FPVector3 hitPoint)
    {
        successfulHit = default;
        hitPoint = new FPVector3(new FP64(0), new FP64(0), new FP64(0));

        GetAxesFromDirection(attackerDir, out FPAxisSet axesA);
        GetAxesFromDirection(defenderDir, out FPAxisSet axesB);

        for (int e = 0; e < hitboxEvents.Length; e++)
        {
            FPHitboxEvent evt = hitboxEvents[e];
            bool isAttackBoxActive = TryGetActiveAttackBox(evt, attackerFrame, out FPCollisionBox attackBox);
            
            if (isAttackBoxActive)
            {
                FPVector3 worldCenterA = attackerPos + TransformLocalToWorld(attackBox.localPosition, ref axesA);

                for (int i = 0; i < defenderHurtboxes.Length; i++)
                {
                    FPVector3 worldCenterB = defenderPos + TransformLocalToWorld(defenderHurtboxes[i].localPosition, ref axesB);

                    bool isZeroExtents = attackBox.extents.x.rawValue == 0 && attackBox.extents.y.rawValue == 0 && attackBox.extents.z.rawValue == 0;
                    if (isZeroExtents) continue;

                    bool isIntersecting = CheckOBBIntersection(worldCenterA, attackBox.extents, ref axesA, worldCenterB, defenderHurtboxes[i].extents, ref axesB);
                    if (isIntersecting)
                    {
                        successfulHit = evt;
                        hitPoint = CalculateIntersectionCenter(worldCenterA, attackBox.extents, worldCenterB, defenderHurtboxes[i].extents);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static void GetAxesFromDirection(FPVector3 dir, out FPAxisSet axisSet)
    {
        FPVector3 upVector = FPVector3.FromVector3(Vector3.up);
        bool isZeroDir = dir.x.rawValue == 0 && dir.y.rawValue == 0 && dir.z.rawValue == 0;
        FPVector3 forwardVector = isZeroDir ? FPVector3.FromVector3(Vector3.forward) : dir;
        FPVector3 rightVector = FPVector3.Cross(upVector, forwardVector);

        axisSet = new FPAxisSet
        {
            right = rightVector,
            up = upVector,
            forward = forwardVector
        };
    }

    private static FPVector3 TransformLocalToWorld(FPVector3 localPos, ref FPAxisSet axes)
    {
        FPVector3 rightPart = axes.right * localPos.x;
        FPVector3 upPart = axes.up * localPos.y;
        FPVector3 forwardPart = axes.forward * localPos.z;
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

    private static bool TryGetActiveAttackBox(FPHitboxEvent hitboxEvent, int currentActionFrame, out FPCollisionBox activeBox)
    {
        activeBox = default;
        int pathIndex = currentActionFrame - hitboxEvent.activeStartFrame;

        if (pathIndex >= 0 && hitboxEvent.boxPath != null && pathIndex < hitboxEvent.boxPath.Length)
        {
            activeBox = hitboxEvent.boxPath[pathIndex];
            return true;
        }
        return false;
    }

    private static bool CheckOBBIntersection(FPVector3 centerA, FPVector3 extentsA, ref FPAxisSet axesA, FPVector3 centerB, FPVector3 extentsB, ref FPAxisSet axesB)
    {
        FPVector3 distanceVector = centerB - centerA;

        FPVector3[] arrA = { axesA.right, axesA.up, axesA.forward };
        FPVector3[] arrB = { axesB.right, axesB.up, axesB.forward };

        for (int i = 0; i < 3; i++)
        {
            FP64 projectionA = GetExtentsComponent(extentsA, i);
            FP64 projectionB = extentsB.x * FP64.Abs(FPVector3.Dot(arrB[0], arrA[i])) +
                               extentsB.y * FP64.Abs(FPVector3.Dot(arrB[1], arrA[i])) +
                               extentsB.z * FP64.Abs(FPVector3.Dot(arrB[2], arrA[i]));

            bool isSeparated = FP64.Abs(FPVector3.Dot(distanceVector, arrA[i])).rawValue > (projectionA + projectionB).rawValue;
            if (isSeparated) return false;
        }

        for (int i = 0; i < 3; i++)
        {
            FP64 projectionA = extentsA.x * FP64.Abs(FPVector3.Dot(arrA[0], arrB[i])) +
                               extentsA.y * FP64.Abs(FPVector3.Dot(arrA[1], arrB[i])) +
                               extentsA.z * FP64.Abs(FPVector3.Dot(arrA[2], arrB[i]));
            FP64 projectionB = GetExtentsComponent(extentsB, i);

            bool isSeparated = FP64.Abs(FPVector3.Dot(distanceVector, arrB[i])).rawValue > (projectionA + projectionB).rawValue;
            if (isSeparated) return false;
        }

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                FPVector3 cross = FPVector3.Cross(arrA[i], arrB[j]);

                bool isParallel = cross.x.rawValue == 0 && cross.y.rawValue == 0 && cross.z.rawValue == 0;
                if (isParallel) continue;

                FP64 projectionA = extentsA.x * FP64.Abs(FPVector3.Dot(arrA[0], cross)) +
                                   extentsA.y * FP64.Abs(FPVector3.Dot(arrA[1], cross)) +
                                   extentsA.z * FP64.Abs(FPVector3.Dot(arrA[2], cross));

                FP64 projectionB = extentsB.x * FP64.Abs(FPVector3.Dot(arrB[0], cross)) +
                                   extentsB.y * FP64.Abs(FPVector3.Dot(arrB[1], cross)) +
                                   extentsB.z * FP64.Abs(FPVector3.Dot(arrB[2], cross));

                bool isSeparated = FP64.Abs(FPVector3.Dot(distanceVector, cross)).rawValue > (projectionA + projectionB).rawValue;
                if (isSeparated) return false;
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