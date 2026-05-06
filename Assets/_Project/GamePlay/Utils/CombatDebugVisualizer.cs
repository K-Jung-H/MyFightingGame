using UnityEngine;

public class CombatDebugVisualizer : MonoBehaviour
{
    [SerializeField] private GameLoopManager gameLoopManager;
    [SerializeField] private bool isShowingInSceneView = true;
    [SerializeField] private bool isShowingInGameView = true;
    [SerializeField] private bool isShowingHurtboxes = true;
    [SerializeField] private bool isShowingHitboxes = true;
    [SerializeField] private bool isShowingLookDirection = true;
    [SerializeField] private bool isShowingStageBoundaries = true;

    private Material debugMaterial;

    private void Awake()
    {
        CreateDebugMaterial();
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

        if (isShowingStageBoundaries) DrawGizmoStageBoundaries(gameLoopManager.GetStageBoundary());
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

        try 
        {    
            bool hasPlayerOne = playerOne != null;
            if (hasPlayerOne) DrawGLForPlayer(playerOne);

            bool hasPlayerTwo = playerTwo != null;
            if (hasPlayerTwo) DrawGLForPlayer(playerTwo);

            if (isShowingStageBoundaries)  
                DrawGLStageBoundaries(gameLoopManager.GetStageBoundary());
        }
        finally
        {
            GL.PopMatrix();
        }
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

    private void DrawGizmoStageBoundaries(StageBoundary boundary)
    {
        if (boundary.Planes == null) return;

        foreach (var plane in boundary.Planes)
        {
            if (!plane.isActive) continue;

            Vector3 normal = plane.Normal.ToVector3();
            float dist = plane.Distance.ToFloat();
            Vector3 centerBase = normal * dist;
            
            float h = 5f; 
            float w = 20f;
            Vector3 center = centerBase + Vector3.up * (h * 0.5f);
            
            Quaternion rotation = Quaternion.LookRotation(normal);
            Vector3 right = rotation * Vector3.right * (w * 0.5f);
            Vector3 up = Vector3.up * (h * 0.5f);

            Vector3 p0 = center - right - up;
            Vector3 p1 = center + right - up;
            Vector3 p2 = center + right + up;
            Vector3 p3 = center - right + up;

            Color baseColor = plane.isBreakable ? new Color(1f, 0.5f, 0f) : Color.cyan;
            Color faceColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.1f);
            Color wireColor = plane.isBreakable ? new Color(1f, 0.7f, 0.2f) : Color.cyan;

            Gizmos.color = faceColor;
            Quaternion meshRotation = rotation * Quaternion.Euler(0, 180, 0);
            Gizmos.DrawMesh(GetQuadMesh(), center, meshRotation, new Vector3(w, h, 1f));

            Gizmos.color = wireColor;
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(center, normal * 2f);
        }
    }

