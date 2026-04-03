using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System;

public class ServerNetworkManager : MonoBehaviour
{
    public static ServerNetworkManager Instance { get; private set; }

    public event Action OnConnectionEstablished;
    public event Action<byte, RoomMetadata[]> OnSearchRoomResponseReceived;
    public event Action<bool, string, bool> OnJoinRoomResponseReceived;
    public event Action<RoomStateModel> OnRoomStateBroadcastReceived;

    public event Action<int, bool, int, int, bool, int> OnSelectBroadcastReceived;
    public event Action<bool> OnCountdownUpdateReceived;
    public event Action OnStartButtonActiveReceived;
    public event Action<GameSceneType> OnSceneChangeReceived;
    public event Action OnGameStartReceived;
    public event Action<int> OnSlotAssignedReceived;
    public event Action<GameSceneType> OnMatchAbortedReceived;
    public event Action OnRoundVerifiedReceived;
    public event Action<bool, bool> OnRematchSyncReceived;

    private NetworkDriver serverDriver;
    private NetworkConnection serverConnection;
    
    private bool isInitialized;
    private bool isConnected;
    private float lastPingTime;
    private float lastServerPacketReceiveTime;

    private const float PING_INTERVAL = 2.0f;
    private const float SERVER_TIMEOUT_LIMIT = 10.0f;

