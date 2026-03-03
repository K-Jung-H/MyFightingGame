using UnityEngine;

public class VfxMarker : MonoBehaviour
{
    public bool isIncludeInBake = true;

    public int recordStartFrame;
    public int recordEndFrame;
    public int intervalFrames;
    public EffectType effectType;
    public HumanBodyBones targetBone;
    public bool isAttached;

    [HideInInspector]
    public int currentPreviewFrame;

    private void OnDrawGizmos()
    {
        if (!isIncludeInBake) return;

        bool isWithinRange = currentPreviewFrame >= recordStartFrame && currentPreviewFrame <= recordEndFrame;
        bool isSpawnFrame = false;

        if (isWithinRange)
        {
            if (intervalFrames <= 0)
            {
                isSpawnFrame = currentPreviewFrame == recordStartFrame;
            }
            else
            {
                isSpawnFrame = (currentPreviewFrame - recordStartFrame) % intervalFrames == 0;
            }
        }

        if (isSpawnFrame)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            Gizmos.DrawSphere(transform.position, 0.05f);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.07f);
        }
        else if (isWithinRange)
        {
            Gizmos.color = new Color(0f, 0.5f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.05f);
        }
    }
}