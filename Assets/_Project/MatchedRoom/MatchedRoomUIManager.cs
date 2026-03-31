using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class MatchedRoomUIManager : MonoBehaviour
{
    public TextMeshProUGUI textP1Name;
    public TextMeshProUGUI textP1Level;
    public TextMeshProUGUI textP2Name;
    public TextMeshProUGUI textP2Level;

    public TextMeshProUGUI textRoomPlayedGames;
    public TextMeshProUGUI textRoomP1Wins;
    public TextMeshProUGUI textRoomP2Wins;
    public TextMeshProUGUI textRoomChat;

    public Button btnToggleReadyP1;
    public Button btnToggleReadyP2;
    public Button btnGameStart;
    public Button btnReturn;

    private bool isP1Ready;
    private bool isP2Ready;
    private bool isHost;

    private void Start()
    {
        if (btnToggleReadyP1 != null) btnToggleReadyP1.onClick.AddListener(OnClickReadyP1);
        if (btnToggleReadyP2 != null) btnToggleReadyP2.onClick.AddListener(OnClickReadyP2);
        if (btnGameStart != null) btnGameStart.onClick.AddListener(OnClickGameStart);
        if (btnReturn != null) btnReturn.onClick.AddListener(OnClickReturn);

        InitializeDefaultState();
    }

    private void OnDestroy()
    {
        if (btnToggleReadyP1 != null) btnToggleReadyP1.onClick.RemoveAllListeners();
        if (btnToggleReadyP2 != null) btnToggleReadyP2.onClick.RemoveAllListeners();
        if (btnGameStart != null) btnGameStart.onClick.RemoveAllListeners();
        if (btnReturn != null) btnReturn.onClick.RemoveAllListeners();
    }

    public void UpdatePlayerInfo(int playerIndex, string playerName, int playerLevel)
    {
        if (playerIndex == 1)
        {
            if (textP1Name != null) textP1Name.text = playerName;
            if (textP1Level != null) textP1Level.text = $"Lv. {playerLevel}";
        }
        else if (playerIndex == 2)
        {
            if (textP2Name != null) textP2Name.text = playerName;
            if (textP2Level != null) textP2Level.text = $"Lv. {playerLevel}";
        }
    }

    public void UpdateRoomStats(int totalGames, int p1Wins, int p2Wins)
    {
        if (textRoomPlayedGames != null) textRoomPlayedGames.text = $"Played Games: {totalGames}";
        if (textRoomP1Wins != null) textRoomP1Wins.text = $"1P Wins: {p1Wins}";
        if (textRoomP2Wins != null) textRoomP2Wins.text = $"2P Wins: {p2Wins}";
    }

    public void SetPlayerReadyState(int playerIndex, bool isReady)
    {
        if (playerIndex == 1)
        {
            isP1Ready = isReady;
            UpdateButtonColor(btnToggleReadyP1, isP1Ready);
        }
        else if (playerIndex == 2)
        {
            isP2Ready = isReady;
            UpdateButtonColor(btnToggleReadyP2, isP2Ready);
        }

        CheckGameStartCondition();
    }

    public void SetupRoomAuthority(bool isUserHost)
    {
        isHost = isUserHost;
        
        if (btnToggleReadyP1 != null) btnToggleReadyP1.interactable = isHost;
        if (btnToggleReadyP2 != null) btnToggleReadyP2.interactable = !isHost;
        
        if (btnGameStart != null)
        {
            btnGameStart.gameObject.SetActive(isHost);
            btnGameStart.interactable = false;
        }
    }

    public void AppendChatMessage(string sender, string message)
    {
        if (textRoomChat != null)
        {
            textRoomChat.text += $"\n[{sender}]: {message}";
        }
    }

    private void OnClickReadyP1()
    {
        if (!isHost) return;
        
        SetPlayerReadyState(1, !isP1Ready);
    }

    private void OnClickReadyP2()
    {
        if (isHost) return;

        SetPlayerReadyState(2, !isP2Ready);
    }

    private void OnClickGameStart()
    {
        if (isHost && isP1Ready && isP2Ready)
        {
            Debug.Log("Game Start Requested.");
        }
    }

    private void OnClickReturn()
    {
        Debug.Log("Return to Lobby Requested.");
    }

    private void InitializeDefaultState()
    {
        UpdatePlayerInfo(1, "Waiting...", 0);
        UpdatePlayerInfo(2, "Waiting...", 0);
        UpdateRoomStats(0, 0, 0);
        SetPlayerReadyState(1, false);
        SetPlayerReadyState(2, false);
        
        if (textRoomChat != null) textRoomChat.text = string.Empty;
    }

    private void CheckGameStartCondition()
    {
        if (isHost && btnGameStart != null)
        {
            btnGameStart.interactable = (isP1Ready && isP2Ready);
        }
    }

    private void UpdateButtonColor(Button btn, bool isReady)
    {
        if (btn == null) return;
        
        ColorBlock colors = btn.colors;
        colors.normalColor = isReady ? Color.green : Color.white;
        colors.selectedColor = isReady ? Color.green : Color.white;
        btn.colors = colors;
    }
}