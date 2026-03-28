using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PresetImageController : MonoBehaviour
{
    public Image shadowImage;
    public Image realImage;
    public float transitionDuration = 1f;

    private Color initialShadowColor;
    private Coroutine transitionCoroutine;
    private bool isStateActive;

public void SetupPreset(ImagePreset preset)
    {
        if (preset.shadowSprite == null || preset.realSprite == null)
        {
            Debug.LogWarning("전달받은 프리셋 데이터에 스프라이트가 비어있습니다. PanelPresetDatabase 에셋을 확인하세요.");
        }

        if (shadowImage != null)
        {
            shadowImage.sprite = preset.shadowSprite;
            initialShadowColor = shadowImage.color;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}]의 PresetImageController에 Shadow Image 컴포넌트가 연결되지 않았습니다.");
        }

        if (realImage != null)
        {
            realImage.sprite = preset.realSprite;
            Color startRealColor = realImage.color;
            startRealColor.a = 0f;
            realImage.color = startRealColor;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}]의 PresetImageController에 Real Image 컴포넌트가 연결되지 않았습니다.");
        }
    }

    public void SetState(bool isTargetActive)
    {
        isStateActive = isTargetActive;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        if (isStateActive)
        {
            transitionCoroutine = StartCoroutine(TransitionToActive());
        }
        else
        {
            ResetToInactive();
        }
    }

    private IEnumerator TransitionToActive()
    {
        float elapsedTime = 0f;
        Color currentShadowColor = shadowImage != null ? shadowImage.color : Color.white;
        Color currentRealColor = realImage != null ? realImage.color : Color.clear;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / transitionDuration;

            if (shadowImage != null)
            {
                shadowImage.color = Color.Lerp(currentShadowColor, Color.black, t);
            }

            if (realImage != null)
            {
                Color newRealColor = currentRealColor;
                newRealColor.a = Mathf.Lerp(currentRealColor.a, 1f, t);
                realImage.color = newRealColor;
            }

            yield return null;
        }
    }

    private void ResetToInactive()
    {
        if (shadowImage != null)
        {
            shadowImage.color = initialShadowColor;
        }

        if (realImage != null)
        {
            Color resetRealColor = realImage.color;
            resetRealColor.a = 0f;
            realImage.color = resetRealColor;
        }
    }
}