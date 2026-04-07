using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ImagePreset
{
    public Sprite shadowSprite;
    public Sprite realSprite;
}

[CreateAssetMenu(fileName = "NewPanelPresetDatabase", menuName = "ScriptableObjects/PanelPresetDatabase")]
public class PanelPresetDatabase : ScriptableObject
{
    public List<ImagePreset> presets = new List<ImagePreset>();
}