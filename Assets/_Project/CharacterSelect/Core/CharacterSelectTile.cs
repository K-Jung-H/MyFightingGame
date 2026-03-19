using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectTile : MonoBehaviour
{
    public Image borderImage;
    public Image portraitImage;

    public void SetupTile(Sprite portrait)
    {
        bool hasPortrait = portrait != null;
        if (hasPortrait)
        {
            portraitImage.sprite = portrait;
        }
    }

    public void UpdateVisuals(bool isP1Selected, bool isP2Selected, Color p1Color, Color p2Color)
    {
        if (isP1Selected && isP2Selected)
        {
            borderImage.enabled = true;
            borderImage.color = Color.Lerp(p1Color, p2Color, 0.5f);
        }
        else if (isP1Selected)
        {
            borderImage.enabled = true;
            borderImage.color = p1Color;
        }
        else if (isP2Selected)
        {
            borderImage.enabled = true;
            borderImage.color = p2Color;
        }
        else
        {
            borderImage.enabled = false;
        }
    }
}