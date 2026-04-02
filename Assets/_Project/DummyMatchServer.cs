using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;

/*
 * 서버에서 관리하는 개별 방의 상태와 접속자 정보, 패킷 로그 및 라운드 검증 데이터를 보관하는 클래스입니다.
 */
public class ServerRoom
{
    public string roomCode;
    public string roomTitle;
    public bool isPrivate;
    public string password;

    public RoomStateType currentState;

    public NetworkConnection p1;
    public NetworkConnection p2;
    public RoomStateModel stateModel;
    
    public bool isCountdownStarted;
    public bool isCountdownFinished;
    public float countdownTimer;
    public bool isP1StartRequested;
    public bool isP2StartRequested;

    public bool isP1RoundReported;
    public bool isP2RoundReported;
    public int p1ReportedWinner;
    public int p2ReportedWinner;
    public int p1ReportedWins;
    public int p2ReportedWins;

    public List<string> roomLogs;
    private const int MAX_ROOM_LOGS = 8;

    public ServerRoom(string code, string title, bool isPriv, string pwd)
    {
        roomCode = code;
        roomTitle = title;
        isPrivate = isPriv;
        password = pwd;

        currentState = RoomStateType.Lobby;

        p1 = default;
        p2 = default;
        stateModel = new RoomStateModel();
        stateModel.isStageLocked = true;
        
        isCountdownStarted = false;
        isCountdownFinished = false;
        countdownTimer = 3f;
        isP1StartRequested = false;
        isP2StartRequested = false;

        isP1RoundReported = false;
        isP2RoundReported = false;
        p1ReportedWinner = -1;
        p2ReportedWinner = -1;
        p1ReportedWins = 0;
        p2ReportedWins = 0;

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

    public bool IsFull() => p1.IsCreated && p2.IsCreated;
    public bool IsEmpty() => !p1.IsCreated && !p2.IsCreated;
    public bool HasPassword() => !string.IsNullOrEmpty(password);
}

/*
 * 클라이언트 접속 처리, 상태 머신 기반 패킷 라우팅 및 롤백 넷코드 무결성 검증을 담당하는 더미 매치 서버입니다.
 */
public class DummyMatchServer : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    
    private Dictionary<string, ServerRoom> activeRooms = new Dictionary<string, ServerRoom>();
    private Dictionary<NetworkConnection, ServerRoom> connectionToRoom = new Dictionary<NetworkConnection, ServerRoom>();
    
    private List<string> serverLogs = new List<string>();
    private const int MAX_LOG_LINES = 15;
    private Vector2 scrollPosition;

    /*
     * 매 프레임마다 네트워크 드라이버를 업데이트하고 연결 관리 및 카운트다운 갱신을 수행합니다.
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
            LogEvent("New client connected. Awaiting Room Request.");
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
     * 개선된 UI 레이아웃을 통해 서버의 글로벌 상태와 개별 방의 상태, 스코어, 패킷 로그를 렌더링합니다.
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
            GUILayout.Label($"<color=#00FFCC><b>[Room {room.roomCode}] {room.roomTitle} ({pCount}/2) - {room.currentState}</b></color>");
            
            string p1State = room.p1.IsCreated ? (room.stateModel.isP1Ready ? "<color=green>Ready</color>" : "<color=yellow>Waiting</color>") : "<color=grey>Empty</color>";
            string p2State = room.p2.IsCreated ? (room.stateModel.isP2Ready ? "<color=green>Ready</color>" : "<color=yellow>Waiting</color>") : "<color=grey>Empty</color>";
            
            GUILayout.Label($"Rounds: {room.stateModel.maxRounds}  |  Time: {room.stateModel.roundTimeLimit}");
            GUILayout.Label($"P1: {p1State}  |  P2: {p2State}");
            GUILayout.Label($"<color=white><b>Score -> P1: {room.stateModel.p1Wins}  |  P2: {room.stateModel.p2Wins}</b></color>");
            
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
     * 객체 파괴 시 드라이버와 메모리를 정리합니다.
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
     * 지정된 포트로 서버를 바인딩하고 연결 수신을 시작합니다.
     */
    public void StartServer()
    {
        NetworkSettings settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(disconnectTimeoutMS: 5000, heartbeatTimeoutMS: 500);
        driver = NetworkDriver.Create(settings);
        
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(9000);
        
        if (driver.Bind(endpoint) != 0)
        {
            LogEvent("Failed to bind port 9000.");
            return;
        }
        
        driver.Listen();
        connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        
        LogEvent("Dedicated Lobby Server started on port 9000.");
    }

