using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class StageBakerWindow : EditorWindow
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
    private float fadeNear = 2.0f;
    private float fadeFar = 5.0f;
    
    [SerializeField]
    private List<GameObject> excludedObjects = new List<GameObject>();
    private SerializedObject serializedObj;
    private SerializedProperty excludedProp;

    [MenuItem("Tools/Stage Baker")]
    public static void Open() => GetWindow<StageBakerWindow>("Stage Baker");

    private void OnEnable()
    {
        serializedObj = new SerializedObject(this);
        excludedProp = serializedObj.FindProperty("excludedObjects");
    }

    private void OnGUI()
    {
        serializedObj.Update();

        GUILayout.Label("Stage Data Baker & Visual Stripper", EditorStyles.boldLabel);
        
        targetSO = (GameStageDataSO)EditorGUILayout.ObjectField("Target Data SO", targetSO, typeof(GameStageDataSO), false);
        sourceObject = (GameObject)EditorGUILayout.ObjectField("Source Object (Scene/Prefab)", sourceObject, typeof(GameObject), true);
        
        EditorGUILayout.Space(5);
        GUILayout.Label("Dither Settings", EditorStyles.boldLabel);
        fadeNear = EditorGUILayout.FloatField("Fade Near", fadeNear);
        fadeFar = EditorGUILayout.FloatField("Fade Far", fadeFar);

        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(excludedProp, new GUIContent("Excluded Containers (No Dither)"), true);
        
        GUILayout.Space(15);
        GUI.enabled = targetSO != null && sourceObject != null;
        if (GUILayout.Button("Bake Stage Data & Generate Visual Prefab", GUILayout.Height(40)))
        {
            ExecuteBakeProcess();
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

        serializedObj.ApplyModifiedProperties();
    }

    private void ExecuteBakeProcess()
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

        GameObject duplicate = Instantiate(sourceObject);
        duplicate.name = sourceObject.name + "_VisualOnly";

        StageWallAnimationController animController = duplicate.AddComponent<StageWallAnimationController>();
        animController.wallObjects = new GameObject[markers.Length];
        
        BoundaryWallMarker[] duplicateMarkers = duplicate.GetComponentsInChildren<BoundaryWallMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            animController.wallObjects[i] = duplicateMarkers[i].gameObject;
        }

        string soPath = AssetDatabase.GetAssetPath(targetSO);
        string directory = Path.GetDirectoryName(soPath);
        string materialDir = Path.Combine(directory, "Materials").Replace("\\", "/");

        List<string> excludedPaths = new List<string>();
        foreach (GameObject exObj in excludedObjects)
        {
            if (exObj == null) continue;

            Transform resolvedTarget = exObj.transform;
            if (!resolvedTarget.IsChildOf(sourceObject.transform) && resolvedTarget != sourceObject.transform)
            {
                Transform found = FindChildByNameRecursive(sourceObject.transform, exObj.name);
                if (found != null)
                {
                    resolvedTarget = found;
                }
                else
                {
                    continue;
                }
            }
            excludedPaths.Add(GetRelativePath(sourceObject.transform, resolvedTarget));
        }

        ConvertMaterialsToDither(duplicate, materialDir, excludedPaths, fadeNear, fadeFar);

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

        string finalPath = Path.Combine(directory, duplicate.name + ".prefab").Replace("\\", "/");
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(duplicate, finalPath);
        DestroyImmediate(duplicate);

        Undo.RecordObject(targetSO, "Bake Stage Data");
        targetSO.boundary = new StageBoundary { Planes = bakedPlanes };
        targetSO.visualPrefab = savedPrefab;
        EditorUtility.SetDirty(targetSO);
        AssetDatabase.SaveAssets();
    }

    private void ConvertMaterialsToDither(GameObject root, string saveDirectory, List<string> excludedPaths, float near, float far)
    {
        EnsureDirectoryExists(saveDirectory);
        Shader ditherShader = Shader.Find("Shader Graphs/WallDitherShader");
        if (ditherShader == null) return;

        Dictionary<Material, Material> materialCache = new Dictionary<Material, Material>();
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            string currentPath = GetRelativePath(root.transform, renderer.transform);
            bool skipDither = false;

            foreach (string p in excludedPaths)
            {
                if (p == "" || currentPath == p || currentPath.StartsWith(p + "/"))
                {
                    skipDither = true;
                    break;
                }
            }

            if (skipDither) continue;

            Material[] originalMaterials = renderer.sharedMaterials;
            Material[] newMaterials = new Material[originalMaterials.Length];

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material origMat = originalMaterials[i];
                if (origMat == null) continue;

                if (materialCache.TryGetValue(origMat, out Material cachedMat))
                {
                    newMaterials[i] = cachedMat;
                }
                else
                {
                    Material newMat = CreateDitherMaterial(origMat, ditherShader, near, far);
                    string safeName = origMat.name.Replace(" (Instance)", "");
                    string assetPath = saveDirectory + "/" + safeName + "_Dither.mat";
                    
                    AssetDatabase.CreateAsset(newMat, assetPath);
                    materialCache.Add(origMat, newMat);
                    newMaterials[i] = newMat;
                }
            }
            renderer.sharedMaterials = newMaterials;
        }
    }

    private Transform FindChildByNameRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildByNameRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return "";

        string path = target.name;
        Transform current = target;
        while (current.parent != null && current.parent != root)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
        }
    }

    private Material CreateDitherMaterial(Material original, Shader ditherShader, float near, float far)
    {
        Material newMat = new Material(ditherShader);

        if (original.HasProperty("_BaseMap")) newMat.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
        else if (original.HasProperty("_MainTex")) newMat.SetTexture("_BaseMap", original.GetTexture("_MainTex"));

        if (original.HasProperty("_BumpMap")) newMat.SetTexture("_BumpMap", original.GetTexture("_BumpMap"));
        if (original.HasProperty("_EmissionMap")) newMat.SetTexture("_EmissionMap", original.GetTexture("_EmissionMap"));
        if (original.HasProperty("_OcclusionMap")) newMat.SetTexture("_OcclusionMap", original.GetTexture("_OcclusionMap"));

        if (original.HasProperty("_Metallic")) newMat.SetFloat("_Metallic", original.GetFloat("_Metallic"));
        else newMat.SetFloat("_Metallic", 0.0f);

        if (original.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", original.GetFloat("_Smoothness"));
        else newMat.SetFloat("_Smoothness", 0.5f);

        newMat.SetFloat("_Fade_Near", near);
        newMat.SetFloat("_Fade_Far", far);

        return newMat;
    }
}