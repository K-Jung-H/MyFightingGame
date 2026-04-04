using UnityEngine;

[CreateAssetMenu(fileName = "NewInputBindingPreset", menuName = "ScriptableObjects/Input Binding Preset")]
public class InputBindingPresetSO : ScriptableObject
{
    public string presetName;
    public InputBinding bindingData;
}