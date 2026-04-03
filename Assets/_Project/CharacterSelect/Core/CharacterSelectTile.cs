using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectTile : MonoBehaviour
{
    public Image portraitImage;
    public Image leftCursorImage;
    public Image rightCursorImage;
    public float colorBlendRatio = 0.5f;

    private void Awake()
    {
        if (rightCursorImage != null)
        {
            Vector3 rightScale = rightCursorImage.rectTransform.localScale;
            rightScale.x = -Mathf.Abs(rightScale.x);
            rightCursorImage.rectTransform.localScale = rightScale;
        }
    }

    public void SetupTile(Sprite portrait)
    {
        if (portrait != null)
        {
            portraitImage.sprite = portrait;
        }
    }

    public void UpdateVisuals(bool isLeftSelected, bool isRightSelected, Color leftColor, Color rightColor)
    {
        bool isBothSelected = isLeftSelected && isRightSelected;

        if (isBothSelected)
        {
            Color mixedCursorColor = Color.Lerp(leftColor, rightColor, 0.5f);
            Color finalBlendColor = Color.Lerp(Color.white, mixedCursorColor, colorBlendRatio);

            leftCursorImage.enabled = true;
            leftCursorImage.color = finalBlendColor;

            rightCursorImage.enabled = true;
            rightCursorImage.color = finalBlendColor;
        }
        else
        {
            leftCursorImage.enabled = isLeftSelected;
            if (isLeftSelected)
            {
                leftCursorImage.color = Color.Lerp(Color.white, leftColor, colorBlendRatio);
            }

            rightCursorImage.enabled = isRightSelected;
            if (isRightSelected)
            {
                rightCursorImage.color = Color.Lerp(Color.white, rightColor, colorBlendRatio);
            }
        }
    }
}