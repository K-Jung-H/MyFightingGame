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
        bool isPlaying = Application.isPlaying;
        bool hasManager = gameLoopManager != null;
        bool canDraw = isPlaying && hasManager && isShowingInSceneView;
        if (!canDraw) return;

        PlayerController playerOne = gameLoopManager.GetPlayerOneController();
        PlayerController playerTwo = gameLoopManager.GetPlayerTwoController();

        bool hasPlayerOne = playerOne != null;
        if (hasPlayerOne) DrawGizmosForPlayer(playerOne);
        
        bool hasPlayerTwo = playerTwo != null;
        if (hasPlayerTwo) DrawGizmosForPlayer(playerTwo);
    }

    private void OnRenderObject()
    {
        bool isPlaying = Application.isPlaying;
        bool hasManager = gameLoopManager != null;
        bool canDraw = isPlaying && hasManager && isShowingInGameView;
        if (!canDraw) return;

        PlayerController playerOne = gameLoopManager.GetPlayerOneController();
        PlayerController playerTwo = gameLoopManager.GetPlayerTwoController();

        debugMaterial.SetPass(0);
        GL.PushMatrix();

        bool hasPlayerOne = playerOne != null;
        if (hasPlayerOne) DrawGLForPlayer(playerOne);
        
        bool hasPlayerTwo = playerTwo != null;
        if (hasPlayerTwo) DrawGLForPlayer(playerTwo);

        GL.PopMatrix();
    }

    private void DrawGizmosForPlayer(PlayerController controller)
    {
        if (isShowingLookDirection) DrawGizmoLookDirection(controller);
        if (isShowingHurtboxes) DrawGizmoHurtboxes(controller);
        if (isShowingHitboxes) DrawGizmoHitboxes(controller);
    }

    private void DrawGLForPlayer(PlayerController controller)
    {
        if (isShowingLookDirection) DrawGLLookDirection(controller);
        if (isShowingHurtboxes) DrawGLHurtboxes(controller);
        if (isShowingHitboxes) DrawGLHitboxes(controller);
    }

    private void DrawGizmoLookDirection(PlayerController controller)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(controller.GetPhysics().GetPosition() + Vector3.up * 0.1f, controller.GetPhysics().GetLookDirection() * 2f);
    }

    private void DrawGizmoHurtboxes(PlayerController controller)
    {
        Hurtbox_Type currentType = Hurtbox_Type.Standing; 
        PlayerConfigSO config = controller.GetConfig();
        
        bool hasConfig = config != null;
        if (!hasConfig) return;

        CollisionBox[] boxes = config.GetHurtboxBoxes(currentType);
        bool hasBoxes = boxes != null && boxes.Length > 0;
        if (!hasBoxes) return;

        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        bool isLookDirectionValid = lookDirection != Vector3.zero;
        Quaternion rotation = isLookDirectionValid ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

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

    private void DrawGizmoHitboxes(PlayerController controller)
    {
        PlayerStateMachine stateMachine = controller.GetStateMachine();
        bool isAttacking = stateMachine.GetCurrentState() == PlayerState_Type.Attacking;
        if (!isAttacking) return;

        ActionDataSO actionData = stateMachine.GetCurrentActionData();
        bool isActionDataValid = actionData != null && actionData.frameData.hitboxEvents != null;
        if (!isActionDataValid) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        bool isLookDirectionValid = lookDirection != Vector3.zero;
        Quaternion rotation = isLookDirectionValid ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);

        foreach (var hitEvent in actionData.frameData.hitboxEvents)
        {
            int pathIndex = currentFrame - hitEvent.activeStartFrame;

            bool isPathIndexValid = pathIndex >= 0 && hitEvent.boxPath != null && pathIndex < hitEvent.boxPath.Length;
            if (isPathIndexValid)
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

    private void DrawGLLookDirection(PlayerController controller)
    {
        Vector3 startPosition = controller.GetPhysics().GetPosition() + Vector3.up * 0.1f;
        Vector3 endPosition = startPosition + controller.GetPhysics().GetLookDirection() * 2f;
        DrawGLLine(startPosition, endPosition, Color.yellow);
    }

    private void DrawGLHurtboxes(PlayerController controller)
    {
        Hurtbox_Type currentType = Hurtbox_Type.Standing;
        PlayerConfigSO config = controller.GetConfig();

        bool hasConfig = config != null;
        if (!hasConfig) return;

        CollisionBox[] boxes = config.GetHurtboxBoxes(currentType);
        bool hasBoxes = boxes != null && boxes.Length > 0;
        if (!hasBoxes) return;

        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        bool isLookDirectionValid = lookDirection != Vector3.zero;
        Quaternion rotation = isLookDirectionValid ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;
        Color hurtboxColor = GetHurtboxColor(currentType);

        foreach (var box in boxes)
        {
            DrawGLWireCube(position, rotation, box.localPosition, box.extents, hurtboxColor);
        }
    }

    private void DrawGLHitboxes(PlayerController controller)
    {
        PlayerStateMachine stateMachine = controller.GetStateMachine();
        bool isAttacking = stateMachine.GetCurrentState() == PlayerState_Type.Attacking;
        if (!isAttacking) return;

        ActionDataSO actionData = stateMachine.GetCurrentActionData();
        bool isActionDataValid = actionData != null && actionData.frameData.hitboxEvents != null;
        if (!isActionDataValid) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        bool isLookDirectionValid = lookDirection != Vector3.zero;
        Quaternion rotation = isLookDirectionValid ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        foreach (var hitEvent in actionData.frameData.hitboxEvents)
        {
            int pathIndex = currentFrame - hitEvent.activeStartFrame;

            bool isPathIndexValid = pathIndex >= 0 && hitEvent.boxPath != null && pathIndex < hitEvent.boxPath.Length;
            if (isPathIndexValid)
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