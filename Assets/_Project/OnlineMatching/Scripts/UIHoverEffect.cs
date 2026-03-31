using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject hoverImage;

    private Button targetButton;

    private void Awake()
    {
        targetButton = GetComponent<Button>();
        
        if (hoverImage != null)
        {
            hoverImage.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetButton != null && targetButton.interactable && hoverImage != null)
        {
            hoverImage.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverImage != null)
        {
            hoverImage.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (hoverImage != null)
        {
            hoverImage.SetActive(false);
        }
    }
}