using UnityEngine;
using UnityEngine.UI;

public class SpriteNumberDisplay : MonoBehaviour
{
    public Image[] uiDigitImages;
    public bool isZeroPadded;

    private Sprite[] numberSprites;
    private int lastDisplayedNumber = -1;
    private int maxDisplayableNumber;

    private void Awake()
    {
        if (uiDigitImages == null || uiDigitImages.Length == 0)
        {
            return;
        }
        
        maxDisplayableNumber = (int)Mathf.Pow(10, uiDigitImages.Length) - 1;
    }

    public void InitializeSprites(Sprite[] sprites)
    {
        numberSprites = sprites;
    }

    public void SetNumber(int number)
    {
        if (number == lastDisplayedNumber)
        {
            return;
        }

        lastDisplayedNumber = number;
        UpdateDigitSprites(number);
    }

    public void ForceUpdateNumber(int number)
    {
        lastDisplayedNumber = number;
        UpdateDigitSprites(number);
    }

    private void UpdateDigitSprites(int number)
    {
        if (uiDigitImages == null || uiDigitImages.Length == 0 || numberSprites == null)
        {
            return;
        }

        int clampedNumber = Mathf.Clamp(number, 0, maxDisplayableNumber);
        int currentNumber = clampedNumber;
        int length = uiDigitImages.Length;

        for (int i = length - 1; i >= 0; i--)
        {
            int digit = currentNumber % 10;
            currentNumber /= 10;

            int threshold = (int)Mathf.Pow(10, length - 1 - i);
            bool isVisible = isZeroPadded || clampedNumber >= threshold || i == length - 1;

            if (uiDigitImages[i] != null)
            {
                uiDigitImages[i].preserveAspect = true;
                
                if (uiDigitImages[i].sprite != numberSprites[digit])
                {
                    uiDigitImages[i].sprite = numberSprites[digit];
                }
                
                if (uiDigitImages[i].gameObject.activeSelf != isVisible)
                {
                    uiDigitImages[i].gameObject.SetActive(isVisible);
                }
            }
        }
    }
}