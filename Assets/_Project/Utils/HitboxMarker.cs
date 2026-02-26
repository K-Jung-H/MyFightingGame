using UnityEngine;

public class HitboxMarker : MonoBehaviour
{
    public int recordStartFrame;
    public int recordEndFrame;
    public int hitGroupID;
    public Hitbox_Type hitboxType;
    public int damage;
    public Vector3 boxExtents = new Vector3(0.1f, 0.1f, 0.1f);

    [HideInInspector] 
    public int currentPreviewFrame;


    private void OnDrawGizmos()
    {
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