using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectTile : MonoBehaviour
{
    public Image portraitImage;
    public Image p1CursorImage;
    public Image p2CursorImage;
    public float colorBlendRatio = 0.5f;

    private void Awake()
    {
        bool isP2CursorValid = p2CursorImage != null;
        if (isP2CursorValid)
        {
            Vector3 p2Scale = p2CursorImage.rectTransform.localScale;
            p2Scale.x = -Mathf.Abs(p2Scale.x);
            p2CursorImage.rectTransform.localScale = p2Scale;
        }
    }

    public void SetupTile(Sprite portrait)
    {
        bool isPortraitValid = portrait != null;
        if (isPortraitValid)
        {
            portraitImage.sprite = portrait;
        }
    }

    public void UpdateVisuals(bool isP1Selected, bool isP2Selected, Color p1Color, Color p2Color)
    {
        bool isBothSelected = isP1Selected && isP2Selected;

        if (isBothSelected)
        {
            Color mixedCursorColor = Color.Lerp(p1Color, p2Color, 0.5f);
            Color finalBlendColor = Color.Lerp(Color.white, mixedCursorColor, colorBlendRatio);

            p1CursorImage.enabled = true;
            p1CursorImage.color = finalBlendColor;

            p2CursorImage.enabled = true;
            p2CursorImage.color = finalBlendColor;
        }
        else
        {
            p1CursorImage.enabled = isP1Selected;
            if (isP1Selected)
            {
                p1CursorImage.color = Color.Lerp(Color.white, p1Color, colorBlendRatio);
            }

            p2CursorImage.enabled = isP2Selected;
            if (isP2Selected)
            {
                p2CursorImage.color = Color.Lerp(Color.white, p2Color, colorBlendRatio);
            }
        }
    }
}