    private const byte SERVER_PING_PACKET = 22;
    private const byte SERVER_PONG_PACKET = 23;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 창이 내려가도 네트워크 패킷 펌핑을 멈추지 않도록 설정
        Application.runInBackground = true;
    }

    private void Update()
    {
        PumpServerEvents();
        ProcessServerPing();
    }

    private void OnDestroy()
    {
        CleanupDriver();
    }

    /*
     * 매칭 서버로 네트워크 소켓 연결을 시도합니다.
     */
    public void InitializeNetwork(string serverIp, ushort port)
    {
        if (isInitialized) return;

        serverDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(serverIp, port);
        serverConnection = serverDriver.Connect(endpoint);
        
        lastServerPacketReceiveTime = Time.realtimeSinceStartup;
        isInitialized = true;
    }

    /*
     * 대문자로 변경된 UI 데이터 모델을 기반으로 방 생성 요청을 서버에 전송합니다.
     */
    public void SendCreateRoomRequest(RoomCreateData data)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.CreateRoomRequest);
            writer.WriteFixedString64(new FixedString64Bytes(data.RoomName));
            writer.WriteByte((byte)(data.IsPrivate ? 1 : 0));
            writer.WriteByte((byte)(data.UsePassword ? 1 : 0));
            writer.WriteFixedString64(new FixedString64Bytes(data.UsePassword ? data.Password : ""));
            
            serverDriver.EndSend(writer);
        }
    }

    /*
     * 룸 코드 또는 제목으로 방 검색 요청을 서버에 발송합니다.
     */
    public void SendSearchRoomRequest(byte searchType, string query)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.SearchRoomRequest);
            writer.WriteByte(searchType);
            writer.WriteFixedString64(new FixedString64Bytes(query));
            serverDriver.EndSend(writer);
        }
    }

    /*
     * 특정 룸 코드와 비밀번호를 사용하여 방 접속 요청을 발송합니다.
     */
    public void SendJoinRoomRequest(string roomCode, string password)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.JoinRoomRequest);
            writer.WriteFixedString64(new FixedString64Bytes(roomCode));
            writer.WriteFixedString64(new FixedString64Bytes(password ?? ""));
            serverDriver.EndSend(writer);
        }
    }

    public void SendRuleUpdate(int rounds, int timeLimit)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.RuleUpdate);
            writer.WriteInt(rounds);
            writer.WriteInt(timeLimit);
            serverDriver.EndSend(writer);
        }
    }

    public void SendReadyStateUpdate(bool isReady)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.ReadyStateUpdate);
            writer.WriteByte((byte)(isReady ? 1 : 0));
            serverDriver.EndSend(writer);
        }
    }
    
    public void SendLobbyStartRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.LobbyStartRequest);
            serverDriver.EndSend(writer);
        }
    }

    public void SendRoomLeaveRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.RoomLeaveRequest);
            serverDriver.EndSend(writer);
        }
    }


    /*
     * 캐릭터 선택 락인(Lock-in) 정보를 서버로 전송합니다.
     */
    public void SendSelectUpdate(int playerIndex, int characterIndex, bool isLocked)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.SelectUpdate);
            writer.WriteInt(playerIndex);
            writer.WriteInt(characterIndex);
            writer.WriteByte((byte)(isLocked ? 1 : 0));
            serverDriver.EndSend(writer);
        }
    }

    /*
     * 플레이어의 시작 진영(Side) 정보를 서버로 전송합니다.
     */
    public void SendSideUpdate(int side)
    {
        if (!isConnected || !serverConnection.IsCreated) return;
        
        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.SideUpdate);
            writer.WriteInt(side);
            serverDriver.EndSend(writer);
        }
    }

    /*
     * 캐릭터 선택이 끝난 뒤 인게임 진입을 요청합니다.
     */
    public void SendStartRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;
        
        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.StartRequest);
            serverDriver.EndSend(writer);
        }
    }
    
    /*
     * 씬 로딩 및 P2P 준비 완료를 서버에 통보합니다.
     */
    public void SendHandshake()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.Handshake);
            serverDriver.EndSend(writer);
            Debug.Log("[CL-NET] Handshake packet sent to driver.");
        }
    }

    public void SendRoundEndReport(int winnerSlot, int p1Wins, int p2Wins)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.RoundEndReport);
            writer.WriteInt(winnerSlot);
            writer.WriteInt(p1Wins);
            writer.WriteInt(p2Wins);
            serverDriver.EndSend(writer);
        }
    }

    public void SendMatchEndAction(MatchEndActionType actionType)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.MatchEndActionRequest);
            writer.WriteByte((byte)actionType);
            serverDriver.EndSend(writer);
        }
    }

    /*
     * 디싱크(Desync) 발생 시 매치 무효화 요청을 서버로 보냅니다.
     */
    public void SendMatchAbortRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.MatchAborted);
            serverDriver.EndSend(writer);
        }
    }

    /*
     * 비정상 연결 종료 시 서버에 이를 보고합니다.
     */
    public void SendReportDisconnect()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.ReportDisconnect);
            serverDriver.EndSend(writer);
        }
    }

    /*
     * 서버와의 연결 유지를 위해 주기적으로 Ping을 발송합니다.
     */
    private void ProcessServerPing()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        float currentTime = Time.realtimeSinceStartup;
        
        if (currentTime - lastServerPacketReceiveTime > SERVER_TIMEOUT_LIMIT)
        {
            HandleServerTimeout();
            return;
        }

        if (currentTime - lastPingTime > PING_INTERVAL)
        {
            lastPingTime = currentTime;
            
            int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
            if (sendStatus == 0)
            {
                writer.WriteByte(SERVER_PING_PACKET);
                writer.WriteFloat(currentTime);
                serverDriver.EndSend(writer);
            }
        }
    }

    /*
     * 유니티 틱마다 수신된 네트워크 이벤트를 펌핑합니다.
     */
    private void PumpServerEvents()
    {
        if (!isInitialized || !serverDriver.IsCreated) return;

        serverDriver.ScheduleUpdate().Complete();
        ProcessConnectionEvents();
    }

    /*
     * 연결 수립, 데이터 수신, 연결 단절 이벤트를 분기하여 처리합니다.
     */
    private void ProcessConnectionEvents()
    {
        DataStreamReader stream;
        NetworkEvent.Type cmd;

        while ((cmd = serverDriver.PopEventForConnection(serverConnection, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                isConnected = true;
                lastServerPacketReceiveTime = Time.realtimeSinceStartup;
                OnConnectionEstablished?.Invoke();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                byte packetType = stream.ReadByte();
                HandleServerData(packetType, ref stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                isConnected = false;
                serverConnection = default;
                OnMatchAbortedReceived?.Invoke(GameSceneType.Start);
            }
        }
    }

    /*
     * 수신된 패킷 타입에 맞춰 데이터를 디코딩하고 각 씬 매니저로 이벤트를 발생시킵니다.
     */
    private void HandleServerData(byte packetType, ref DataStreamReader stream)
    {
        lastServerPacketReceiveTime = Time.realtimeSinceStartup;

        if (packetType == NetworkPacketType.SearchRoomResponse)
        {
            byte searchType = stream.ReadByte();
            int count = stream.ReadInt();
            RoomMetadata[] rooms = new RoomMetadata[count];
            
            for (int i = 0; i < count; i++)
            {
                rooms[i] = new RoomMetadata();
                rooms[i].RoomCode = stream.ReadFixedString64().ToString();
                rooms[i].RoomTitle = stream.ReadFixedString64().ToString();
                rooms[i].PlayerCount = stream.ReadByte();
                rooms[i].HasPassword = stream.ReadByte() == 1;
                rooms[i].MaxPlayerCount = 2;
            }
            
            OnSearchRoomResponseReceived?.Invoke(searchType, rooms);
        }
        else if (packetType == NetworkPacketType.JoinRoomResponse)
        {
            bool isSuccess = stream.ReadByte() == 1;
            string roomCodeOrReason = stream.ReadFixedString64().ToString();
            bool isHost = stream.ReadByte() == 1;
            
            OnJoinRoomResponseReceived?.Invoke(isSuccess, roomCodeOrReason, isHost);
        }
        else if (packetType == NetworkPacketType.RoomStateBroadcast)
        {
            RoomStateModel updatedModel = new RoomStateModel();
            
            updatedModel.isP1Connected = stream.ReadByte() == 1;
            updatedModel.isP2Connected = stream.ReadByte() == 1;
            
            updatedModel.maxRounds = stream.ReadInt();
            updatedModel.roundTimeLimit = stream.ReadInt();
            
            updatedModel.p1Wins = stream.ReadInt();
            updatedModel.p1Losses = stream.ReadInt();
            updatedModel.p2Wins = stream.ReadInt();
            updatedModel.p2Losses = stream.ReadInt();
            
            updatedModel.isP1Ready = stream.ReadByte() == 1;
            updatedModel.isP2Ready = stream.ReadByte() == 1;

            OnRoomStateBroadcastReceived?.Invoke(updatedModel);
        }
        else if (packetType == NetworkPacketType.SelectBroadcast)
        {
            int p1Idx = stream.ReadInt();
            bool p1Lock = stream.ReadByte() == 1;
            int p1Side = stream.ReadInt();
            int p2Idx = stream.ReadInt();
            bool p2Lock = stream.ReadByte() == 1;
            int p2Side = stream.ReadInt();
            OnSelectBroadcastReceived?.Invoke(p1Idx, p1Lock, p1Side, p2Idx, p2Lock, p2Side);
        }
        else if (packetType == NetworkPacketType.AssignSlot)
        {
            int assignedSlot = stream.ReadInt();
            OnSlotAssignedReceived?.Invoke(assignedSlot);
        }
        else if (packetType == NetworkPacketType.CountdownUpdate)
        {
            bool isStarted = stream.ReadByte() == 1;
            OnCountdownUpdateReceived?.Invoke(isStarted);
        }
        else if (packetType == NetworkPacketType.StartButtonActive)
        {
            OnStartButtonActiveReceived?.Invoke();
        }
        else if (packetType == NetworkPacketType.SceneChange)
        {
            int targetSceneIndex = stream.ReadInt();
            GameSceneType targetSceneType = (GameSceneType)targetSceneIndex;
            OnSceneChangeReceived?.Invoke(targetSceneType);
        }
        else if (packetType == NetworkPacketType.GameStart)
        {
            stream.ReadFixedString64();
            OnGameStartReceived?.Invoke();
        }
        else if (packetType == SERVER_PONG_PACKET)
        {
            float sentTime = stream.ReadFloat();
        }
        else if (packetType == NetworkPacketType.MatchAborted)
        {
            int sceneTypeInt = stream.ReadInt();
            OnMatchAbortedReceived?.Invoke((GameSceneType)sceneTypeInt);
        }
        else if (packetType == NetworkPacketType.RoundVerified)
        {
            OnRoundVerifiedReceived?.Invoke();
        }
        else if (packetType == NetworkPacketType.RematchSyncBroadcast)
        {
            bool p1Ready = stream.ReadByte() == 1;
            bool p2Ready = stream.ReadByte() == 1;
            OnRematchSyncReceived?.Invoke(p1Ready, p2Ready);
        }
    }

    /*
     * 서버와 지정된 시간 동안 통신이 없으면 연결을 강제 종료하고 메인으로 돌아갑니다.
     */
    private void HandleServerTimeout()
    {
        isConnected = false;
        if (serverConnection.IsCreated)
        {
            serverDriver.Disconnect(serverConnection);
        }
        serverConnection = default;
        OnMatchAbortedReceived?.Invoke(GameSceneType.Start);
    }

    /*
     * 네트워크 객체 소멸 시 드라이버 메모리를 안전하게 해제합니다.
     */
    private void CleanupDriver()
    {
        if (serverDriver.IsCreated)
        {
            if (serverConnection.IsCreated)
            {
                serverDriver.Disconnect(serverConnection);
            }
            serverDriver.ScheduleUpdate().Complete();
            serverDriver.Dispose();
            serverDriver = default;
        }
        serverConnection = default;
        isConnected = false;
        isInitialized = false;
    }
}