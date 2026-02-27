using UnityEngine;
public static class HitboxManager
{
    public static bool EvaluateHit(
        Vector3 attackerPos, Vector3 attackerDir, HitboxEvent[] hitboxEvents, int attackerFrame,
        Vector3 defenderPos, Vector3 defenderDir, CollisionBox[] defenderHurtboxes,
        out HitboxEvent successfulHit, out string debugReason)
    {
        successfulHit = default;
        debugReason = string.Empty;

        if (defenderHurtboxes == null || defenderHurtboxes.Length == 0)
        {
            debugReason = "디펜더의 피격박스(Hurtbox) 데이터가 없거나 배열이 비어있습니다.";
            return false;
        }

        if (hitboxEvents == null || hitboxEvents.Length == 0)
        {
            debugReason = "공격자의 타격박스(Hitbox) 데이터가 없습니다.";
            return false;
        }

        Quaternion rotA = attackerDir != Vector3.zero ? Quaternion.LookRotation(attackerDir) : Quaternion.identity;
        Quaternion rotB = defenderDir != Vector3.zero ? Quaternion.LookRotation(defenderDir) : Quaternion.identity;

        bool hasActiveBoxThisFrame = false;
        bool intersectionFailed = false;

        foreach (var evt in hitboxEvents)
        {
            if (TryGetActiveAttackBox(evt, attackerFrame, out CollisionBox attackBox))
            {
                hasActiveBoxThisFrame = true;
                Vector3 worldCenterA = attackerPos + (rotA * attackBox.localPosition);

                for (int i = 0; i < defenderHurtboxes.Length; i++)
                {
                    Vector3 worldCenterB = defenderPos + (rotB * defenderHurtboxes[i].localPosition);

                    if (attackBox.extents == Vector3.zero || defenderHurtboxes[i].extents == Vector3.zero)
                    {
                        debugReason = "박스의 Extents(크기)가 Vector3.zero로 설정되어 판정할 수 없습니다.";
                        continue;
                    }

                    if (CheckOBBIntersection(worldCenterA, attackBox.extents, rotA, worldCenterB, defenderHurtboxes[i].extents, rotB))
                    {
                        successfulHit = evt;
                        return true;
                    }
                    else
                    {
                        intersectionFailed = true;
                    }
                }
            }
        }

        if (!hasActiveBoxThisFrame)
        {
            debugReason = $"현재 프레임({attackerFrame})에 매칭되는 공격 박스(boxPath)를 찾지 못했습니다.";
        }
        else if (intersectionFailed && string.IsNullOrEmpty(debugReason))
        {
            debugReason = "OBB 물리적 교차 판정에서 거리/크기 문제로 빗나갔습니다.";
        }

        return false;
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