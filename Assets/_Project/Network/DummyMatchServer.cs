using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;

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

    public int p1RoundWins;
    public int p2RoundWins;

    public int p1ReportedWinner;
    public int p2ReportedWinner;
    
    public int p1ReportedP1Wins;
    public int p1ReportedP2Wins;
    public int p2ReportedP1Wins;
    public int p2ReportedP2Wins;

    public bool isVotingStarted;
    public float votingTimer;
    public bool hasP1Voted;
    public bool hasP2Voted;

    public MatchEndActionType p1VoteAction;
    public MatchEndActionType p2VoteAction;

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

        p1RoundWins = 0;
        p2RoundWins = 0;

        p1ReportedWinner = -1;
        p2ReportedWinner = -1;
        
        p1ReportedP1Wins = 0;
        p1ReportedP2Wins = 0;
        p2ReportedP1Wins = 0;
        p2ReportedP2Wins = 0;

        hasP1Voted = false;
        hasP2Voted = false;
        isVotingStarted = false;
        votingTimer = 15f;

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

public class DummyMatchServer : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    
    private Dictionary<string, ServerRoom> activeRooms = new Dictionary<string, ServerRoom>();
    private Dictionary<NetworkConnection, ServerRoom> connectionToRoom = new Dictionary<NetworkConnection, ServerRoom>();
    
    private List<string> serverLogs = new List<string>();
    private const int MAX_LOG_LINES = 15;
    private Vector2 scrollPosition;

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
            GUILayout.Label($"<color=white><b>Match Score -> P1: {room.stateModel.p1Wins}  |  P2: {room.stateModel.p2Wins}</b></color>");
            
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

    private void OnDestroy()
    {
        if (driver.IsCreated)
        {
            driver.Dispose();
            connections.Dispose();
        }
    }

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

            if (room.isVotingStarted)
            {
                room.votingTimer -= Time.deltaTime;

                if (room.votingTimer <= 0f)
                {
                    room.isVotingStarted = false;
                    room.LogRoomEvent("[Server] Vote timeout. Forcing return to lobby.");
                    
                    ResetRoomForRematch(room);
                    room.currentState = RoomStateType.Lobby;
                    BroadcastRoomState(room);
                    BroadcastSceneChange(room, (int)GameSceneType.OnlineMatchedRoom);
                }
            }
        }
    }

    private void ProcessData(NetworkConnection conn, ref DataStreamReader stream)
    {
        byte packetType = stream.ReadByte();

        switch (packetType)
        {
            case NetworkPacketType.CreateRoomRequest: 
                HandleCreateRoomRequest(conn, ref stream); 
                return;
            case NetworkPacketType.SearchRoomRequest: 
                HandleSearchRoomRequest(conn, ref stream); 
                return;
            case NetworkPacketType.JoinRoomRequest: 
                HandleJoinRoomRequest(conn, ref stream); 
                return;
            case 22: 
                SendServerPong(conn, stream.ReadFloat()); 
                return;
        }
        
        if (!connectionToRoom.TryGetValue(conn, out ServerRoom currentRoom))
        {
            LogEvent($"<color=red>Dropped Packet [{packetType}] - Client not in a room.</color>");
            return;
        }

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
            case NetworkPacketType.SideUpdate:
                if (currentRoom.currentState == RoomStateType.Lobby) 
                    HandleLobbyPackets(packetType, conn, currentRoom, ref stream);
                else
                    LogEvent($"<color=red>Dropped Packet [{packetType}] - Invalid State ({currentRoom.currentState}).</color>");
                break;
            case NetworkPacketType.CancelPhaseRequest:
                if (currentRoom.currentState == RoomStateType.CharacterSelect)
                    HandleCancelPhaseRequest(conn, currentRoom);
                else
                    LogEvent($"<color=red>Dropped Packet [{packetType}] - Invalid State ({currentRoom.currentState}).</color>");
                break;
            case NetworkPacketType.SelectUpdate:
            case NetworkPacketType.StartRequest:
                if (currentRoom.currentState == RoomStateType.CharacterSelect) 
                    HandleCharacterSelectPackets(packetType, conn, currentRoom, ref stream);
                else
                    LogEvent($"<color=red>Dropped Packet [{packetType}] - Invalid State ({currentRoom.currentState}).</color>");
                break;
            case NetworkPacketType.Handshake:
            case NetworkPacketType.RoundEndReport:
            case NetworkPacketType.MatchEndActionRequest:
                if (currentRoom.currentState == RoomStateType.InGame) 
                    HandleInGamePackets(packetType, conn, currentRoom, ref stream);
                else
                    LogEvent($"<color=red>Dropped Packet [{packetType}] - Invalid State ({currentRoom.currentState}).</color>");
                break;
            default:
                LogEvent($"<color=red>Dropped Packet [{packetType}] - Unknown Packet Type.</color>");
                break;
        }
    }

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
            case NetworkPacketType.SideUpdate:
                int side = stream.ReadInt();
                if (conn == room.p1) room.stateModel.p1PreferredSide = side;
                else if (conn == room.p2) room.stateModel.p2PreferredSide = side;
                room.LogRoomEvent($"[{sender}] Side Update -> {side}");
                BroadcastSelectState(room);
                break;
        }
    }

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

    private void HandleCancelPhaseRequest(NetworkConnection conn, ServerRoom room)
    {
        string sender = (conn == room.p1) ? "P1" : "P2";
        room.LogRoomEvent($"[{sender}] Requested Phase Cancel.");

        if (room.currentState == RoomStateType.CharacterSelect)
        {
            room.currentState = RoomStateType.Lobby;
            
            room.stateModel.isP1CharacterLocked = false;
            room.stateModel.isP2CharacterLocked = false;
            room.stateModel.isP1Ready = false;
            room.stateModel.isP2Ready = false;
            
            room.isP1StartRequested = false;
            room.isP2StartRequested = false;
            room.isCountdownStarted = false;
            room.isCountdownFinished = false;
            room.countdownTimer = 3f;

            room.LogRoomEvent("[Server] Phase downgraded to Lobby. Broadcasting SceneChange.");
            
            BroadcastRoomState(room);
            BroadcastSceneChange(room, (int)GameSceneType.OnlineMatchedRoom);
        }
    }

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
                int reportedP1RoundWins = stream.ReadInt();
                int reportedP2RoundWins = stream.ReadInt();

                if (conn == room.p1)
                {
                    room.p1ReportedWinner = reportedWinner;
                    room.p1ReportedP1Wins = reportedP1RoundWins;
                    room.p1ReportedP2Wins = reportedP2RoundWins;
                    room.isP1RoundReported = true;
                }
                else if (conn == room.p2)
                {
                    room.p2ReportedWinner = reportedWinner;
                    room.p2ReportedP1Wins = reportedP1RoundWins;
                    room.p2ReportedP2Wins = reportedP2RoundWins;
                    room.isP2RoundReported = true;
                }

                if (room.isP1RoundReported && room.isP2RoundReported)
                {
                    bool isWinnerMatch = room.p1ReportedWinner == room.p2ReportedWinner;
                    bool isScoreMatch = (room.p1ReportedP1Wins == room.p2ReportedP1Wins) && 
                                        (room.p1ReportedP2Wins == room.p2ReportedP2Wins);

                    if (isWinnerMatch && isScoreMatch)
                    {
                        room.LogRoomEvent("[Server] Round Reports match. Broadcasting RoundVerified.");
                        
                        room.p1RoundWins = room.p1ReportedP1Wins;
                        room.p2RoundWins = room.p1ReportedP2Wins;
                        
                        room.isP1RoundReported = false;
                        room.isP2RoundReported = false;

                        int requiredRoundWins = (room.stateModel.maxRounds / 2) + 1;
                        bool isMatchOver = room.p1RoundWins >= requiredRoundWins || room.p2RoundWins >= requiredRoundWins;

                        if (isMatchOver)
                        {
                            room.isVotingStarted = true;
                            room.votingTimer = 15f;

                            if (room.p1RoundWins > room.p2RoundWins)
                            {
                                room.stateModel.p1Wins++;
                                room.stateModel.p2Losses++;
                            }
                            else if (room.p2RoundWins > room.p1RoundWins)
                            {
                                room.stateModel.p2Wins++;
                                room.stateModel.p1Losses++;
                            }
                        }
                        
                        BroadcastRoundVerified(room);
                    }
                    else
                    {
                        room.LogRoomEvent("[Server] DESYNC DETECTED! Round Reports mismatch. Forcing Match Abort.");
                        ResolveDisconnect(room.p1, room);
                    }
                }
                break;
            case NetworkPacketType.MatchEndActionRequest:
                MatchEndActionType action = (MatchEndActionType)stream.ReadByte();

                if (conn == room.p1) 
                {
                    room.hasP1Voted = true;
                    room.p1VoteAction = action;
                }
                else if (conn == room.p2) 
                {
                    room.hasP2Voted = true;
                    room.p2VoteAction = action;
                }

                room.LogRoomEvent($"[{sender}] Vote Action: {action}");

                bool p1Rematch = room.hasP1Voted && room.p1VoteAction == MatchEndActionType.Rematch;
                bool p2Rematch = room.hasP2Voted && room.p2VoteAction == MatchEndActionType.Rematch;
                BroadcastRematchSync(room, p1Rematch, p2Rematch);

                EvaluateMatchEndVotes(room);
                break;
        }
    }

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

    private void HandleRoomLeaveRequest(NetworkConnection conn, ServerRoom room)
    {
        string sender = (conn == room.p1) ? "P1" : "P2";
        room.LogRoomEvent($"[{sender}] Requested to leave room.");
        
        BroadcastMatchAborted(conn, (int)GameSceneType.OnlineLobby);
        
        HandleDisconnect(conn);
    }

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
                room.stateModel.p2Wins = 0;
                room.stateModel.p2Losses = 0;

                room.stateModel.isP1Ready = room.stateModel.isP2Ready;
                room.stateModel.p1CharacterIndex = room.stateModel.p2CharacterIndex;
                room.stateModel.isP1CharacterLocked = room.stateModel.isP2CharacterLocked;
                room.stateModel.p1PreferredSide = room.stateModel.p2PreferredSide;

                room.stateModel.isP2Ready = false;
                room.stateModel.p2CharacterIndex = 0;
                room.stateModel.isP2CharacterLocked = false;
                
                SendSlotId(room.p1, 0);
            }
            else
            {
                room.p1 = default;
                room.stateModel.isP1Connected = false;
                room.stateModel.p1Wins = 0;
                room.stateModel.p1Losses = 0;
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
            room.stateModel.p2Wins = 0;
            room.stateModel.p2Losses = 0;
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

    private void BroadcastRoomState(ServerRoom room)
    {
        if (room.p1.IsCreated) SendRoomState(room.p1, room);
        if (room.p2.IsCreated) SendRoomState(room.p2, room);
    }

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

    private void SendSlotId(NetworkConnection conn, int slotId)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.AssignSlot);
        writer.WriteInt(slotId);
        driver.EndSend(writer);
    }

    private void BroadcastSelectState(ServerRoom room)
    {
        if (room.p1.IsCreated) SendSelectState(room.p1, room);
        if (room.p2.IsCreated) SendSelectState(room.p2, room);
    }

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

    private void BroadcastCountdownState(ServerRoom room, bool isStarted)
    {
        if (room.p1.IsCreated) SendCountdownState(room.p1, isStarted);
        if (room.p2.IsCreated) SendCountdownState(room.p2, isStarted);
    }

    private void SendCountdownState(NetworkConnection conn, bool isStarted)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.CountdownUpdate);
        writer.WriteByte((byte)(isStarted ? 1 : 0));
        driver.EndSend(writer);
    }

    private void BroadcastStartButtonActive(ServerRoom room)
    {
        if (room.p1.IsCreated) SendStartButtonActive(room.p1);
        if (room.p2.IsCreated) SendStartButtonActive(room.p2);
    }

    private void SendStartButtonActive(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.StartButtonActive);
        driver.EndSend(writer);
    }

    private void BroadcastSceneChange(ServerRoom room, int targetSceneInt)
    {
        if (room.p1.IsCreated) SendSceneChange(room.p1, targetSceneInt);
        if (room.p2.IsCreated) SendSceneChange(room.p2, targetSceneInt);
    }

    private void SendSceneChange(NetworkConnection conn, int targetSceneInt)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.SceneChange);
        writer.WriteInt(targetSceneInt);
        driver.EndSend(writer);
    }

    private void BroadcastGameStart(ServerRoom room)
    {
        if (room.p1.IsCreated) SendGameStart(room.p1);
        if (room.p2.IsCreated) SendGameStart(room.p2);
    }

    private void SendGameStart(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.GameStart);
        writer.WriteFixedString64(new FixedString64Bytes("127.0.0.1"));
        driver.EndSend(writer);
    }

    private void BroadcastRoundVerified(ServerRoom room)
    {
        if (room.p1.IsCreated) SendRoundVerified(room.p1);
        if (room.p2.IsCreated) SendRoundVerified(room.p2);
    }

    private void SendRoundVerified(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.RoundVerified);
        driver.EndSend(writer);
    }

    private void BroadcastRematchSync(ServerRoom room, bool p1Ready, bool p2Ready)
    {
        if (room.p1.IsCreated) SendRematchSync(room.p1, p1Ready, p2Ready);
        if (room.p2.IsCreated) SendRematchSync(room.p2, p1Ready, p2Ready);
    }

    private void SendRematchSync(NetworkConnection conn, bool p1Ready, bool p2Ready)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.RematchSyncBroadcast);
        writer.WriteByte((byte)(p1Ready ? 1 : 0));
        writer.WriteByte((byte)(p2Ready ? 1 : 0));
        driver.EndSend(writer);
    }

    private void EvaluateMatchEndVotes(ServerRoom room)
    {
        bool anyReturnToLobby = (room.hasP1Voted && room.p1VoteAction == MatchEndActionType.ReturnToMenu) || 
                                (room.hasP2Voted && room.p2VoteAction == MatchEndActionType.ReturnToMenu);

        bool anyCharacterSelect = (room.hasP1Voted && room.p1VoteAction == MatchEndActionType.ReturnToCharacterSelect) || 
                                  (room.hasP2Voted && room.p2VoteAction == MatchEndActionType.ReturnToCharacterSelect);

        bool bothRematch = room.hasP1Voted && room.p1VoteAction == MatchEndActionType.Rematch && 
                           room.hasP2Voted && room.p2VoteAction == MatchEndActionType.Rematch;

        if (anyReturnToLobby)
        {
            room.isVotingStarted = false;
            ResetRoomForRematch(room);
            room.currentState = RoomStateType.Lobby;
            BroadcastRoomState(room);
            BroadcastSceneChange(room, (int)GameSceneType.OnlineMatchedRoom);
        }
        else if (anyCharacterSelect)
        {
            room.isVotingStarted = false;
            ResetRoomForRematch(room);
            room.currentState = RoomStateType.CharacterSelect;
            BroadcastRoomState(room);
            BroadcastSceneChange(room, (int)GameSceneType.CharacterSelect);
        }
        else if (bothRematch)
        {
            room.isVotingStarted = false;
            ResetRoomForRematch(room);
            room.currentState = RoomStateType.InGame;
            BroadcastRoomState(room);
            BroadcastSceneChange(room, (int)GameSceneType.GamePlay);
        }
    }

    private void ResetRoomForRematch(ServerRoom room)
    {
        room.hasP1Voted = false;
        room.hasP2Voted = false;
        
        room.p1RoundWins = 0;
        room.p2RoundWins = 0;
        
        room.isP1RoundReported = false;
        room.isP2RoundReported = false;
        room.isP1StartRequested = false;
        room.isP2StartRequested = false;
        room.stateModel.isP1Ready = false;
        room.stateModel.isP2Ready = false;

        room.stateModel.isP1CharacterLocked = false;
        room.stateModel.isP2CharacterLocked = false;
    }

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

    private void LogEvent(string msg)
    {
        Debug.Log($"[Server] {msg}");
        serverLogs.Add(msg);
        if (serverLogs.Count > MAX_LOG_LINES) serverLogs.RemoveAt(0);
    }

    private string GenerateRoomCode()
    {
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for(int i = 0; i < 5; i++) code += chars[UnityEngine.Random.Range(0, chars.Length)];
        return code;
    }
}