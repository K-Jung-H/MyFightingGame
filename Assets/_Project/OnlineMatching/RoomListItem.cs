using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomListItem : MonoBehaviour
{
    public TextMeshProUGUI roomCodeText;
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playerCountText;
    public Button joinButton;

    public string currentRoomCode { get; private set; }

    public void Setup(string roomCode, string roomName, int currentPlayerCount, int maxPlayerCount)
    {
        currentRoomCode = roomCode;
        
        if (roomCodeText != null)
        {
            roomCodeText.text = $"Code: {roomCode}";
        }
        
        if (roomNameText != null)
        {
            roomNameText.text = roomName;
        }
        
        if (playerCountText != null)
        {
            playerCountText.text = $"{currentPlayerCount} / {maxPlayerCount}";
        }
    }
}