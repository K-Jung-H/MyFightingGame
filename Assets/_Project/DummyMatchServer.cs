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

public class DummyMatchServer : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    
    private Dictionary<string, ServerRoom> activeRooms = new Dictionary<string, ServerRoom>();
    private Dictionary<NetworkConnection, ServerRoom> connectionToRoom = new Dictionary<NetworkConnection, ServerRoom>();
    
    private bool isMatchActive;
    private List<string> serverLogs = new List<string>();
    private const int MAX_LOG_LINES = 15;

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

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 350, 70), "Lobby Server Status");
        GUI.Label(new Rect(20, 30, 350, 20), $"Active Connections: {connectionToRoom.Count}");
        GUI.Label(new Rect(20, 50, 350, 20), $"Active Rooms: {activeRooms.Count}");

        GUI.Box(new Rect(10, 90, 350, 220), "Active Rooms List");
        float roomY = 110;
        foreach (var kvp in activeRooms)
        {
            ServerRoom room = kvp.Value;
            int playerCount = (room.p1.IsCreated ? 1 : 0) + (room.p2.IsCreated ? 1 : 0);
            string passwordMark = room.HasPassword() ? "[P]" : "";
            
            GUI.Label(new Rect(20, roomY, 330, 20), $"[{room.roomCode}] {room.roomTitle} {passwordMark} ({playerCount}/2)");
            
            roomY += 20;
            if (roomY > 280) break;
        }

        GUI.Box(new Rect(370, 10, 400, 300), "Server Event Logs");
        float logY = 35;
        for (int i = 0; i < serverLogs.Count; i++)
        {
            GUI.Label(new Rect(380, logY, 380, 20), serverLogs[i]);
            logY += 16;
        }
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

    public void SetMatchActive(bool active)
    {
        isMatchActive = active;
        LogEvent($"Match active state changed to: {active}");
    }

    private NetworkDriver CreateConfiguredDriver()
    {
        NetworkSettings settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(
            disconnectTimeoutMS: 5000,
            heartbeatTimeoutMS: 500
        );
        return NetworkDriver.Create(settings);
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

    private void HandleNewConnection(NetworkConnection conn)
    {
        LogEvent("New client connected. Awaiting Room Request.");
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
                    LogEvent($"Room [{room.roomCode}] countdown finished.");
                    BroadcastStartButtonActive(room);
                }
            }
        }
    }

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
        
        if (!connectionToRoom.TryGetValue(conn, out ServerRoom currentRoom)) return;

        if (packetType == 22) 
        {
            float sentTime = stream.ReadFloat();
            SendServerPong(conn, sentTime);
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
            ProcessHandshake(conn, currentRoom);
        }
        else if (packetType == NetworkPacketType.ReportDisconnect)
        {
            ResolveDisconnect(conn, currentRoom);
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
            BroadcastSelectState(targetRoom);
        }
    }

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

    private void HandleDisconnect(NetworkConnection conn)
    {
        if (!connectionToRoom.TryGetValue(conn, out ServerRoom room)) return;

        bool isMatched = false;

        if (conn == room.p1)
        {
            LogEvent($"Room [{room.roomCode}] P1 Disconnected.");
            room.p1 = default;
            room.stateModel.isP1CharacterLocked = false;
            room.isP1Ready = false;
            room.isP1StartRequested = false;
            isMatched = true;
        }
        else if (conn == room.p2)
        {
            LogEvent($"Room [{room.roomCode}] P2 Disconnected.");
            room.p2 = default;
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
            BroadcastSelectState(room);
        }
    }

    private void SendSlotId(NetworkConnection conn, int slotId)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.AssignSlot);
        writer.WriteInt(slotId);
        driver.EndSend(writer);
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

    private void BroadcastSceneChange(ServerRoom room)
    {
        if (room.p1.IsCreated) SendSceneChange(room.p1);
        if (room.p2.IsCreated) SendSceneChange(room.p2);
    }

    private void SendSceneChange(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.SceneChange);
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
        FixedString64Bytes peerIp = new FixedString64Bytes("127.0.0.1");
        writer.WriteFixedString64(peerIp);
        driver.EndSend(writer);
    }

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
        if (serverLogs.Count > MAX_LOG_LINES)
        {
            serverLogs.RemoveAt(0);
        }
    }

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