    /*
     * 끊어진 연결 목록을 정리하여 배열을 최적화합니다.
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
     * 플레이어가 모두 떠난 빈 방을 식별하여 메모리에서 해제합니다.
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
     * 캐릭터 선택 씬 등의 3초 카운트다운 타이머를 관리하고 완료 시 시작 버튼을 활성화합니다.
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
                    room.LogRoomEvent($"[Server] Select countdown finished.");
                    BroadcastStartButtonActive(room);
                }
            }
        }
    }

    /*
     * 수신된 패킷을 식별하고 방의 현재 상태(RoomStateType)에 따라 적절한 핸들러로 라우팅합니다.
     */
    private void ProcessData(NetworkConnection conn, ref DataStreamReader stream)
    {
        byte packetType = stream.ReadByte();

        switch (packetType)
        {
            case NetworkPacketType.CreateRoomRequest: HandleCreateRoomRequest(conn, ref stream); return;
            case NetworkPacketType.SearchRoomRequest: HandleSearchRoomRequest(conn, ref stream); return;
            case NetworkPacketType.JoinRoomRequest: HandleJoinRoomRequest(conn, ref stream); return;
            case 22: SendServerPong(conn, stream.ReadFloat()); return;
        }
        
        if (!connectionToRoom.TryGetValue(conn, out ServerRoom currentRoom)) return;

        switch (packetType)
        {
            case NetworkPacketType.RoomLeaveRequest: 
                HandleRoomLeaveRequest(conn, currentRoom); 
                break;
            case NetworkPacketType.ReportDisconnect: 
                ResolveDisconnect(conn, currentRoom); 
                break;
            
            case NetworkPacketType.RuleUpdate:
            case NetworkPacketType.ReadyStateUpdate:
            case NetworkPacketType.LobbyStartRequest:
                if (currentRoom.currentState == RoomStateType.Lobby) 
                    HandleLobbyPackets(packetType, conn, currentRoom, ref stream);
                break;

            case NetworkPacketType.SelectUpdate:
            case NetworkPacketType.SideUpdate:
            case NetworkPacketType.StartRequest:
                if (currentRoom.currentState == RoomStateType.CharacterSelect) 
                    HandleCharacterSelectPackets(packetType, conn, currentRoom, ref stream);
                break;

            case NetworkPacketType.Handshake:
            case NetworkPacketType.RoundEndReport:
                if (currentRoom.currentState == RoomStateType.InGame) 
                    HandleInGamePackets(packetType, conn, currentRoom, ref stream);
                break;
        }
    }

    /*
     * 로비 씬 내부에서 발생하는 방 규칙 설정, 준비 상태 전환, 씬 시작 요청 패킷을 처리합니다.
     */
    private void HandleLobbyPackets(byte packetType, NetworkConnection conn, ServerRoom room, ref DataStreamReader stream)
    {
        string sender = (conn == room.p1) ? "P1" : "P2";

        switch (packetType)
        {
            case NetworkPacketType.RuleUpdate:
                int rounds = stream.ReadInt();
                int timeLimit = stream.ReadInt();
                if (conn == room.p1)
                {
                    room.stateModel.maxRounds = rounds;
                    room.stateModel.roundTimeLimit = timeLimit;
                    room.LogRoomEvent($"[{sender}] Rule Update -> R: {rounds}, T: {timeLimit}");
                    BroadcastRoomState(room);
                }
                break;

            case NetworkPacketType.ReadyStateUpdate:
                bool isReady = stream.ReadByte() == 1;
                if (conn == room.p1) room.stateModel.isP1Ready = isReady;
                else if (conn == room.p2) room.stateModel.isP2Ready = isReady;
                room.LogRoomEvent($"[{sender}] Ready State -> {isReady}");
                BroadcastRoomState(room);
                break;

            case NetworkPacketType.LobbyStartRequest:
                room.LogRoomEvent($"[{sender}] Requested Lobby Start.");
                if (conn == room.p1 && room.stateModel.isP2Ready)
                {
                    room.currentState = RoomStateType.CharacterSelect;
                    room.LogRoomEvent($"[Server] State Changed to CharacterSelect. Broadcasting SceneChange.");
                    
                    BroadcastSceneChange(room, (int)GameSceneType.CharacterSelect);
                }
                break;
        }
    }

