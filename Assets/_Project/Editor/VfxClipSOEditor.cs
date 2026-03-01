using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(VfxClipSO))]
public class VfxClipSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VfxClipSO clip = (VfxClipSO)target;

        GUILayout.Space(15);
        GUILayout.Label("Auto Setup Tool", EditorStyles.boldLabel);

        bool hasSourceSheet = clip.sourceSpriteSheet != null;
        if (hasSourceSheet)
        {
            bool isButtonClicked = GUILayout.Button("Load Sprites From Sheet", GUILayout.Height(30));
            if (isButtonClicked)
            {
                LoadSpritesFromSheet(clip);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a Source Sprite Sheet to auto-load frames.", MessageType.Info);
        }
    }

    private void LoadSpritesFromSheet(VfxClipSO clip)
    {
        string path = AssetDatabase.GetAssetPath(clip.sourceSpriteSheet);
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

        Sprite[] extractedSprites = allAssets.OfType<Sprite>()
            .OrderBy(sprite => sprite.name, new NaturalCustomComparer())
            .ToArray();

        bool hasExtractedSprites = extractedSprites.Length > 0;
        if (hasExtractedSprites)
        {
            Undo.RecordObject(clip, "Auto Load Sprites");
            clip.frames = extractedSprites;
            EditorUtility.SetDirty(clip);
        }
    }
}

public class NaturalCustomComparer : System.Collections.Generic.IComparer<string>
{
    public int Compare(string x, string y)
    {
        return EditorUtility.NaturalCompare(x, y);
    }
}