    private void DrawGLStageBoundaries(StageBoundary boundary)
    {
        if (boundary.Planes == null) return;

        for (int i = 0; i < boundary.Planes.Length; i++)
        {
            var plane = boundary.Planes[i];
            if (!plane.isActive) continue;

            Vector3 normal = plane.Normal.ToVector3();
            float dist = plane.Distance.ToFloat();
            Vector3 centerBase = normal * dist;
            
            float h = 5f; 
            float w = 20f;
            Vector3 center = centerBase + Vector3.up * (h * 0.5f);
            
            Quaternion rotation = Quaternion.LookRotation(normal);
            Vector3 right = rotation * Vector3.right * (w * 0.5f);
            Vector3 up = Vector3.up * (h * 0.5f);

            Vector3 p0 = center - right - up;
            Vector3 p1 = center + right - up;
            Vector3 p2 = center + right + up;
            Vector3 p3 = center - right + up;

            Color baseColor = plane.isBreakable ? new Color(1f, 0.5f, 0f) : Color.cyan;
            Color faceColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f);
            Color wireColor = plane.isBreakable ? new Color(1f, 0.7f, 0.2f) : Color.cyan;

            GL.Begin(GL.QUADS);
            GL.Color(faceColor);
            GL.Vertex(p0);
            GL.Vertex(p1);
            GL.Vertex(p2);
            GL.Vertex(p3);
            GL.End();

            GL.Begin(GL.LINES);
            GL.Color(wireColor);
            GL.Vertex(p0); GL.Vertex(p1);
            GL.Vertex(p1); GL.Vertex(p2);
            GL.Vertex(p2); GL.Vertex(p3);
            GL.Vertex(p3); GL.Vertex(p0);
            GL.End();

            DrawGLLine(center, center + normal * 2f, Color.yellow);
        }
    }

    private void DrawGizmoLookDirection(PlayerController controller)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(controller.GetPhysics().GetPosition() + Vector3.up * 0.1f, controller.GetPhysics().GetLookDirection() * 2f);
    }

    private void DrawGizmoHurtboxes(PlayerController controller)
    {
        Hurtbox_Type currentType = controller.GetStateMachine().GetCurrentHurtboxType();
        PlayerConfigSO config = controller.GetConfig();

        if (config == null) return;

        FPCollisionBox[] boxes = config.GetFPHurtboxBoxes(currentType);
        if (boxes == null || boxes.Length == 0) return;

        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        Quaternion rotation = (lookDirection != Vector3.zero) ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Color hurtboxColor = GetHurtboxColor(currentType);

        foreach (var box in boxes)
        {
            Vector3 localPos = box.localPosition.ToVector3();
            Vector3 extents = box.extents.ToVector3();

            Gizmos.color = new Color(hurtboxColor.r, hurtboxColor.g, hurtboxColor.b, 0.4f);
            Gizmos.DrawCube(localPos, extents * 2f);

            Gizmos.color = hurtboxColor;
            Gizmos.DrawWireCube(localPos, extents * 2f);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawGizmoHitboxes(PlayerController controller)
    {
        PlayerStateMachine stateMachine = controller.GetStateMachine();
        if (stateMachine.GetCurrentState() != PlayerState_Type.Attacking) return;

        ActionDataSO actionData = stateMachine.GetCurrentActionData();
        if (actionData == null) return;

        FPHitboxEvent[] hitboxEvents = actionData.GetFPHitboxEvents();
        if (hitboxEvents == null || hitboxEvents.Length == 0) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        Quaternion rotation = (lookDirection != Vector3.zero) ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);

        foreach (var hitEvent in hitboxEvents)
        {
            int pathIndex = currentFrame - hitEvent.activeStartFrame;

            if (pathIndex >= 0 && hitEvent.boxPath != null && pathIndex < hitEvent.boxPath.Length)
            {
                FPCollisionBox activeBox = hitEvent.boxPath[pathIndex];
                Vector3 localPos = activeBox.localPosition.ToVector3();
                Vector3 extents = activeBox.extents.ToVector3();

                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawCube(localPos, extents * 2f);

                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(localPos, extents * 2f);
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
        Hurtbox_Type currentType = controller.GetStateMachine().GetCurrentHurtboxType();
        PlayerConfigSO config = controller.GetConfig();

        if (config == null) return;

        FPCollisionBox[] boxes = config.GetFPHurtboxBoxes(currentType);
        if (boxes == null || boxes.Length == 0) return;

        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        Quaternion rotation = (lookDirection != Vector3.zero) ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;
        Color hurtboxColor = GetHurtboxColor(currentType);

        foreach (var box in boxes)
        {
            Vector3 localPos = box.localPosition.ToVector3();
            Vector3 extents = box.extents.ToVector3();
            DrawGLWireCube(position, rotation, localPos, extents, hurtboxColor);
        }
    }

    private void DrawGLHitboxes(PlayerController controller)
    {
        PlayerStateMachine stateMachine = controller.GetStateMachine();
        if (stateMachine.GetCurrentState() != PlayerState_Type.Attacking) return;

        ActionDataSO actionData = stateMachine.GetCurrentActionData();
        if (actionData == null) return;

        FPHitboxEvent[] hitboxEvents = actionData.GetFPHitboxEvents();
        if (hitboxEvents == null || hitboxEvents.Length == 0) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        Vector3 position = controller.GetPhysics().GetPosition();
        Vector3 lookDirection = controller.GetPhysics().GetLookDirection();
        Quaternion rotation = (lookDirection != Vector3.zero) ? Quaternion.LookRotation(lookDirection) : Quaternion.identity;

        foreach (var hitEvent in hitboxEvents)
        {
            int pathIndex = currentFrame - hitEvent.activeStartFrame;

            if (pathIndex >= 0 && hitEvent.boxPath != null && pathIndex < hitEvent.boxPath.Length)
            {
                FPCollisionBox activeBox = hitEvent.boxPath[pathIndex];
                Vector3 localPos = activeBox.localPosition.ToVector3();
                Vector3 extents = activeBox.extents.ToVector3();
                DrawGLWireCube(position, rotation, localPos, extents, Color.red);
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

    private static Mesh _quadMesh;
    private static Mesh GetQuadMesh()
    {
        if (_quadMesh == null)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quadMesh = quad.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(quad);
        }
        return _quadMesh;
    }
}