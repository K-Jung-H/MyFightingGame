using UnityEngine;
using UnityEngine.UI;

public class StagePortraitUI : MonoBehaviour
{
    public Image imageThumbnail;
    public Image imageBackground;
    public GameObject imageSelectP1;
    public GameObject imageSelectP2;
    
    [HideInInspector] public GameStageDataSO stageData;
    public bool isEmpty { get; private set; }

    public void SetupPortrait(GameStageDataSO data)
    {
        stageData = data;
        isEmpty = false;
        
        if (imageThumbnail != null && data != null)
        {
            imageThumbnail.gameObject.SetActive(true);
            imageThumbnail.sprite = data.thumbnail;
        }
        SetSelectionHighlight(false, false);
    }

    public void SetupEmptyPortrait()
    {
        stageData = null;
        isEmpty = true;

        if (imageThumbnail != null) imageThumbnail.gameObject.SetActive(false);
        if (imageSelectP1 != null) imageSelectP1.SetActive(false);
        if (imageSelectP2 != null) imageSelectP2.SetActive(false);
        
        if (imageBackground != null) imageBackground.gameObject.SetActive(true);
    }

    public void SetSelectionHighlight(bool isP1Selected, bool isP2Selected)
    {
        if (isEmpty) return;
        
        if (imageSelectP1 != null) imageSelectP1.SetActive(isP1Selected);
        if (imageSelectP2 != null) imageSelectP2.SetActive(isP2Selected);
    }
}