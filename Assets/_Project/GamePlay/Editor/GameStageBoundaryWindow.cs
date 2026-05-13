using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

public class GameStageBoundaryWindow : EditorWindow
{
    private static readonly List<Type> WhitelistedComponents = new List<Type>
    {
        typeof(Transform),
        typeof(MeshFilter),
        typeof(MeshRenderer),
        typeof(ParticleSystem),
        typeof(ParticleSystemRenderer),
        typeof(Light),
        typeof(StageWallAnimationController) 
    };

    private GameStageDataSO targetSO;
    private GameObject sourceObject;

    [MenuItem("Tools/Stage Boundary Baker")]
    public static void Open() => GetWindow<GameStageBoundaryWindow>("Stage Baker");

    private void OnGUI()
    {
        GUILayout.Label("Stage Data Baker & Visual Stripper", EditorStyles.boldLabel);
        
        targetSO = (GameStageDataSO)EditorGUILayout.ObjectField("Target Data SO", targetSO, typeof(GameStageDataSO), false);
        sourceObject = (GameObject)EditorGUILayout.ObjectField("Source Object (Scene/Prefab)", sourceObject, typeof(GameObject), true);
        
        GUILayout.Space(15);
        GUI.enabled = targetSO != null && sourceObject != null;
        if (GUILayout.Button("Bake Stage Data & Generate Visual Prefab", GUILayout.Height(40)))
        {
            Bake();
        }
        GUI.enabled = true;

        if (targetSO != null)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                $"Wall Count: {targetSO.boundary.TotalWallCount}\n" +
                $"Visual Prefab: {(targetSO.visualPrefab != null ? targetSO.visualPrefab.name : "None")}", 
                MessageType.Info);
        }
    }

    private void Bake()
    {
        BoundaryWallMarker[] markers = sourceObject.GetComponentsInChildren<BoundaryWallMarker>(true);
        BoundaryPlane[] bakedPlanes = new BoundaryPlane[markers.Length];
        Vector3 origin = sourceObject.transform.position;

        for (int i = 0; i < markers.Length; i++)
        {
            Vector3 worldPos = markers[i].transform.position;
            Vector3 normal = markers[i].transform.forward;
            float distance = Vector3.Dot(worldPos - origin, normal);

            bakedPlanes[i] = new BoundaryPlane {
                Normal = FPVector3.FromVector3(normal),
                Distance = FP64.FromFloat(distance),
                isActive = markers[i].isActive,
                isBreakable = markers[i].isBreakable,
                durability = markers[i].durability,
                explosionForce = markers[i].explosionForce 
            };
        }

        CameraBoundsMarker[] camMarkers = sourceObject.GetComponentsInChildren<CameraBoundsMarker>(true);
        CameraBoundsData[] boundsDataArray = new CameraBoundsData[camMarkers.Length];

        for (int i = 0; i < camMarkers.Length; i++)
        {
            CameraBoundsMarker camMarker = camMarkers[i];
            BoxCollider box = camMarker.GetComponent<BoxCollider>();
            
            Vector3 pos = camMarker.transform.position;
            Vector3 scale = camMarker.transform.lossyScale;
            Vector3 center = box.center;
            Vector3 size = box.size;

            float calcMinX = pos.x + (center.x - size.x * 0.5f) * scale.x;
            float calcMaxX = pos.x + (center.x + size.x * 0.5f) * scale.x;
            float calcMinZ = pos.z + (center.z - size.z * 0.5f) * scale.z;
            float calcMaxZ = pos.z + (center.z + size.z * 0.5f) * scale.z;

            int resolvedIndex = -1; 
            if (camMarker.targetBreakableWall != null)
            {
                resolvedIndex = System.Array.FindIndex(markers, m => m.gameObject == camMarker.targetBreakableWall);
            }

            boundsDataArray[i] = new CameraBoundsData
            {
                minX = calcMinX,
                maxX = calcMaxX,
                minZ = calcMinZ,
                maxZ = calcMaxZ,
                unlockWallIndex = resolvedIndex 
            };
        }
        
        GameObject duplicate = Instantiate(sourceObject);
        duplicate.name = sourceObject.name + "_VisualOnly";

        StageWallAnimationController animController = duplicate.AddComponent<StageWallAnimationController>();
        animController.wallObjects = new GameObject[markers.Length];
        
        BoundaryWallMarker[] duplicateMarkers = duplicate.GetComponentsInChildren<BoundaryWallMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            animController.wallObjects[i] = duplicateMarkers[i].gameObject;
        }

        Component[] allComponents = duplicate.GetComponentsInChildren<Component>(true);
        for (int i = allComponents.Length - 1; i >= 0; i--)
        {
            Component comp = allComponents[i];
            if (comp == null) continue;

            bool isWhitelisted = false;
            foreach (Type t in WhitelistedComponents)
            {
                if (t.IsAssignableFrom(comp.GetType()))
                {
                    isWhitelisted = true;
                    break;
                }
            }

            if (!isWhitelisted)
            {
                DestroyImmediate(comp);
            }
        }

        string soPath = AssetDatabase.GetAssetPath(targetSO);
        string directory = Path.GetDirectoryName(soPath);
        string finalPath = Path.Combine(directory, duplicate.name + ".prefab").Replace("\\", "/");

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(duplicate, finalPath);
        DestroyImmediate(duplicate);

        Undo.RecordObject(targetSO, "Bake Stage Data");
        targetSO.boundary = new StageBoundary { Planes = bakedPlanes };
        targetSO.visualPrefab = savedPrefab;
        targetSO.cameraBoundsList = boundsDataArray; 
        EditorUtility.SetDirty(targetSO);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Stage Baker] 베이킹 완료: {markers.Length}개의 벽 데이터 저장 (카메라 바운즈 {camMarkers.Length}개 적용됨). 시각용 프리팹 생성됨 - {finalPath}");
    }
}