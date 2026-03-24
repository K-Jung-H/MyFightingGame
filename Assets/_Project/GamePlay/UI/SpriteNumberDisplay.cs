using UnityEngine;
using UnityEngine.UI;

public class SpriteNumberDisplay : MonoBehaviour
{
    public Sprite[] numberSprites;
    public Image[] uiDigitImages;
    public bool isZeroPadded;

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
        if (uiDigitImages == null || uiDigitImages.Length == 0)
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
                uiDigitImages[i].sprite = numberSprites[digit];
                uiDigitImages[i].gameObject.SetActive(isVisible);
            }
        }
    }
}