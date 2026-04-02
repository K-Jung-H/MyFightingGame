using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;

/*
 * 서버에서 관리하는 개별 방의 상태와 접속자 정보, 패킷 로그를 보관하는 데이터 클래스입니다.
 */
public class ServerRoom
{
    public string roomCode;
    public string roomTitle;
    public bool isPrivate;
    public string password;

    public NetworkConnection p1;
    public NetworkConnection p2;
    public RoomStateModel stateModel;
    public bool isP1Ready;
    public bool isP2Ready;
    public bool isCountdownStarted;
    public bool isCountdownFinished;
    public float countdownTimer;
    public bool isP1StartRequested;
    public bool isP2StartRequested;

    public List<string> roomLogs;
    private const int MAX_ROOM_LOGS = 8;

    public ServerRoom(string code, string title, bool isPriv, string pwd)
    {
        roomCode = code;
        roomTitle = title;
        isPrivate = isPriv;
        password = pwd;

        p1 = default;
        p2 = default;
        stateModel = new RoomStateModel();
        stateModel.isStageLocked = true;
        isP1Ready = false;
        isP2Ready = false;
        isCountdownStarted = false;
        isCountdownFinished = false;
        countdownTimer = 3f;
        isP1StartRequested = false;
        isP2StartRequested = false;

        roomLogs = new List<string>();
    }

    public void LogRoomEvent(string msg)
    {
        roomLogs.Add(msg);
        if (roomLogs.Count > MAX_ROOM_LOGS)
        {
            roomLogs.RemoveAt(0);
        }
    }

    public bool IsFull()
    {
        return p1.IsCreated && p2.IsCreated;
    }

    public bool IsEmpty()
    {
        return !p1.IsCreated && !p2.IsCreated;
    }

    public bool HasPassword()
    {
        return !string.IsNullOrEmpty(password);
    }
}

/*
 * 클라이언트의 접속을 수락하고 패킷을 라우팅하며, 로비 시스템 및 P2P 매칭을 중재하는 더미 서버 클래스입니다.
 */
