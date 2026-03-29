using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PlayerSettingController : MonoBehaviour
{
    public Button leftSideButton;
    public Button rightSideButton;
    
    public Button p1KeyBindButton;
    public Button p2KeyBindButton;

    public TextMeshProUGUI currentSideStatusText;
    public Image currentSideStatusImage;
    
    public TextMeshProUGUI currentKeyBindStatusText;
    public Image currentKeyBindStatusImage;

    public Side_Select_PanelPresetManager sideSelectManager;

    public int SelectedSide { get; private set; } = 0;
    public int SelectedKeyBind { get; private set; } = 0;

    public event Action<int> OnSideSelected;

    private void Start()
    {
        leftSideButton.onClick.AddListener(() => SelectSide(0));
        rightSideButton.onClick.AddListener(() => SelectSide(1));
        
        p1KeyBindButton.onClick.AddListener(() => SelectKeyBind(0));
        p2KeyBindButton.onClick.AddListener(() => SelectKeyBind(1));

        SelectSide(0);
        SelectKeyBind(0);
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

    private void SelectKeyBind(int bind)
    {
        SelectedKeyBind = bind;

        if (currentKeyBindStatusText != null)
        {
            currentKeyBindStatusText.text = (bind == 0) ? "P1 Keys Mapped" : "P2 Keys Mapped";
        }
        
        if (currentKeyBindStatusImage != null)
        {
            currentKeyBindStatusImage.gameObject.SetActive(true);
        }
    }
}