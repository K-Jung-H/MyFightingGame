using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PlayerSettingController : MonoBehaviour
{
    public Button leftSideButton;
    public Button rightSideButton;
    
    public TextMeshProUGUI currentSideStatusText;
    public Image currentSideStatusImage;
    public Side_Select_PanelPresetManager sideSelectManager;

    [Header("KeyBind UI")]
    public InputBindingTableSO bindingTable;
    public Transform keyBindContentParent;
    public KeyBindElementUI keyBindElementPrefab;
    
    [Header("KeyBind Details")]
    public TextMeshProUGUI presetNameText;
    public TextMeshProUGUI movementKeysText;
    public TextMeshProUGUI attackKeysLeftText;
    public TextMeshProUGUI attackKeysRightText;

    public TextMeshProUGUI systemKeysText;

    public int SelectedSide { get; private set; } = 0;

    public event Action<int> OnSideSelected;

    private void Start()
    {
        leftSideButton.onClick.AddListener(() => SelectSide(0));
        rightSideButton.onClick.AddListener(() => SelectSide(1));

        SelectSide(0);
        InitializeKeyBindList();
    }

    private void SelectSide(int side)
    {
        SelectedSide = side;
        
        if (currentSideStatusText != null)
        {
            currentSideStatusText.text = (side == 0) ? "Selected: Left Side" : "Selected: Right Side";
        }
        
        if (currentSideStatusImage != null)
        {
            currentSideStatusImage.gameObject.SetActive(true);
        }

        if (sideSelectManager != null)
        {
            sideSelectManager.UpdateSideSelection(side);
        }

        OnSideSelected?.Invoke(side);
    }

    private void InitializeKeyBindList()
    {
        if (bindingTable == null || keyBindElementPrefab == null || keyBindContentParent == null) return;

        foreach (Transform child in keyBindContentParent)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < bindingTable.presets.Length; i++)
        {
            InputBindingPresetSO preset = bindingTable.presets[i];
            KeyBindElementUI element = Instantiate(keyBindElementPrefab, keyBindContentParent);
            element.Initialize(preset, OnKeyBindSelected);
        }

        if (bindingTable.presets.Length > 0)
        {
            OnKeyBindSelected(bindingTable.presets[0]);
        }
    }

    private void OnKeyBindSelected(InputBindingPresetSO preset)
    {
        MatchDataManager.LocalKeyBindPreset = preset;
        UpdateKeyBindDetailsUI(preset);
    }

    private void UpdateKeyBindDetailsUI(InputBindingPresetSO preset)
    {
        if (preset == null) return;

        if (presetNameText != null) presetNameText.text = preset.presetName;
        
        if (movementKeysText != null)
        {
            movementKeysText.text = $"Up: {preset.bindingData.upKey}\nDown: {preset.bindingData.downKey}\nLeft: {preset.bindingData.leftKey}\nRight: {preset.bindingData.rightKey}";
        }

        if (attackKeysLeftText != null)
        {
            attackKeysLeftText.text = $"LP: {preset.bindingData.lpKey}  LK: {preset.bindingData.lkKey}";
        }

        if (attackKeysRightText != null)
        {
            attackKeysRightText.text = $"RP: {preset.bindingData.rpKey}  RK: {preset.bindingData.rkKey}";
        }

        if (systemKeysText != null)
        {
            systemKeysText.text = $"Select: {preset.bindingData.selectKey}\nPause: {preset.bindingData.pauseKey}";
        }
    }
}