using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class PanelPresetAutoBuilder
{
    private static readonly string realImagesPath = "Assets/Asset/Sprites/Character/full_size";
    private static readonly string shadowImagesPath = "Assets/Asset/Sprites/Icon/Shadow";
    private static readonly string saveDirectory = "Assets/Resources/SO";

    [MenuItem("Tools/Auto Build Panel Presets")]
    public static void BuildPresets()
    {
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        string[] realGuids = AssetDatabase.FindAssets("t:Sprite", new[] { realImagesPath });
        string[] shadowGuids = AssetDatabase.FindAssets("t:Sprite", new[] { shadowImagesPath });

        List<Sprite> realSprites = new List<Sprite>();
        List<Sprite> shadowSprites = new List<Sprite>();

        foreach (string guid in realGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            realSprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }

        foreach (string guid in shadowGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            shadowSprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }

        realSprites = realSprites.OrderBy(s => s.name).ToList();
        shadowSprites = shadowSprites.OrderBy(s => s.name).ToList();

        int totalImages = Mathf.Min(realSprites.Count, shadowSprites.Count);
        int splitCount = 3;
        int itemsPerSO = Mathf.CeilToInt((float)totalImages / splitCount);

        for (int i = 0; i < splitCount; i++)
        {
            PanelPresetDatabase db = ScriptableObject.CreateInstance<PanelPresetDatabase>();
            db.presets = new List<ImagePreset>();

            int startIndex = i * itemsPerSO;
            int endIndex = Mathf.Min(startIndex + itemsPerSO, totalImages);

            for (int j = startIndex; j < endIndex; j++)
            {
                ImagePreset preset = new ImagePreset();
                preset.realSprite = realSprites[j];
                preset.shadowSprite = shadowSprites[j];
                db.presets.Add(preset);
            }

            string assetPath = $"{saveDirectory}/PanelPresetDatabase_Part{i + 1}.asset";
            AssetDatabase.CreateAsset(db, assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SO 분할 생성 및 자동 할당이 완료되었습니다.");
    }
}