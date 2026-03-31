using UnityEngine;
using UnityEngine.UI;

public class OnlineMatchingManager : MonoBehaviour
{
    public PlayerSettingController playerSetting;
    public LobbyUIManager lobbyUI;
    
    public Button returnButton;

    private void Start()
    {
        if (NetworkSessionManager.Instance != null && !NetworkSessionManager.Instance.GetIsConnected())
        {
            NetworkSessionManager.Instance.InitializeNetwork("127.0.0.1");
        }

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

        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnSearchRoomResponseReceived += HandleSearchRoomResponse;
            NetworkSessionManager.Instance.OnJoinRoomResponseReceived += HandleJoinRoomResponse;
        }
    }

    private void HandleSideChanged(int side)
    {
        if (NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.GetIsConnected())
        {
            NetworkSessionManager.Instance.SendSideUpdate(side);
        }
    }

    private void HandleCreateRoom(RoomCreateData data)
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.SendCreateRoomRequest(data);
        }
    }

    private void HandleTitleSearch(string titleQuery)
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.SendSearchRoomRequest(0, titleQuery);
        }
    }

    private void HandleCodeJoin(string roomCode)
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.SendSearchRoomRequest(1, roomCode);
        }
    }

    private void HandleJoinWithPassword(string roomCode, string password)
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.SendJoinRoomRequest(roomCode, password);
        }
    }

    private void HandleSearchRoomResponse(byte searchType, RoomMetadata[] rooms)
    {
        if (lobbyUI != null)
        {
            lobbyUI.ShowSearchResults(rooms);
        }
    }

    private void HandleJoinRoomResponse(bool isSuccess, string roomCodeOrReason, bool isHost)
    {
        if (isSuccess)
        {
            Debug.Log($"방 진입 성공. 룸 코드: {roomCodeOrReason}, 호스트 여부: {isHost}");
            
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ChangeScene(GameSceneType.OnlineMatchedRoom);
            }
        }
        else
        {
            Debug.LogWarning($"방 진입 실패 사유: {roomCodeOrReason}");
            
            if (lobbyUI != null)
            {
                lobbyUI.HandlePasswordFailure();
            }
        }
    }

    private void ReturnToPreviousScene()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ChangeScene(GameSceneType.Start);
        }
    }
}