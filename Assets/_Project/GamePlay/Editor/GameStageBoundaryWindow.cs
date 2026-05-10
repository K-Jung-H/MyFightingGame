using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

public class GameStageBoundaryWindow : EditorWindow
{
    // 보존할 컴포넌트 타입 컨테이너
    private static readonly List<Type> WhitelistedComponents = new List<Type>
    {
        typeof(Transform),
        typeof(MeshFilter),
        typeof(MeshRenderer),
        typeof(ParticleSystem),
        typeof(ParticleSystemRenderer),
        typeof(Light)
    };

    private GameStageDataSO targetSO;
    private GameObject sourceObject;

    [MenuItem("Tools/Stage Boundary Baker")]
    public static void Open() => GetWindow<GameStageBoundaryWindow>("Stage Baker");

    private void OnGUI()
    {
        GUILayout.Label("Stage Data Baker & Visual Stripper", EditorStyles.boldLabel);
        
        targetSO = (GameStageDataSO)EditorGUILayout.ObjectField("Target Data SO", targetSO, typeof(GameStageDataSO), false);
        sourceObject = (GameObject)EditorGUILayout.ObjectField("Source Object (Scene or Prefab)", sourceObject, typeof(GameObject), true);
        
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
        // 1. 원본에서 마커 데이터 추출
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
                durability = markers[i].durability
            };
        }

        // 2. 시각 전용 프리팹 생성을 위한 메모리상 복제
        GameObject duplicate = Instantiate(sourceObject);
        duplicate.name = sourceObject.name + "_VisualOnly";

        // 3. 화이트리스트 기반 컴포넌트 스트리핑
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

        // 4. 저장 경로 설정 (타겟 SO와 같은 폴더)
        string soPath = AssetDatabase.GetAssetPath(targetSO);
        string directory = Path.GetDirectoryName(soPath);
        string finalPath = Path.Combine(directory, duplicate.name + ".prefab").Replace("\\", "/");

        // 5. 프리팹 에셋으로 저장 및 복제본 파괴
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(duplicate, finalPath);
        DestroyImmediate(duplicate);

        // 6. 타겟 SO에 데이터 저장
        Undo.RecordObject(targetSO, "Bake Stage Data");
        
        targetSO.boundary = new StageBoundary { Planes = bakedPlanes };
        targetSO.visualPrefab = savedPrefab;
        
        EditorUtility.SetDirty(targetSO);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Stage Baker] {markers.Length}개의 벽 데이터를 저장하고, 순수 시각용 프리팹을 성공적으로 생성했습니다: {finalPath}");
    }
}