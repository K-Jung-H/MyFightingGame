using System.Collections.Generic;
using UnityEngine;

public class PanelPresetManager : MonoBehaviour
{
    public List<PanelPresetDatabase> presetDatabases;
    public List<HoverPanelElement> targetPanels;

    private void Start()
    {
        AssignRandomPresets();
    }

    public void AssignRandomPresets()
    {
        if (presetDatabases == null || presetDatabases.Count == 0 || targetPanels == null)
        {
            return;
        }

        List<ImagePreset> availablePresets = GetAllPresets();

        for (int i = 0; i < targetPanels.Count; i++)
        {
            if (availablePresets.Count == 0)
            {
                break;
            }

            int randomIndex = Random.Range(0, availablePresets.Count);
            ImagePreset selectedPreset = availablePresets[randomIndex];

            targetPanels[i].InitializePanel(this, selectedPreset.realSprite, selectedPreset.shadowSprite);

            availablePresets.RemoveAt(randomIndex);
        }
    }

    public void RefreshSinglePanel(HoverPanelElement panel)
    {
        List<ImagePreset> allPresets = GetAllPresets();
        List<ImagePreset> availablePresets = new List<ImagePreset>();

        for (int i = 0; i < allPresets.Count; i++)
        {
            bool isUsed = false;
            for (int j = 0; j < targetPanels.Count; j++)
            {
                if (targetPanels[j] != panel && targetPanels[j].realImage.sprite == allPresets[i].realSprite)
                {
                    isUsed = true;
                    break;
                }
            }

            if (!isUsed)
            {
                availablePresets.Add(allPresets[i]);
            }
        }

        if (availablePresets.Count == 0)
        {
            return;
        }

        int randomIndex = Random.Range(0, availablePresets.Count);
        ImagePreset newPreset = availablePresets[randomIndex];

        panel.UpdateImages(newPreset.realSprite, newPreset.shadowSprite);
    }

    private List<ImagePreset> GetAllPresets()
    {
        List<ImagePreset> allPresets = new List<ImagePreset>();
        for (int i = 0; i < presetDatabases.Count; i++)
        {
            if (presetDatabases[i] != null && presetDatabases[i].presets != null)
            {
                allPresets.AddRange(presetDatabases[i].presets);
            }
        }
        return allPresets;
    }
}