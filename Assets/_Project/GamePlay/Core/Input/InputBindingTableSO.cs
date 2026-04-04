using UnityEngine;

[CreateAssetMenu(fileName = "InputBindingTable", menuName = "ScriptableObjects/Input Binding Table")]
public class InputBindingTableSO : ScriptableObject
{
    public InputBindingPresetSO[] presets;
}