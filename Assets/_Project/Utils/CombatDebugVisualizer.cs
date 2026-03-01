using UnityEngine;

public class CombatDebugVisualizer : MonoBehaviour
{
    [SerializeField] private GameLoopManager gameLoopManager;
    
    [Header("Display Target")]
    [SerializeField] private bool isShowingInSceneView = true;
    [SerializeField] private bool isShowingInGameView = true;

    [Header("Display Options")]
    [SerializeField] private bool isShowingHurtboxes = true;
    [SerializeField] private bool isShowingHitboxes = true;
    [SerializeField] private bool isShowingLookDirection = true;

    private Material debugMaterial;

    private void Awake()
    {
        CreateDebugMaterial();
    }

    private void CreateDebugMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        debugMaterial = new Material(shader);
        debugMaterial.hideFlags = HideFlags.HideAndDontSave;
        debugMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        debugMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        debugMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        debugMaterial.SetInt("_ZWrite", 0);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || gameLoopManager == null || !isShowingInSceneView) return;

        PlayerStateMachine playerOne = gameLoopManager.GetPlayerOneStateMachine();
        PlayerStateMachine playerTwo = gameLoopManager.GetPlayerTwoStateMachine();

        if (playerOne != null) DrawGizmosForPlayer(playerOne);
        if (playerTwo != null) DrawGizmosForPlayer(playerTwo);
    }

    private void OnRenderObject()
    {
        if (!Application.isPlaying || gameLoopManager == null || !isShowingInGameView) return;

        PlayerStateMachine playerOne = gameLoopManager.GetPlayerOneStateMachine();
        PlayerStateMachine playerTwo = gameLoopManager.GetPlayerTwoStateMachine();

        debugMaterial.SetPass(0);
        GL.PushMatrix();

        if (playerOne != null) DrawGLForPlayer(playerOne);
        if (playerTwo != null) DrawGLForPlayer(playerTwo);

        GL.PopMatrix();
    }

    private void DrawGizmosForPlayer(PlayerStateMachine stateMachine)
    {
        if (isShowingLookDirection) DrawGizmoLookDirection(stateMachine);
        if (isShowingHurtboxes) DrawGizmoHurtboxes(stateMachine);
        if (isShowingHitboxes) DrawGizmoHitboxes(stateMachine);
    }

    private void DrawGLForPlayer(PlayerStateMachine stateMachine)
    {
        if (isShowingLookDirection) DrawGLLookDirection(stateMachine);
        if (isShowingHurtboxes) DrawGLHurtboxes(stateMachine);
        if (isShowingHitboxes) DrawGLHitboxes(stateMachine);
    }

    private void DrawGizmoLookDirection(PlayerStateMachine stateMachine)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(stateMachine.GetPosition() + Vector3.up * 0.1f, stateMachine.GetLookDirection() * 2f);
    }

    private void DrawGizmoHurtboxes(PlayerStateMachine stateMachine)
    {
        Hurtbox_Type currentType = stateMachine.GetCurrentHurtboxType();
        PlayerConfigSO config = stateMachine.GetPlayerConfig();
        
        if (config == null) return;

        CollisionBox[] boxes = config.GetHurtboxBoxes(currentType);
        if (boxes == null || boxes.Length == 0) return;

        Vector3 position = stateMachine.GetPosition();
        Vector3 lookDirection = stateMachine.GetLookDirection();
        Quaternion rotation = lookDirection != Vector3.zero ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Color hurtboxColor = GetHurtboxColor(currentType);

        foreach (var box in boxes)
        {
            Gizmos.color = new Color(hurtboxColor.r, hurtboxColor.g, hurtboxColor.b, 0.4f);
            Gizmos.DrawCube(box.localPosition, box.extents * 2f);
            
            Gizmos.color = hurtboxColor;
            Gizmos.DrawWireCube(box.localPosition, box.extents * 2f);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawGizmoHitboxes(PlayerStateMachine stateMachine)
    {
        if (stateMachine.GetCurrentState() != PlayerState_Type.Attacking) return;

        ActionDataSO actionData = stateMachine.GetCurrentActionData();
        if (actionData == null || actionData.frameData.hitboxEvents == null) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        Vector3 position = stateMachine.GetPosition();
        Vector3 lookDirection = stateMachine.GetLookDirection();
        Quaternion rotation = lookDirection != Vector3.zero ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);

        foreach (var hitEvent in actionData.frameData.hitboxEvents)
        {
            int pathIndex = currentFrame - hitEvent.activeStartFrame;

            if (pathIndex >= 0 && hitEvent.boxPath != null && pathIndex < hitEvent.boxPath.Length)
            {
                CollisionBox activeBox = hitEvent.boxPath[pathIndex];

                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawCube(activeBox.localPosition, activeBox.extents * 2f);
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(activeBox.localPosition, activeBox.extents * 2f);
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawGLLookDirection(PlayerStateMachine stateMachine)
    {
        Vector3 startPosition = stateMachine.GetPosition() + Vector3.up * 0.1f;
        Vector3 endPosition = startPosition + stateMachine.GetLookDirection() * 2f;
        DrawGLLine(startPosition, endPosition, Color.yellow);
    }

    private void DrawGLHurtboxes(PlayerStateMachine stateMachine)
    {
        Hurtbox_Type currentType = stateMachine.GetCurrentHurtboxType();
        PlayerConfigSO config = stateMachine.GetPlayerConfig();

        if (config == null) return;

        CollisionBox[] boxes = config.GetHurtboxBoxes(currentType);
        if (boxes == null || boxes.Length == 0) return;

        Vector3 position = stateMachine.GetPosition();
        Vector3 lookDirection = stateMachine.GetLookDirection();
        Quaternion rotation = lookDirection != Vector3.zero ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;
        Color hurtboxColor = GetHurtboxColor(currentType);

        foreach (var box in boxes)
        {
            DrawGLWireCube(position, rotation, box.localPosition, box.extents, hurtboxColor);
        }
    }

    private void DrawGLHitboxes(PlayerStateMachine stateMachine)
    {
        if (stateMachine.GetCurrentState() != PlayerState_Type.Attacking) return;

        ActionDataSO actionData = stateMachine.GetCurrentActionData();
        if (actionData == null || actionData.frameData.hitboxEvents == null) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        Vector3 position = stateMachine.GetPosition();
        Vector3 lookDirection = stateMachine.GetLookDirection();
        Quaternion rotation = lookDirection != Vector3.zero ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        foreach (var hitEvent in actionData.frameData.hitboxEvents)
        {
            int pathIndex = currentFrame - hitEvent.activeStartFrame;

            if (pathIndex >= 0 && hitEvent.boxPath != null && pathIndex < hitEvent.boxPath.Length)
            {
                CollisionBox activeBox = hitEvent.boxPath[pathIndex];
                DrawGLWireCube(position, rotation, activeBox.localPosition, activeBox.extents, Color.red);
            }
        }
    }

    private void DrawGLWireCube(Vector3 worldPosition, Quaternion worldRotation, Vector3 localPosition, Vector3 extents, Color color)
    {
        Vector3 center = worldPosition + (worldRotation * localPosition);

        Vector3 point0 = center + worldRotation * new Vector3(-extents.x, -extents.y, -extents.z);
        Vector3 point1 = center + worldRotation * new Vector3(extents.x, -extents.y, -extents.z);
        Vector3 point2 = center + worldRotation * new Vector3(extents.x, -extents.y, extents.z);
        Vector3 point3 = center + worldRotation * new Vector3(-extents.x, -extents.y, extents.z);
        Vector3 point4 = center + worldRotation * new Vector3(-extents.x, extents.y, -extents.z);
        Vector3 point5 = center + worldRotation * new Vector3(extents.x, extents.y, -extents.z);
        Vector3 point6 = center + worldRotation * new Vector3(extents.x, extents.y, extents.z);
        Vector3 point7 = center + worldRotation * new Vector3(-extents.x, extents.y, extents.z);

        GL.Begin(GL.LINES);
        GL.Color(color);

        GL.Vertex(point0); GL.Vertex(point1);
        GL.Vertex(point1); GL.Vertex(point2);
        GL.Vertex(point2); GL.Vertex(point3);
        GL.Vertex(point3); GL.Vertex(point0);

        GL.Vertex(point4); GL.Vertex(point5);
        GL.Vertex(point5); GL.Vertex(point6);
        GL.Vertex(point6); GL.Vertex(point7);
        GL.Vertex(point7); GL.Vertex(point4);

        GL.Vertex(point0); GL.Vertex(point4);
        GL.Vertex(point1); GL.Vertex(point5);
        GL.Vertex(point2); GL.Vertex(point6);
        GL.Vertex(point3); GL.Vertex(point7);

        GL.End();
    }

    private void DrawGLLine(Vector3 start, Vector3 end, Color color)
    {
        GL.Begin(GL.LINES);
        GL.Color(color);
        GL.Vertex(start);
        GL.Vertex(end);
        GL.End();
    }

    private Color GetHurtboxColor(Hurtbox_Type type)
    {
        switch (type)
        {
            case Hurtbox_Type.Standing: return new Color(0.2f, 0.6f, 0.8f);
            case Hurtbox_Type.Crouching: return new Color(0.8f, 0.5f, 0.2f);
            case Hurtbox_Type.Airborne: return new Color(0.6f, 0.2f, 0.8f);
            case Hurtbox_Type.Invincible: return new Color(0.9f, 0.9f, 0.9f);
            default: return Color.gray;
        }
    }
}