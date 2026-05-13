using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CameraBoundsMarker : MonoBehaviour
{
    [Header("Activation Condition")]
    [Tooltip("이 영역을 개방하기 위해 부서져야 할 벽(Wall) 객체를 연결하세요. 항상 켜져 있는 기본 영역이라면 비워둡니다.")]
    public GameObject targetBreakableWall;

    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = targetBreakableWall == null ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawCube(transform.position + col.center, col.size);
            Gizmos.color = targetBreakableWall == null ? Color.green : new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireCube(transform.position + col.center, col.size);
            
            if (targetBreakableWall != null)
            {
                Gizmos.DrawLine(transform.position, targetBreakableWall.transform.position);
            }
        }
    }
}