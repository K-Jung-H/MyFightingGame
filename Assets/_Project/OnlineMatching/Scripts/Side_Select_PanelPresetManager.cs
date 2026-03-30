using UnityEngine;
using System.Collections.Generic;

public class Side_Select_PanelPresetManager : MonoBehaviour
{
    public List<PanelPresetDatabase> presetDatabases;
    public List<PresetImageController> leftSideControllers;
    public List<PresetImageController> rightSideControllers;

    private void Start()
    {
        AssignRandomPresets();
    }

    public void AssignRandomPresets()
    {
        if (presetDatabases == null || presetDatabases.Count == 0) return;

        List<ImagePreset> availablePresets = new List<ImagePreset>();
        foreach (PanelPresetDatabase database in presetDatabases)
        {
            if (database != null && database.presets != null)
            {
                availablePresets.AddRange(database.presets);
            }
        }

        AssignUniquePresetsToList(leftSideControllers, availablePresets);
        AssignUniquePresetsToList(rightSideControllers, availablePresets);
    }

    public void UpdateSideSelection(int selectedSide)
    {
        bool isLeftActive = (selectedSide == 0);
        bool isRightActive = (selectedSide == 1);

        if (leftSideControllers != null)
        {
            foreach (PresetImageController controller in leftSideControllers)
            {
                if (controller != null) controller.SetState(isLeftActive);
            }
        }

        if (rightSideControllers != null)
        {
            foreach (PresetImageController controller in rightSideControllers)
            {
                if (controller != null) controller.SetState(isRightActive);
            }
        }
    }

    private void AssignUniquePresetsToList(List<PresetImageController> controllers, List<ImagePreset> availablePool)
    {
        if (controllers == null || controllers.Count == 0) return;

        foreach (PresetImageController controller in controllers)
        {
            if (controller == null) continue;

            if (availablePool.Count == 0)
            {
                Debug.LogWarning("모든 프리셋이 소진되어 더 이상 고유한 이미지를 할당할 수 없습니다.");
                break;
            }

            int randomIndex = Random.Range(0, availablePool.Count);
            ImagePreset selectedPreset = availablePool[randomIndex];
            
            availablePool.RemoveAt(randomIndex);

            controller.SetupPreset(selectedPreset);
        }
    }
}