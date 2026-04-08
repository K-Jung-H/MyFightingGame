using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInfoPanel : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI pingText;
    [SerializeField] private TextMeshProUGUI recordText;
    [SerializeField] private TextMeshProUGUI statusText;

    public void UpdatePanel(string playerName, int pingMs, int wins, int losses, bool isReady, bool isOccupied)
    {
        if (!isOccupied)
        {
            ClearPanel();
            return;
        }

        gameObject.SetActive(true);
        nameText.text = playerName;
        recordText.text = $"{wins} <color=#00FF00>W</color> / {losses} <color=#FF0000>L</color>";
        statusText.text = isReady ? "<color=#00FF00>READY</color>" : "WAITING";

        UpdatePingVisual(pingMs);
    }

    public void ClearPanel()
    {
        nameText.text = "Player Waiting";
        pingText.text = "-";
        recordText.text = "-";
        statusText.text = "-";
        
        if (portraitImage != null)
        {
            portraitImage.color = new Color(1, 1, 1, 0.2f);
        }
    }

    public void UpdatePingVisual(int pingMs)
    {
        if (pingMs <= 0) 
        {
            pingText.text = "Ping: -";
            return;
        }

        string colorHex = "#00FFCC";
        string statusStr = "Good";

        if (pingMs >= 150) 
        {
            colorHex = "#FF4444";
            statusStr = "Bad";
        }
        else if (pingMs >= 80) 
        {
            colorHex = "#FFCC00";
            statusStr = "Fair";
        }

        pingText.text = $"Ping: <color={colorHex}>{pingMs}ms ({statusStr})</color>";
    }
}