    /*
     * 캐릭터 선택 씬에서 발생하는 캐릭터 락인, 진영 선택, 게임 시작 요청 패킷을 처리합니다.
     */
    private void HandleCharacterSelectPackets(byte packetType, NetworkConnection conn, ServerRoom room, ref DataStreamReader stream)
    {
        string sender = (conn == room.p1) ? "P1" : "P2";

        switch (packetType)
        {
            case NetworkPacketType.SelectUpdate:
                int playerIdx = stream.ReadInt();
                int charIdx = stream.ReadInt();
                bool isLocked = stream.ReadByte() == 1;

                if (conn == room.p1) 
                {
                    room.stateModel.p1CharacterIndex = charIdx;
                    room.stateModel.isP1CharacterLocked = isLocked;
                }
                else if (conn == room.p2) 
                {
                    room.stateModel.p2CharacterIndex = charIdx;
                    room.stateModel.isP2CharacterLocked = isLocked;
                }

                BroadcastSelectState(room);

                if (room.stateModel.IsAllReadyToStart())
                {
                    if (!room.isCountdownStarted && !room.isCountdownFinished)
                    {
                        room.isCountdownStarted = true;
                        room.countdownTimer = 3f;
                        BroadcastCountdownState(room, true);
                    }
                }
                else
                {
                    room.isCountdownStarted = false;
                    room.isCountdownFinished = false;
                    room.countdownTimer = 3f;
                    room.isP1StartRequested = false;
                    room.isP2StartRequested = false;
                    BroadcastCountdownState(room, false);
                }
                break;

            case NetworkPacketType.SideUpdate:
                int side = stream.ReadInt();
                if (conn == room.p1) room.stateModel.p1PreferredSide = side;
                else if (conn == room.p2) room.stateModel.p2PreferredSide = side;
                BroadcastSelectState(room);
                break;

            case NetworkPacketType.StartRequest:
                if (room.isCountdownFinished)
                {
                    if (conn == room.p1) room.isP1StartRequested = true;
                    else if (conn == room.p2) room.isP2StartRequested = true;

                    if (room.isP1StartRequested && room.isP2StartRequested)
                    {
                        room.currentState = RoomStateType.InGame;
                        room.LogRoomEvent($"[Server] State Changed to InGame. Broadcasting SceneChange.");
                        
                        BroadcastSceneChange(room, (int)GameSceneType.GamePlay); 
                    }
                }
                break;
        }
    }

    /*
     * 인게임 씬에서 발생하는 핸드쉐이크, 라운드 결과 교차 검증 패킷을 처리합니다.
     */
    private void HandleInGamePackets(byte packetType, NetworkConnection conn, ServerRoom room, ref DataStreamReader stream)
    {
        string sender = (conn == room.p1) ? "P1" : "P2";

        switch (packetType)
        {
            case NetworkPacketType.Handshake:
                room.LogRoomEvent($"[{sender}] Handshake complete.");
                if (conn == room.p1) room.stateModel.isP1Ready = true;
                else if (conn == room.p2) room.stateModel.isP2Ready = true;

                if (room.stateModel.isP1Ready && room.stateModel.isP2Ready)
                {
                    room.LogRoomEvent($"[Server] Both Handshakes complete. Broadcasting GameStart.");
                    BroadcastGameStart(room);
                }
                break;

            case NetworkPacketType.RoundEndReport:
                int reportedWinner = stream.ReadInt();
                int reportedP1Wins = stream.ReadInt();
                int reportedP2Wins = stream.ReadInt();

                room.LogRoomEvent($"[{sender}] Round Report -> Winner: {reportedWinner}, P1W: {reportedP1Wins}, P2W: {reportedP2Wins}");

                if (conn == room.p1)
                {
                    room.p1ReportedWinner = reportedWinner;
                    room.p1ReportedWins = reportedP1Wins;
                    room.isP1RoundReported = true;
                }
                else if (conn == room.p2)
                {
                    room.p2ReportedWinner = reportedWinner;
                    room.p2ReportedWins = reportedP2Wins;
                    room.isP2RoundReported = true;
                }

                if (room.isP1RoundReported && room.isP2RoundReported)
                {
                    if (room.p1ReportedWinner == room.p2ReportedWinner)
                    {
                        room.LogRoomEvent("[Server] Round Reports match. Broadcasting NextRoundStart.");
                        room.stateModel.p1Wins = room.p1ReportedWins;
                        room.stateModel.p2Wins = room.p2ReportedWins;
                        
                        room.isP1RoundReported = false;
                        room.isP2RoundReported = false;
                        
                        BroadcastNextRoundStart(room);
                    }
                    else
                    {
                        room.LogRoomEvent("[Server] DESYNC DETECTED! Round Reports mismatch. Forcing Match Abort.");
                        ResolveDisconnect(room.p1, room);
                    }
                }
                break;
        }
    }

