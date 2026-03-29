using UnityEngine;
using UnityEngine.UI;

public class OnlineMatchingManager : MonoBehaviour
{
    public PlayerSettingController playerSetting;
    public LobbyUIManager lobbyUI;
    
    public Button returnButton;

    private void Start()
    {
        if (playerSetting != null)
        {
            playerSetting.OnSideSelected += HandleSideChanged;
        }

        if (lobbyUI != null)
        {
            lobbyUI.OnCreateRoomRequested += HandleCreateRoom;
            lobbyUI.OnTitleSearchRequested += HandleTitleSearch;
            lobbyUI.OnCodeJoinRequested += HandleCodeJoin;
            lobbyUI.OnJoinWithPasswordRequested += HandleJoinWithPassword;
        }

        if (returnButton != null)
        {
            returnButton.onClick.AddListener(ReturnToPreviousScene);
        }
    }

    private void HandleSideChanged(int side)
    {
        if (NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.GetIsConnected())
        {
            NetworkSessionManager.Instance.SendSideUpdate(side);
        }
    }

    private void HandleCreateRoom(string title, bool isPrivate, string password)
    {
        NetworkSessionManager.Instance.SendCreateRoomRequest(title, isPrivate, password);
    }

    private void HandleTitleSearch(string titleQuery)
    {
        NetworkSessionManager.Instance.SendSearchRoomRequest(0, titleQuery);
    }

    private void HandleCodeJoin(string roomCode)
    {
        NetworkSessionManager.Instance.SendSearchRoomRequest(1, roomCode);
    }

    private void HandleJoinWithPassword(string roomCode, string password)
    {
        NetworkSessionManager.Instance.SendJoinRoomRequest(roomCode, password);
    }

    private void ReturnToPreviousScene()
    {
        GameFlowManager.Instance.ChangeScene(GameSceneType.Start);
    }
}