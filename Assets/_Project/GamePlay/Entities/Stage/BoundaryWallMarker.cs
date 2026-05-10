using UnityEngine;

[ExecuteInEditMode]
public class BoundaryWallMarker : MonoBehaviour
{
    public bool isActive = true;
    public bool isBreakable = false;
    public int durability = 0;
    public float explosionForce = 15f;
    public float visualWidth = 20f;
    public float visualHeight = 5f;

    private static Mesh _quadMesh;

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            DrawWallPlane(true);
        }
    }

    public void DrawWallPlane(bool isGizmo)
    {
        Vector3 center = transform.position + Vector3.up * (visualHeight * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(transform.forward);
        
        Vector3 right = rotation * Vector3.right * (visualWidth * 0.5f);
        Vector3 up = Vector3.up * (visualHeight * 0.5f);

        Vector3 p0 = center - right - up;
        Vector3 p1 = center + right - up;
        Vector3 p2 = center + right + up;
        Vector3 p3 = center - right + up;

        Color planeColorBase;
        Color wireColorBase;

        if (!isActive)
        {
            planeColorBase = Color.gray;
            wireColorBase = Color.gray;
        }
        else
        {
            planeColorBase = isBreakable ? new Color(1f, 0.5f, 0f) : Color.cyan;
            wireColorBase = isBreakable ? new Color(1f, 0.7f, 0.2f) : Color.cyan;
        }

        Color wallFaceColor = new Color(planeColorBase.r, planeColorBase.g, planeColorBase.b, 0.3f);
        Color wallWireColor = wireColorBase;

        if (isGizmo)
        {
            Gizmos.color = wallFaceColor;
            Quaternion meshRotation = rotation * Quaternion.Euler(0, 180, 0);
            Gizmos.DrawMesh(GetQuadMesh(), center, meshRotation, new Vector3(visualWidth, visualHeight, 1f));
            
            Gizmos.color = wallWireColor;
            Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(center, transform.forward * 2f);
        }
    }

    private static Mesh GetQuadMesh()
    {
        if (_quadMesh == null)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quadMesh = quad.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(quad);
            else DestroyImmediate(quad);
        }
        return _quadMesh;
    }
}