    /*
     * 글로벌 영역에서 새로운 방을 생성하고 초기 상태를 할당합니다.
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

        LogEvent($"Created Room [{newCode}] Title: {title}, Private: {isPrivate}");

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
     * 활성화된 방 목록을 검색하여 매칭되는 결과를 클라이언트에게 반환합니다.
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
     * 룸 코드와 비밀번호를 확인하여 클라이언트를 방에 합류시킵니다.
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
     * 로컬 핑 계산을 위해 수신된 시간을 그대로 반환합니다.
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
     * 퇴장 요청을 처리하여 해당 연결을 방에서 분리합니다.
     */
    private void HandleRoomLeaveRequest(NetworkConnection conn, ServerRoom room)
    {
        string sender = (conn == room.p1) ? "P1" : "P2";
        room.LogRoomEvent($"[{sender}] Requested to leave room.");
        HandleDisconnect(conn);
    }

    /*
     * 플레이어 퇴장 또는 연결 유실 시 방장 권한을 승계하거나 매치를 중단시킵니다.
     */
    private void HandleDisconnect(NetworkConnection conn)
    {
        if (!connectionToRoom.TryGetValue(conn, out ServerRoom room)) return;

        bool isMatched = false;

        if (conn == room.p1)
        {
            LogEvent($"Room [{room.roomCode}] P1 Disconnected.");
            
            if (room.p2.IsCreated && room.currentState != RoomStateType.InGame)
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
                room.stateModel.isP1Ready = false;
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
            room.stateModel.isP2Ready = false;
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
            if (room.currentState == RoomStateType.InGame)
            {
                NetworkConnection survivor = (conn == room.p1) ? room.p2 : room.p1;
                LogEvent($"Room [{room.roomCode}] Match active. Aborting match.");
                room.currentState = RoomStateType.Lobby;
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
     * 디싱크 발생 또는 심각한 통신 오류 시 강제로 매치를 무효화하고 로비로 롤백시킵니다.
     */
    private void ResolveDisconnect(NetworkConnection reporterConn, ServerRoom room)
    {
        room.currentState = RoomStateType.Lobby;
        
        bool isP1Reporter = (reporterConn == room.p1);
        NetworkConnection suspectConn = isP1Reporter ? room.p2 : room.p1;
        
        int targetSceneInt = (int)GameSceneType.OnlineMatchedRoom;

        if (reporterConn.IsCreated) BroadcastMatchAborted(reporterConn, targetSceneInt);
        if (suspectConn.IsCreated) BroadcastMatchAborted(suspectConn, targetSceneInt);

        DestroyRoom(room);
    }

    /*
     * 방에 연결된 양측 클라이언트에게 룸 전체 데이터 모델을 동기화합니다.
     */
    private void BroadcastRoomState(ServerRoom room)
    {
        if (room.p1.IsCreated) SendRoomState(room.p1, room);
        if (room.p2.IsCreated) SendRoomState(room.p2, room);
    }

    /*
     * 로비 관련 데이터 필드를 바이트 스트림에 담아 전송합니다.
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
     * 클라이언트가 서버에 입장했을 때 자신의 슬롯 번호(0 또는 1)를 안내합니다.
     */
    private void SendSlotId(NetworkConnection conn, int slotId)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.AssignSlot);
        writer.WriteInt(slotId);
        driver.EndSend(writer);
    }

    /*
     * 캐릭터 선택 씬의 진영 및 락인 상태를 전파합니다.
     */
    private void BroadcastSelectState(ServerRoom room)
    {
        if (room.p1.IsCreated) SendSelectState(room.p1, room);
        if (room.p2.IsCreated) SendSelectState(room.p2, room);
    }

    /*
     * 캐릭터 인덱스와 락인 정보를 직렬화하여 전송합니다.
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
     * 캐릭터 락인 완료 후 3초 대기열의 활성 여부를 브로드캐스트합니다.
     */
    private void BroadcastCountdownState(ServerRoom room, bool isStarted)
    {
        if (room.p1.IsCreated) SendCountdownState(room.p1, isStarted);
        if (room.p2.IsCreated) SendCountdownState(room.p2, isStarted);
    }

    /*
     * 카운트다운 상태 불리언을 전송합니다.
     */
    private void SendCountdownState(NetworkConnection conn, bool isStarted)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.CountdownUpdate);
        writer.WriteByte((byte)(isStarted ? 1 : 0));
        driver.EndSend(writer);
    }

    /*
     * 카운트다운 완료를 알리고 UI에 시작 버튼을 노출시킵니다.
     */
    private void BroadcastStartButtonActive(ServerRoom room)
    {
        if (room.p1.IsCreated) SendStartButtonActive(room.p1);
        if (room.p2.IsCreated) SendStartButtonActive(room.p2);
    }

    /*
     * 시작 버튼 활성화 패킷을 전송합니다.
     */
    private void SendStartButtonActive(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.StartButtonActive);
        driver.EndSend(writer);
    }

    /*
     * 룸의 상태 머신이 변경되었을 때 클라이언트들에게 타겟 씬 전환을 명령합니다.
     */
    private void BroadcastSceneChange(ServerRoom room, int targetSceneInt)
    {
        if (room.p1.IsCreated) SendSceneChange(room.p1, targetSceneInt);
        if (room.p2.IsCreated) SendSceneChange(room.p2, targetSceneInt);
    }

    /*
     * 씬 전환 식별자 패킷과 목적지 씬의 정수 인덱스를 전송합니다.
     */
    private void SendSceneChange(NetworkConnection conn, int targetSceneInt)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.SceneChange);
        writer.WriteInt(targetSceneInt);
        driver.EndSend(writer);
    }

    /*
     * 인게임 씬의 핸드쉐이크가 완료된 후 실제 틱 시뮬레이션 시작을 알립니다.
     */
    private void BroadcastGameStart(ServerRoom room)
    {
        if (room.p1.IsCreated) SendGameStart(room.p1);
        if (room.p2.IsCreated) SendGameStart(room.p2);
    }

    /*
     * P2P 통신을 위한 로컬 IP와 게임 시작 패킷을 전송합니다.
     */
    private void SendGameStart(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.GameStart);
        writer.WriteFixedString64(new FixedString64Bytes("127.0.0.1"));
        driver.EndSend(writer);
    }

    /*
     * 라운드 종료 검증을 통과했을 때 클라이언트의 인게임 상태를 다음 라운드로 이행시킵니다.
     */
    private void BroadcastNextRoundStart(ServerRoom room)
    {
        if (room.p1.IsCreated) SendNextRoundStart(room.p1);
        if (room.p2.IsCreated) SendNextRoundStart(room.p2);
    }

    /*
     * 라운드 재개 패킷 식별자를 전송합니다.
     */
    private void SendNextRoundStart(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.NextRoundStart);
        driver.EndSend(writer);
    }

    /*
     * 비정상 종료 시 접속된 클라이언트들에게 에러 씬으로 돌아갈 것을 명령합니다.
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
     * 메모리 누수를 방지하기 위해 방 객체를 지우고 양측의 연결을 강제로 차단합니다.
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
     * 서버 글로벌 로그 컨테이너에 텍스트를 누적 기록합니다.
     */
    private void LogEvent(string msg)
    {
        Debug.Log($"[Server] {msg}");
        serverLogs.Add(msg);
        if (serverLogs.Count > MAX_LOG_LINES) serverLogs.RemoveAt(0);
    }

    /*
     * 새로운 방에 부여할 5자리 난수 코드를 생성합니다.
     */
    private string GenerateRoomCode()
    {
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for(int i = 0; i < 5; i++) code += chars[UnityEngine.Random.Range(0, chars.Length)];
        return code;
    }
}