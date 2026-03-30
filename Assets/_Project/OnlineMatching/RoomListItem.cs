using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomListItem : MonoBehaviour
{
    public TextMeshProUGUI textRoomName;
    public TextMeshProUGUI textRoomCode;
    public TextMeshProUGUI textRoomInfo;
    public GameObject imageLockedPassword;
    public GameObject imageSelectionFrame;
    public Button btnSelect;

    public void Setup(string code, string title, int playerCount, int maxPlayers, bool hasPassword)
    {
        if (textRoomName != null) textRoomName.text = title;
        if (textRoomCode != null) textRoomCode.text = $"Code: {code}";
        
        if (textRoomInfo != null) 
        {
            textRoomInfo.text = $"Player: {playerCount} / {maxPlayers}\nNetwork Status: Good";
        }
        
        if (imageLockedPassword != null) 
        {
            imageLockedPassword.SetActive(hasPassword);
        }

        SetSelected(false);
    }

    public void SetSelected(bool isSelected)
    {
        if (imageSelectionFrame != null)
        {
            imageSelectionFrame.SetActive(isSelected);
        }
    }
}