using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GameStageBoundaryWindow : EditorWindow
{
    private GameStageDataSO targetSO;
    private GameObject sceneRoot;
    private bool showVisualization = true;

    [MenuItem("Tools/Stage Boundary Baker")]
    public static void Open() => GetWindow<GameStageBoundaryWindow>("Stage Baker");

    private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    private void OnGUI()
    {
        GUILayout.Label("Stage Boundary Tool", EditorStyles.boldLabel);
        
        targetSO = (GameStageDataSO)EditorGUILayout.ObjectField("Target Data SO", targetSO, typeof(GameStageDataSO), false);
        sceneRoot = (GameObject)EditorGUILayout.ObjectField("Scene Root Object", sceneRoot, typeof(GameObject), true);
        
        showVisualization = EditorGUILayout.Toggle("Show Visualization", showVisualization);

        GUILayout.Space(10);
        GUI.enabled = targetSO != null && sceneRoot != null;
        if (GUILayout.Button("Bake From Scene", GUILayout.Height(30)))
        {
            Bake();
        }
        GUI.enabled = true;

        if (targetSO != null)
        {
            EditorGUILayout.HelpBox($"Current SO contains {targetSO.boundary.TotalWallCount} planes.", MessageType.Info);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!showVisualization || targetSO == null) return;

        StageBoundary boundary = targetSO.boundary;
        if (boundary.Planes == null) return;

        foreach (var plane in boundary.Planes)
        {
            if (!plane.isActive) continue;
            DrawPlaneVisualization(plane);
        }
    }

    private void DrawPlaneVisualization(BoundaryPlane plane)
    {
        Vector3 normal = plane.Normal.ToVector3();
        float dist = plane.Distance.ToFloat();
        float h = 5f; 
        float w = 20f;
        Vector3 center = normal * dist + Vector3.up * (h * 0.5f);
        Quaternion rot = Quaternion.LookRotation(normal);

        Color planeColorBase = plane.isBreakable ? new Color(1f, 0.5f, 0f) : Color.cyan;
        Color wireColorBase = plane.isBreakable ? new Color(1f, 0.7f, 0.2f) : Color.cyan;

        Color faceColor = new Color(planeColorBase.r, planeColorBase.g, planeColorBase.b, 0.1f);
        Color wireColor = wireColorBase;

        Handles.color = faceColor;
        Handles.DrawSolidRectangleWithOutline(new Vector3[] { 
            center + rot * new Vector3(-(w*0.5f), -(h*0.5f), 0),
            center + rot * new Vector3((w*0.5f), -(h*0.5f), 0),
            center + rot * new Vector3((w*0.5f), (h*0.5f), 0),
            center + rot * new Vector3(-(w*0.5f), (h*0.5f), 0)
        }, faceColor, wireColor);
        
        Handles.color = Color.yellow;
        Handles.DrawLine(center, center + normal * 2f);
    }

    private void Bake()
    {
        BoundaryWallMarker[] markers = sceneRoot.GetComponentsInChildren<BoundaryWallMarker>();
        BoundaryPlane[] bakedPlanes = new BoundaryPlane[markers.Length];
        Vector3 origin = sceneRoot.transform.position;

        for (int i = 0; i < markers.Length; i++)
        {
            Vector3 worldPos = markers[i].transform.position;
            Vector3 normal = markers[i].transform.forward;
            float distance = Vector3.Dot(worldPos - origin, normal);

            bakedPlanes[i] = new BoundaryPlane {
                Normal = FPVector3.FromVector3(normal),
                Distance = FP64.FromFloat(distance),
                isActive = markers[i].isActive,
                isBreakable = markers[i].isBreakable
            };
        }

        Undo.RecordObject(targetSO, "Bake Stage Boundary");
        targetSO.boundary.Planes = bakedPlanes;
        EditorUtility.SetDirty(targetSO);
        AssetDatabase.SaveAssets();
        Debug.Log($"Bake Successful: {markers.Length} planes saved to {targetSO.name}.");
    }
}