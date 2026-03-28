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

        AssignPresetsToList(leftSideControllers);
        AssignPresetsToList(rightSideControllers);
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

    private void AssignPresetsToList(List<PresetImageController> controllers)
    {
        if (controllers == null || controllers.Count == 0) return;

        foreach (PresetImageController controller in controllers)
        {
            if (controller == null) continue;

            int databaseIndex = Random.Range(0, presetDatabases.Count);
            PanelPresetDatabase selectedDatabase = presetDatabases[databaseIndex];

            if (selectedDatabase.presets == null || selectedDatabase.presets.Count == 0) continue;

            int presetIndex = Random.Range(0, selectedDatabase.presets.Count);
            ImagePreset selectedPreset = selectedDatabase.presets[presetIndex];

            controller.SetupPreset(selectedPreset);
            Debug.Log("test");
        }
    }
}