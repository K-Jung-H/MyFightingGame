using UnityEngine;

public static class HitboxManager
{
    public static bool EvaluateHit(
        Vector3 attackerPos, Vector3 attackerDir, HitboxEvent[] hitboxEvents, int attackerFrame,
        Vector3 defenderPos, Vector3 defenderDir, CollisionBox[] defenderHurtboxes,
        out HitboxEvent successfulHit)
    {
        successfulHit = default;

        if (defenderHurtboxes == null || defenderHurtboxes.Length == 0 || hitboxEvents == null || hitboxEvents.Length == 0)
        {
            return false;
        }

        Quaternion rotA = attackerDir != Vector3.zero ? Quaternion.LookRotation(attackerDir) : Quaternion.identity;
        Quaternion rotB = defenderDir != Vector3.zero ? Quaternion.LookRotation(defenderDir) : Quaternion.identity;

        foreach (var evt in hitboxEvents)
        {
            if (TryGetActiveAttackBox(evt, attackerFrame, out CollisionBox attackBox))
            {
                Vector3 worldCenterA = attackerPos + (rotA * attackBox.localPosition);

                for (int i = 0; i < defenderHurtboxes.Length; i++)
                {
                    Vector3 worldCenterB = defenderPos + (rotB * defenderHurtboxes[i].localPosition);

                    if (CheckOBBIntersection(worldCenterA, attackBox.extents, rotA, worldCenterB, defenderHurtboxes[i].extents, rotB))
                    {
                        successfulHit = evt;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryGetActiveAttackBox(HitboxEvent hitboxEvent, int currentActionFrame, out CollisionBox activeBox)
    {
        activeBox = default;
        int pathIndex = currentActionFrame - hitboxEvent.activeStartFrame;

        if (pathIndex >= 0 && pathIndex < hitboxEvent.boxPath.Length)
        {
            activeBox = hitboxEvent.boxPath[pathIndex];
            return true;
        }

        return false;
    }

    private static bool CheckOBBIntersection(Vector3 centerA, Vector3 extentsA, Quaternion rotA, Vector3 centerB, Vector3 extentsB, Quaternion rotB)
    {
        Vector3[] axesA = new Vector3[] { rotA * Vector3.right, rotA * Vector3.up, rotA * Vector3.forward };
        Vector3[] axesB = new Vector3[] { rotB * Vector3.right, rotB * Vector3.up, rotB * Vector3.forward };

        Vector3 v = centerB - centerA;

        for (int i = 0; i < 3; i++)
        {
            float projectionA = extentsA[i];
            float projectionB = extentsB.x * Mathf.Abs(Vector3.Dot(axesB[0], axesA[i])) +
                                extentsB.y * Mathf.Abs(Vector3.Dot(axesB[1], axesA[i])) +
                                extentsB.z * Mathf.Abs(Vector3.Dot(axesB[2], axesA[i]));

            if (Mathf.Abs(Vector3.Dot(v, axesA[i])) > projectionA + projectionB)
                return false;
        }

        for (int i = 0; i < 3; i++)
        {
            float projectionA = extentsA.x * Mathf.Abs(Vector3.Dot(axesA[0], axesB[i])) +
                                extentsA.y * Mathf.Abs(Vector3.Dot(axesA[1], axesB[i])) +
                                extentsA.z * Mathf.Abs(Vector3.Dot(axesA[2], axesB[i]));
            float projectionB = extentsB[i];

            if (Mathf.Abs(Vector3.Dot(v, axesB[i])) > projectionA + projectionB)
                return false;
        }

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Vector3 cross = Vector3.Cross(axesA[i], axesB[j]);
                
                if (cross.sqrMagnitude < 0.0001f) continue;

                float projectionA = extentsA.x * Mathf.Abs(Vector3.Dot(axesA[0], cross)) +
                                    extentsA.y * Mathf.Abs(Vector3.Dot(axesA[1], cross)) +
                                    extentsA.z * Mathf.Abs(Vector3.Dot(axesA[2], cross));
                                    
                float projectionB = extentsB.x * Mathf.Abs(Vector3.Dot(axesB[0], cross)) +
                                    extentsB.y * Mathf.Abs(Vector3.Dot(axesB[1], cross)) +
                                    extentsB.z * Mathf.Abs(Vector3.Dot(axesB[2], cross));

                if (Mathf.Abs(Vector3.Dot(v, cross)) > projectionA + projectionB)
                    return false;
            }
        }

        return true;
    }
}