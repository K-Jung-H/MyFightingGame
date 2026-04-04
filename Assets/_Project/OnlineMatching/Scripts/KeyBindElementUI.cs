using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class KeyBindElementUI : MonoBehaviour
{
    public Button selectButton;
    public TextMeshProUGUI presetNameText;
    private InputBindingPresetSO targetPreset;

    public void Initialize(InputBindingPresetSO preset, Action<InputBindingPresetSO> onClickAction)
    {
        targetPreset = preset;
        presetNameText.text = preset.presetName;
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClickAction?.Invoke(targetPreset));
    }
}