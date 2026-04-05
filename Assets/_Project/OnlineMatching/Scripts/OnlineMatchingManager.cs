using UnityEngine;
using UnityEngine.UI;

public class OnlineMatchingManager : MonoBehaviour
{
    public PlayerSettingController playerSetting;
    public LobbyUIManager lobbyUI;
    
    public Button returnButton;

    private int currentSelectedSide = 0;

    private void Start()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.InitializeNetwork("127.0.0.1", 9000);
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

        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnSearchRoomResponseReceived += HandleSearchRoomResponse;
            ServerNetworkManager.Instance.OnJoinRoomResponseReceived += HandleJoinRoomResponse;
        }
    }

    private void OnDestroy()
    {
        if (playerSetting != null)
        {
            playerSetting.OnSideSelected -= HandleSideChanged;
        }

        if (lobbyUI != null)
        {
            lobbyUI.OnCreateRoomRequested -= HandleCreateRoom;
            lobbyUI.OnTitleSearchRequested -= HandleTitleSearch;
            lobbyUI.OnCodeJoinRequested -= HandleCodeJoin;
            lobbyUI.OnJoinWithPasswordRequested -= HandleJoinWithPassword;
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(ReturnToPreviousScene);
        }

        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnSearchRoomResponseReceived -= HandleSearchRoomResponse;
            ServerNetworkManager.Instance.OnJoinRoomResponseReceived -= HandleJoinRoomResponse;
        }
    }

    private void HandleSideChanged(int side)
    {
        currentSelectedSide = side;
    }

    private void HandleCreateRoom(RoomCreateData data)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendCreateRoomRequest(data);
        }
    }

    private void HandleTitleSearch(string titleQuery)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendSearchRoomRequest(0, titleQuery);
        }
    }

    private void HandleCodeJoin(string roomCode)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendSearchRoomRequest(1, roomCode);
        }
    }

    private void HandleJoinWithPassword(string roomCode, string password)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendJoinRoomRequest(roomCode, password);
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
            
            if (ServerNetworkManager.Instance != null)
            {
                ServerNetworkManager.Instance.SendSideUpdate(currentSelectedSide);
            }

            if (RoomStateManager.Instance != null)
            {
                RoomStateManager.Instance.ChangeRoomState(RoomStateType.Lobby);
            }
        }
        else
        {
            Debug.LogWarning($"방 진입 실패 사유: {roomCodeOrReason}");
            
            if (lobbyUI != null)
            {
                if (roomCodeOrReason == "RoomFull.")
                {
                    lobbyUI.HandleRoomFullFailure();
                }
                else if (roomCodeOrReason == "IncorrectPassword.")
                {
                    lobbyUI.HandlePasswordFailure();
                }
            }
        }
    }

    private void ReturnToPreviousScene()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.GoBack();
        }
    }
}