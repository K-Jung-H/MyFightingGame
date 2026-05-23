using UnityEngine;
using UnityEngine.UI;

public class SelectStatusUI : MonoBehaviour
{
    public Image selectedStageThumbnail;
    public GameObject frameSelect;
    public GameObject frameReady;


    public void UpdateStatus(Sprite thumbnailSprite, bool isLocked)
    {
        if (selectedStageThumbnail != null) selectedStageThumbnail.sprite = thumbnailSprite;
        
        if (frameSelect != null) frameSelect.SetActive(!isLocked);
        if (frameReady != null) frameReady.SetActive(isLocked);
    }
}