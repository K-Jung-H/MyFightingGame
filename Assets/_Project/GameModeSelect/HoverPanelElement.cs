using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverPanelElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image shadowImage;
    public Image realImage;
    public float fadeDuration = 0.5f;

    private PanelPresetManager presetManager;
    private Coroutine fadeCoroutine;
    private Color initialShadowColor;

    private void Awake()
    {
        Color startColor = realImage.color;
        startColor.a = 0f;
        realImage.color = startColor;

        if (shadowImage != null)
        {
            initialShadowColor = shadowImage.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeImages(true));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        Color currentRealColor = realImage.color;
        currentRealColor.a = 0f;
        realImage.color = currentRealColor;

        if (shadowImage != null)
        {
            shadowImage.color = initialShadowColor;
        }

        if (presetManager != null)
        {
            presetManager.RefreshSinglePanel(this);
        }
    }

    public void InitializePanel(PanelPresetManager manager, Sprite targetRealSprite, Sprite targetShadowSprite)
    {
        presetManager = manager;
        UpdateImages(targetRealSprite, targetShadowSprite);
    }

    public void UpdateImages(Sprite targetRealSprite, Sprite targetShadowSprite)
    {
        if (realImage != null && targetRealSprite != null)
        {
            realImage.sprite = targetRealSprite;
            ApplyPivotAndSize(realImage, targetRealSprite);
        }

        if (shadowImage != null && targetShadowSprite != null)
        {
            shadowImage.sprite = targetShadowSprite;
            ApplyPivotAndSize(shadowImage, targetShadowSprite);
        }
    }

    private void ApplyPivotAndSize(Image img, Sprite sprite)
    {
        RectTransform rt = img.rectTransform;
        RectTransform parentRt = rt.parent.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);

        float normalizedPivotX = sprite.pivot.x / sprite.rect.width;
        float normalizedPivotY = sprite.pivot.y / sprite.rect.height;
        rt.pivot = new Vector2(normalizedPivotX, normalizedPivotY);

        float parentHeight = parentRt.rect.height;
        float actualHeight = parentHeight - 30f;
        float targetWidth = actualHeight * (sprite.rect.width / sprite.rect.height);

        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

        rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
        rt.offsetMax = new Vector2(rt.offsetMax.x, -30f);

        rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
    }

    private IEnumerator FadeImages(bool isHovering)
    {
        float targetAlpha = isHovering ? 1f : 0f;
        Color targetShadowColor = isHovering ? Color.black : initialShadowColor;

        Color currentRealColor = realImage.color;
        Color currentShadowColor = shadowImage != null ? shadowImage.color : Color.white;

        float startAlpha = currentRealColor.a;
        float timeElapsed = 0f;

        while (timeElapsed < fadeDuration)
        {
            timeElapsed += Time.deltaTime;
            float t = timeElapsed / fadeDuration;

            currentRealColor.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            realImage.color = currentRealColor;

            if (shadowImage != null)
            {
                shadowImage.color = Color.Lerp(currentShadowColor, targetShadowColor, t);
            }

            yield return null;
        }

        currentRealColor.a = targetAlpha;
        realImage.color = currentRealColor;

        if (shadowImage != null)
        {
            shadowImage.color = targetShadowColor;
        }
    }
}