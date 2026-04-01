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

    private void UpdatePingVisual(int pingMs)
    {
        pingText.text = $"{pingMs} ms";
        
        if (pingMs <= 50) pingText.color = Color.green;
        else if (pingMs <= 100) pingText.color = Color.yellow;
        else pingText.color = Color.red;
    }
}