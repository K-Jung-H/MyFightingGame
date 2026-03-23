using UnityEngine;

public class HitboxMarker : MonoBehaviour
{
    public bool isIncludeInBake = true;

    public int recordStartFrame;
    public int recordEndFrame;
    public int hitGroupID;
    
    public Attack_Height attackHeight;
    public Attack_Type attackType;
    public HurtState_Type targetHurtState;
    
    public int damage;
    public int hitstunFrames;
    public int blockStunFrames;
    public Vector3 localPushbackVector;
    public bool isHardKnockdown;
    
    public Vector3 boxExtents = new Vector3(0.1f, 0.1f, 0.1f);

    [HideInInspector] 
    public int currentPreviewFrame;

    private void OnDrawGizmos()
    {
        if (!isIncludeInBake) return;

        bool isActive = currentPreviewFrame >= recordStartFrame && currentPreviewFrame <= recordEndFrame;

        if (isActive)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Gizmos.DrawCube(transform.position, boxExtents * 2f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, boxExtents * 2f);
        }
        else
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireCube(transform.position, boxExtents * 2f);
        }
    }
}