public class DummyMatchServer : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    
    private Dictionary<string, ServerRoom> activeRooms = new Dictionary<string, ServerRoom>();
    private Dictionary<NetworkConnection, ServerRoom> connectionToRoom = new Dictionary<NetworkConnection, ServerRoom>();
    
    private bool isMatchActive;
    private List<string> serverLogs = new List<string>();
    private const int MAX_LOG_LINES = 15;
    private Vector2 scrollPosition;

    /*
     * 매 프레임마다 네트워크 드라이버의 이벤트를 펌핑하고 클라이언트 패킷을 처리합니다.
     */
    private void Update()
    {
        if (!driver.IsCreated) return;

        driver.ScheduleUpdate().Complete();
        CleanupConnections();
        CleanupEmptyRooms();

        NetworkConnection c;
        while ((c = driver.Accept()) != default)
        {
            connections.Add(c);
            HandleNewConnection(c);
        }

        for (int i = 0; i < connections.Length; i++)
        {
            if (!connections[i].IsCreated) continue;

            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    ProcessData(connections[i], ref stream);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    HandleDisconnect(connections[i]);
                    connections[i] = default;
                }
            }
        }

        UpdateRoomTimers();
    }

    /*
     * 서버의 현재 상태와 활성화된 방의 패킷 로그를 화면에 시각화하여 렌더링합니다.
     */

    private void OnGUI()
    {
        GUI.skin.label.richText = true;

        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(350));
        
        GUILayout.BeginVertical("box");
        GUILayout.Label("<color=cyan><b>[ Lobby Server Status ]</b></color>");
        GUILayout.Space(5);
        GUILayout.Label($"Active Connections: {connectionToRoom.Count}");
        GUILayout.Label($"Active Rooms: {activeRooms.Count}");
        GUILayout.EndVertical();

        GUILayout.Space(15);

        GUILayout.BeginVertical("box");
        GUILayout.Label("<color=yellow><b>[ Global Server Event Logs ]</b></color>");
        GUILayout.Space(5);
        for (int i = 0; i < serverLogs.Count; i++)
        {
            GUILayout.Label(serverLogs[i]);
        }
        GUILayout.EndVertical();
        
        GUILayout.EndVertical();

        GUILayout.Space(20);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(600));
        
        foreach (var kvp in activeRooms)
        {
            ServerRoom room = kvp.Value;
            GUILayout.BeginVertical("box");
            
            int pCount = (room.p1.IsCreated ? 1 : 0) + (room.p2.IsCreated ? 1 : 0);
            GUILayout.Label($"<color=#00FFCC><b>[Room {room.roomCode}] {room.roomTitle} ({pCount}/2)</b></color>");
            
            string p1State = room.p1.IsCreated ? (room.stateModel.isP1Ready ? "<color=green>Ready</color>" : "<color=yellow>Waiting</color>") : "<color=grey>Empty</color>";
            string p2State = room.p2.IsCreated ? (room.stateModel.isP2Ready ? "<color=green>Ready</color>" : "<color=yellow>Waiting</color>") : "<color=grey>Empty</color>";
            
            GUILayout.Label($"Rounds: {room.stateModel.maxRounds}  |  Time: {room.stateModel.roundTimeLimit}");
            GUILayout.Label($"P1: {p1State}  |  P2: {p2State}");
            
            GUILayout.Space(5);
            GUILayout.Label("<b>--- Packet Logs ---</b>");
            for (int i = 0; i < room.roomLogs.Count; i++)
            {
                GUILayout.Label(room.roomLogs[i]);
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }
        
        GUILayout.EndScrollView();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    /*
     * 객체 파괴 시 네트워크 드라이버와 할당된 메모리를 안전하게 해제합니다.
     */
    private void OnDestroy()
    {
        if (driver.IsCreated)
        {
            driver.Dispose();
            connections.Dispose();
        }
    }

    /*
     * 포트 9000번으로 네트워크 드라이버를 바인딩하고 서버 수신 대기를 시작합니다.
     */
    public void StartServer()
    {
        driver = CreateConfiguredDriver();
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(9000);
        
        if (driver.Bind(endpoint) != 0)
        {
            LogEvent("Failed to bind port 9000.");
            return;
        }
        
        driver.Listen();
        connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        isMatchActive = false;
        
        LogEvent("Dedicated Lobby Server started on port 9000.");
    }

    /*
     * 인게임 매치 활성화 상태를 갱신합니다.
     */
    public void SetMatchActive(bool active)
    {
        isMatchActive = active;
        LogEvent($"Match active state changed to: {active}");
    }

    /*
     * 타임아웃 규칙이 적용된 네트워크 드라이버 인스턴스를 생성하여 반환합니다.
     */
    private NetworkDriver CreateConfiguredDriver()
    {
        NetworkSettings settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(
            disconnectTimeoutMS: 5000,
            heartbeatTimeoutMS: 500
        );
        return NetworkDriver.Create(settings);
    }

    /*
     * 연결이 해제된 네트워크 커넥션 인덱스를 배열에서 정리합니다.
     */
    private void CleanupConnections()
    {
        for (int i = 0; i < connections.Length; i++)
        {
            if (!connections[i].IsCreated)
            {
                connections.RemoveAtSwapBack(i);
                i--;
            }
        }
    }

    /*
     * 인원이 모두 빠져나간 빈 방을 탐색하여 메모리에서 삭제합니다.
     */
    private void CleanupEmptyRooms()
    {
        List<string> emptyRoomCodes = new List<string>();

        foreach (var kvp in activeRooms)
        {
            if (kvp.Value.IsEmpty())
            {
                emptyRoomCodes.Add(kvp.Key);
            }
        }

        foreach (string code in emptyRoomCodes)
        {
            LogEvent($"Room [{code}] is empty. Sweeping and destroying.");
            activeRooms.Remove(code);
        }
    }

    /*
     * 신규 클라이언트 접속 시 로깅 처리를 수행합니다.
     */
    private void HandleNewConnection(NetworkConnection conn)
    {
        LogEvent("New client connected. Awaiting Room Request.");
    }

    /*
     * 캐릭터 선택 씬 등의 대기열 카운트다운 타이머를 갱신합니다.
     */
    private void UpdateRoomTimers()
    {
        foreach (var kvp in activeRooms)
        {
            ServerRoom room = kvp.Value;
            if (room.isCountdownStarted)
            {
                room.countdownTimer -= Time.deltaTime;
                
                if (room.countdownTimer <= 0f)
                {
                    room.isCountdownStarted = false;
                    room.isCountdownFinished = true;
                    LogEvent($"Room [{room.roomCode}] countdown finished.");
                    BroadcastStartButtonActive(room);
                }
            }
        }
    }

    /*
     * 수신된 패킷의 타입 식별자를 읽고 해당하는 핸들러로 데이터를 전달합니다.
     */
    private void ProcessData(NetworkConnection conn, ref DataStreamReader stream)
    {
        byte packetType = stream.ReadByte();

        if (packetType == NetworkPacketType.CreateRoomRequest)
        {
            HandleCreateRoomRequest(conn, ref stream);
            return;
        }
        else if (packetType == NetworkPacketType.SearchRoomRequest)
        {
            HandleSearchRoomRequest(conn, ref stream);
            return;
        }
        else if (packetType == NetworkPacketType.JoinRoomRequest)
        {
            HandleJoinRoomRequest(conn, ref stream);
            return;
        }
        else if (packetType == 22) 
        {
            float sentTime = stream.ReadFloat();
            SendServerPong(conn, sentTime);
            return; 
        }
        
        if (!connectionToRoom.TryGetValue(conn, out ServerRoom currentRoom)) return;

        string sender = (conn == currentRoom.p1) ? "P1" : "P2";

        if (packetType == NetworkPacketType.RuleUpdate)
        {
            int rounds = stream.ReadInt();
            int timeLimit = stream.ReadInt();

            if (conn == currentRoom.p1)
            {
                currentRoom.stateModel.maxRounds = rounds;
                currentRoom.stateModel.roundTimeLimit = timeLimit;
                currentRoom.LogRoomEvent($"[{sender}] Rule Update -> R: {rounds}, T: {timeLimit}");
                BroadcastRoomState(currentRoom);
            }
        }
        else if (packetType == NetworkPacketType.ReadyStateUpdate)
        {
            bool isReady = stream.ReadByte() == 1;

            if (conn == currentRoom.p1)
            {
                currentRoom.stateModel.isP1Ready = isReady;
                currentRoom.LogRoomEvent($"[{sender}] Ready State -> {isReady}");
                BroadcastRoomState(currentRoom);
            }
            else if (conn == currentRoom.p2)
            {
                currentRoom.stateModel.isP2Ready = isReady;
                currentRoom.LogRoomEvent($"[{sender}] Ready State -> {isReady}");
                BroadcastRoomState(currentRoom);
            }
        }
        else if (packetType == NetworkPacketType.RoomLeaveRequest)
        {
            currentRoom.LogRoomEvent($"[{sender}] Requested to leave room.");
            HandleDisconnect(conn);
        }
        else if (packetType == NetworkPacketType.LobbyStartRequest)
        {
            currentRoom.LogRoomEvent($"[{sender}] Requested Lobby Start.");
            if (conn == currentRoom.p1 && currentRoom.stateModel.isP2Ready)
            {
                currentRoom.LogRoomEvent($"[Server] Broadcasting SceneChange.");
                BroadcastSceneChange(currentRoom);
            }
        }
        else if (packetType == NetworkPacketType.SelectUpdate)
        {
            int playerIdx = stream.ReadInt();
            int charIdx = stream.ReadInt();
            bool isLocked = stream.ReadByte() == 1;

            if (conn == currentRoom.p1) 
            {
                currentRoom.stateModel.p1CharacterIndex = charIdx;
                currentRoom.stateModel.isP1CharacterLocked = isLocked;
            }
            else if (conn == currentRoom.p2) 
            {
                currentRoom.stateModel.p2CharacterIndex = charIdx;
                currentRoom.stateModel.isP2CharacterLocked = isLocked;
            }

            BroadcastSelectState(currentRoom);

            if (currentRoom.stateModel.IsAllReadyToStart())
            {
                if (!currentRoom.isCountdownStarted && !currentRoom.isCountdownFinished)
                {
                    currentRoom.isCountdownStarted = true;
                    currentRoom.countdownTimer = 3f;
                    BroadcastCountdownState(currentRoom, true);
                }
            }
            else
            {
                currentRoom.isCountdownStarted = false;
                currentRoom.isCountdownFinished = false;
                currentRoom.countdownTimer = 3f;
                currentRoom.isP1StartRequested = false;
                currentRoom.isP2StartRequested = false;
                BroadcastCountdownState(currentRoom, false);
            }
        }
        else if (packetType == NetworkPacketType.SideUpdate)
        {
            int side = stream.ReadInt();
            if (conn == currentRoom.p1) currentRoom.stateModel.p1PreferredSide = side;
            else if (conn == currentRoom.p2) currentRoom.stateModel.p2PreferredSide = side;
            
            BroadcastSelectState(currentRoom);
        }
        else if (packetType == NetworkPacketType.StartRequest)
        {
            if (currentRoom.isCountdownFinished)
            {
                if (conn == currentRoom.p1) currentRoom.isP1StartRequested = true;
                else if (conn == currentRoom.p2) currentRoom.isP2StartRequested = true;

                if (currentRoom.isP1StartRequested && currentRoom.isP2StartRequested)
                {
                    LogEvent($"Room [{currentRoom.roomCode}] both players ready. Starting SceneChange.");
                    BroadcastSceneChange(currentRoom);
                }
            }
        }
        else if (packetType == NetworkPacketType.Handshake)
        {
            LogEvent($"[SV-NET] Handshake packet received from {conn.GetHashCode()}.");
            ProcessHandshake(conn, currentRoom);
        }
        else if (packetType == NetworkPacketType.ReportDisconnect)
        {
            ResolveDisconnect(conn, currentRoom);
        }
    }

    /*
     * 클라이언트의 방 생성 요청 데이터를 기반으로 새로운 ServerRoom 객체를 할당합니다.
     */
    private void HandleCreateRoomRequest(NetworkConnection conn, ref DataStreamReader stream)
    {
        string title = stream.ReadFixedString64().ToString();
        bool isPrivate = stream.ReadByte() == 1;
        bool usePassword = stream.ReadByte() == 1;
        string pwd = stream.ReadFixedString64().ToString();

        string actualPassword = usePassword ? pwd : string.Empty;

        string newCode = GenerateRoomCode();
        while (activeRooms.ContainsKey(newCode))
        {
            newCode = GenerateRoomCode();
        }

        ServerRoom newRoom = new ServerRoom(newCode, title, isPrivate, actualPassword);
        newRoom.p1 = conn;
        
        activeRooms[newCode] = newRoom;
        connectionToRoom[conn] = newRoom;

        LogEvent($"Created Room [{newCode}] Title: {title}, Private: {isPrivate}, Password: {usePassword}");

        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.JoinRoomResponse);
        writer.WriteByte(1);
        writer.WriteFixedString64(new FixedString64Bytes(newCode));
        writer.WriteByte(1); 
        driver.EndSend(writer);

        SendSlotId(conn, 0);
        BroadcastSelectState(newRoom);
    }

    /*
     * 활성화된 방 목록을 쿼리하여 검색 조건에 맞는 방 정보를 클라이언트에게 반환합니다.
     */
    private void HandleSearchRoomRequest(NetworkConnection conn, ref DataStreamReader stream)
    {
        byte searchType = stream.ReadByte();
        string query = stream.ReadFixedString64().ToString().ToLower();

        List<ServerRoom> matches = new List<ServerRoom>();

        foreach (var kvp in activeRooms)
        {
            ServerRoom room = kvp.Value;
            
            if (searchType == 0)
            {
                if (!room.isPrivate && room.roomTitle.ToLower().Contains(query))
                {
                    matches.Add(room);
                }
            }
            else if (searchType == 1)
            {
                if (room.roomCode.ToLower() == query)
                {
                    matches.Add(room);
                }
            }

            if (matches.Count >= 10) break;
        }

        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.SearchRoomResponse);
        writer.WriteByte(searchType);
        writer.WriteInt(matches.Count);

        foreach (var r in matches)
        {
            writer.WriteFixedString64(new FixedString64Bytes(r.roomCode));
            writer.WriteFixedString64(new FixedString64Bytes(r.roomTitle));
            byte pCount = (byte)((r.p1.IsCreated ? 1 : 0) + (r.p2.IsCreated ? 1 : 0));
            writer.WriteByte(pCount);
            writer.WriteByte((byte)(r.HasPassword() ? 1 : 0));
        }
        
        driver.EndSend(writer);
    }

    /*
     * 룸 코드와 비밀번호를 검증한 뒤 대상 방에 클라이언트를 접속시키고 상태 플래그를 갱신합니다.
     */
    private void HandleJoinRoomRequest(NetworkConnection conn, ref DataStreamReader stream)
    {
        string code = stream.ReadFixedString64().ToString();
        string pwd = stream.ReadFixedString64().ToString();

        bool success = false;
        string reason = "";

        if (activeRooms.TryGetValue(code, out ServerRoom targetRoom))
        {
            if (targetRoom.IsFull())
            {
                reason = "Room is full.";
            }
            else if (targetRoom.HasPassword() && targetRoom.password != pwd)
            {
                reason = "Incorrect password.";
            }
            else
            {
                success = true;
                targetRoom.p2 = conn;
                connectionToRoom[conn] = targetRoom;
                targetRoom.stateModel.isP2Connected = true;
                reason = "Success";
                LogEvent($"Client joined Room [{code}].");
            }
        }
        else
        {
            reason = "Room not found.";
        }

        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.JoinRoomResponse);
        writer.WriteByte(success ? (byte)1 : (byte)0);
        writer.WriteFixedString64(new FixedString64Bytes(success ? code : reason));
        writer.WriteByte(0); 
        driver.EndSend(writer);

        if (success)
        {
            SendSlotId(conn, 1);
            BroadcastRoomState(targetRoom);
        }
    }

    /*
     * 방에 접속된 모든 플레이어에게 현재 방의 종합 상태 모델을 전송합니다.
     */
    private void BroadcastRoomState(ServerRoom room)
    {
        if (room.p1.IsCreated) SendRoomState(room.p1, room);
        if (room.p2.IsCreated) SendRoomState(room.p2, room);
    }

    /*
     * 단일 네트워크 커넥션으로 방의 접속 정보, 룰, 승패 데이터를 직렬화하여 송신합니다.
     */
    private void SendRoomState(NetworkConnection conn, ServerRoom room)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.RoomStateBroadcast);
        
        writer.WriteByte((byte)(room.stateModel.isP1Connected ? 1 : 0));
        writer.WriteByte((byte)(room.stateModel.isP2Connected ? 1 : 0));
        
        writer.WriteInt(room.stateModel.maxRounds);
        writer.WriteInt(room.stateModel.roundTimeLimit);
        
        writer.WriteInt(room.stateModel.p1Wins);
        writer.WriteInt(room.stateModel.p1Losses);
        writer.WriteInt(room.stateModel.p2Wins);
        writer.WriteInt(room.stateModel.p2Losses);
        
        writer.WriteByte((byte)(room.stateModel.isP1Ready ? 1 : 0));
        writer.WriteByte((byte)(room.stateModel.isP2Ready ? 1 : 0));
        
        driver.EndSend(writer);
    }

    /*
     * P2P 연결 완료 패킷을 수신하고 양측이 모두 준비되었을 때 게임 시작 신호를 전파합니다.
     */
    private void ProcessHandshake(NetworkConnection conn, ServerRoom room)
    {
        if (conn == room.p1) room.isP1Ready = true;
        else if (conn == room.p2) room.isP2Ready = true;

        if (room.isP1Ready && room.isP2Ready)
        {
            LogEvent($"Room [{room.roomCode}] Handshake complete. Broadcasting GameStart.");
            SetMatchActive(true);
            BroadcastGameStart(room);
        }
    }

    /*
     * 플레이어의 연결이 끊어졌을 때 접속 상태를 정리하고 방장 권한을 승계하거나 방을 삭제합니다.
     */
    private void HandleDisconnect(NetworkConnection conn)
    {
        if (!connectionToRoom.TryGetValue(conn, out ServerRoom room)) return;

        bool isMatched = false;

        if (conn == room.p1)
        {
            LogEvent($"Room [{room.roomCode}] P1 Disconnected.");
            
            if (room.p2.IsCreated && !isMatchActive)
            {
                room.p1 = room.p2;
                room.p2 = default;
                
                room.stateModel.isP1Connected = room.stateModel.isP2Connected;
                room.stateModel.isP2Connected = false;
                
                room.stateModel.p1Wins = room.stateModel.p2Wins;
                room.stateModel.p1Losses = room.stateModel.p2Losses;
                room.stateModel.isP1Ready = room.stateModel.isP2Ready;
                room.stateModel.p1CharacterIndex = room.stateModel.p2CharacterIndex;
                room.stateModel.isP1CharacterLocked = room.stateModel.isP2CharacterLocked;
                room.stateModel.p1PreferredSide = room.stateModel.p2PreferredSide;

                room.stateModel.p2Wins = 0;
                room.stateModel.p2Losses = 0;
                room.stateModel.isP2Ready = false;
                room.stateModel.p2CharacterIndex = -1;
                room.stateModel.isP2CharacterLocked = false;
                
                SendSlotId(room.p1, 0);
            }
            else
            {
                room.p1 = default;
                room.stateModel.isP1Connected = false;
                room.stateModel.isP1CharacterLocked = false;
                room.isP1Ready = false;
                room.isP1StartRequested = false;
            }
            isMatched = true;
        }
        else if (conn == room.p2)
        {
            LogEvent($"Room [{room.roomCode}] P2 Disconnected.");
            room.p2 = default;
            room.stateModel.isP2Connected = false;
            room.stateModel.isP2CharacterLocked = false;
            room.isP2Ready = false;
            room.isP2StartRequested = false;
            isMatched = true;
        }

        connectionToRoom.Remove(conn);

        if (room.IsEmpty())
        {
            LogEvent($"Room [{room.roomCode}] is now empty. Destroying room.");
            activeRooms.Remove(room.roomCode);
            return;
        }

        if (isMatched)
        {
            if (isMatchActive)
            {
                NetworkConnection survivor = (conn == room.p1) ? room.p2 : room.p1;
                LogEvent($"Room [{room.roomCode}] Match active. Aborting match.");
                BroadcastMatchAborted(survivor, (int)GameSceneType.OnlineMatchedRoom);
                DestroyRoom(room);
                return;
            }

            room.isCountdownStarted = false;
            room.isCountdownFinished = false;
            room.countdownTimer = 3f;
            
            BroadcastCountdownState(room, false);
            BroadcastRoomState(room);
        }
    }

    /*
     * 접속한 플레이어에게 0(Host) 또는 1(Guest)의 고유 슬롯 아이디를 부여합니다.
     */
    private void SendSlotId(NetworkConnection conn, int slotId)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.AssignSlot);
        writer.WriteInt(slotId);
        driver.EndSend(writer);
    }

    /*
     * 클라이언트의 핑 계산을 위해 수신된 시간을 그대로 반환합니다.
     */
    private void SendServerPong(NetworkConnection conn, float receivedTime)
    {
        int sendStatus = driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(23);
            writer.WriteFloat(receivedTime);
            driver.EndSend(writer);
        }
    }

    /*
     * 캐릭터 선택 씬의 변경된 데이터를 모든 접속자에게 브로드캐스트합니다.
     */
    private void BroadcastSelectState(ServerRoom room)
    {
        if (room.p1.IsCreated) SendSelectState(room.p1, room);
        if (room.p2.IsCreated) SendSelectState(room.p2, room);
    }

    /*
     * 캐릭터 인덱스, 락인 여부, 진영 선택 정보를 직렬화하여 송신합니다.
     */
    private void SendSelectState(NetworkConnection conn, ServerRoom room)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.SelectBroadcast);
        writer.WriteInt(room.stateModel.p1CharacterIndex);
        writer.WriteByte((byte)(room.stateModel.isP1CharacterLocked ? 1 : 0));
        writer.WriteInt(room.stateModel.p1PreferredSide);
        writer.WriteInt(room.stateModel.p2CharacterIndex);
        writer.WriteByte((byte)(room.stateModel.isP2CharacterLocked ? 1 : 0));
        writer.WriteInt(room.stateModel.p2PreferredSide);
        driver.EndSend(writer);
    }

    /*
     * 양측의 캐릭터 선택 완료 시 발동하는 카운트다운 상태를 브로드캐스트합니다.
     */
    private void BroadcastCountdownState(ServerRoom room, bool isStarted)
    {
        if (room.p1.IsCreated) SendCountdownState(room.p1, isStarted);
        if (room.p2.IsCreated) SendCountdownState(room.p2, isStarted);
    }

    /*
     * 카운트다운 진행 여부 불리언 값을 네트워크 스트림에 작성하여 송신합니다.
     */
    private void SendCountdownState(NetworkConnection conn, bool isStarted)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.CountdownUpdate);
        writer.WriteByte((byte)(isStarted ? 1 : 0));
        driver.EndSend(writer);
    }

    /*
     * 카운트다운이 종료되어 게임 시작 버튼을 활성화하라는 신호를 브로드캐스트합니다.
     */
    private void BroadcastStartButtonActive(ServerRoom room)
    {
        if (room.p1.IsCreated) SendStartButtonActive(room.p1);
        if (room.p2.IsCreated) SendStartButtonActive(room.p2);
    }

    /*
     * 시작 버튼 활성화 패킷 식별자를 송신합니다.
     */
    private void SendStartButtonActive(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.StartButtonActive);
        driver.EndSend(writer);
    }

    /*
     * 로비 또는 캐릭터 선택 씬에서 다음 씬으로 넘어가라는 명령을 브로드캐스트합니다.
     */
    private void BroadcastSceneChange(ServerRoom room)
    {
        if (room.p1.IsCreated) SendSceneChange(room.p1);
        if (room.p2.IsCreated) SendSceneChange(room.p2);
    }

    /*
     * 씬 전환 명령 패킷 식별자를 송신합니다.
     */
    private void SendSceneChange(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.SceneChange);
        driver.EndSend(writer);
    }

    /*
     * 인게임 씬 로딩 완료 및 P2P 핸드쉐이크가 끝났음을 알리고 매치를 시작하게 합니다.
     */
    private void BroadcastGameStart(ServerRoom room)
    {
        if (room.p1.IsCreated) SendGameStart(room.p1);
        if (room.p2.IsCreated) SendGameStart(room.p2);
    }

    /*
     * 게임 시작 명령과 함께 연결할 상대방의 IP 주소를 송신합니다. (현재는 로컬 IP로 더미 처리)
     */
    private void SendGameStart(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.GameStart);
        FixedString64Bytes peerIp = new FixedString64Bytes("127.0.0.1");
        writer.WriteFixedString64(peerIp);
        driver.EndSend(writer);
    }

    /*
     * 클라이언트로부터 비정상 종료(Desync 등) 보고를 받았을 때 남은 인원에게 매치 중단을 알립니다.
     */
    private void ResolveDisconnect(NetworkConnection reporterConn, ServerRoom room)
    {
        isMatchActive = false;
        
        bool isP1Reporter = (reporterConn == room.p1);
        NetworkConnection suspectConn = isP1Reporter ? room.p2 : room.p1;
        
        int targetSceneInt = (int)GameSceneType.OnlineMatchedRoom;

        if (reporterConn.IsCreated) BroadcastMatchAborted(reporterConn, targetSceneInt);
        if (suspectConn.IsCreated) BroadcastMatchAborted(suspectConn, targetSceneInt);

        DestroyRoom(room);
    }

    /*
     * 매치가 무효화되었음을 알리고 돌아갈 타겟 씬 인덱스를 송신합니다.
     */
    private void BroadcastMatchAborted(NetworkConnection conn, int targetSceneInt)
    {
        if (!conn.IsCreated) return;

        int sendStatus = driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.MatchAborted);
            writer.WriteInt(targetSceneInt);
            driver.EndSend(writer);
        }
    }

    /*
     * 관리 중인 딕셔너리에서 대상을 제거하고 강제로 네트워크 연결을 끊어 방을 파괴합니다.
     */
    private void DestroyRoom(ServerRoom room)
    {
        LogEvent($"Forcibly destroying Room [{room.roomCode}].");
        
        if (room.p1.IsCreated)
        {
            connectionToRoom.Remove(room.p1);
            driver.Disconnect(room.p1);
        }
        if (room.p2.IsCreated)
        {
            connectionToRoom.Remove(room.p2);
            driver.Disconnect(room.p2);
        }
        
        activeRooms.Remove(room.roomCode);
    }

    /*
     * 서버 글로벌 로그 리스트에 메시지를 기록하고 한도를 초과하면 오래된 로그를 지웁니다.
     */
    private void LogEvent(string msg)
    {
        Debug.Log($"[Server] {msg}");
        serverLogs.Add(msg);
        if (serverLogs.Count > MAX_LOG_LINES)
        {
            serverLogs.RemoveAt(0);
        }
    }

    /*
     * 대문자 알파벳과 숫자를 조합하여 5자리의 고유한 룸 코드를 무작위로 생성합니다.
     */
    private string GenerateRoomCode()
    {
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for(int i = 0; i < 5; i++) 
        {
            code += chars[UnityEngine.Random.Range(0, chars.Length)];
        }
        return code;
    }
}