using UnityEngine;
using UnityEngine.UI;

public class OnlineMatchingManager : MonoBehaviour
{
    public PlayerSettingController playerSetting;
    public LobbyUIManager lobbyUI;
    
    public Button returnButton;

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

    /*
     * 진영 선택 변경 시 서버로 업데이트 패킷을 전송합니다.
     */
    private void HandleSideChanged(int side)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendSideUpdate(side);
        }
    }

    /*
     * 방 생성 요청을 서버로 전송합니다.
     */
    private void HandleCreateRoom(RoomCreateData data)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendCreateRoomRequest(data);
        }
    }

    /*
     * 방 제목 기반 검색 요청을 서버로 전송합니다.
     */
    private void HandleTitleSearch(string titleQuery)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendSearchRoomRequest(0, titleQuery);
        }
    }

    /*
     * 룸 코드 기반 검색 및 참가 요청을 서버로 전송합니다.
     */
    private void HandleCodeJoin(string roomCode)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendSearchRoomRequest(1, roomCode);
        }
    }

    /*
     * 비밀번호가 있는 방의 참가 요청을 서버로 전송합니다.
     */
    private void HandleJoinWithPassword(string roomCode, string password)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendJoinRoomRequest(roomCode, password);
        }
    }

    /*
     * 서버로부터 수신한 방 검색 결과를 로비 UI에 반영합니다.
     */
    private void HandleSearchRoomResponse(byte searchType, RoomMetadata[] rooms)
    {
        if (lobbyUI != null)
        {
            lobbyUI.ShowSearchResults(rooms);
        }
    }

    /*
     * 방 참가 요청에 대한 서버의 응답을 처리하고 성공 시 상태를 전환합니다.
     */
    private void HandleJoinRoomResponse(bool isSuccess, string roomCodeOrReason, bool isHost)
    {
        if (isSuccess)
        {
            Debug.Log($"방 진입 성공. 룸 코드: {roomCodeOrReason}, 호스트 여부: {isHost}");
            
            if (RoomStateManager.Instance != null)
            {
                //RoomStateManager.Instance.ChangeRoomState(RoomStateType.Lobby);
                RoomStateManager.Instance.ChangeRoomState(RoomStateType.CharacterSelect);
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

    /*
     * 뒤로 가기 버튼 클릭 시 매니저 파괴 처리를 위해 전역 매니저의 기능으로 되돌아갑니다.
     */
    private void ReturnToPreviousScene()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.GoBack();
        }
    }
}