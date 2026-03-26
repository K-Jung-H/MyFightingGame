using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;

public class ServerRoom
{
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

    public ServerRoom()
    {
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
}

public class DummyMatchServer : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private ServerRoom currentRoom;
    private bool isMatchActive;
    
    private bool isResetPending;
    private float resetDelayTimer;
    private List<string> serverLogs = new List<string>();
    private const int MAX_LOG_LINES = 15;

    private void Update()
    {
        if (!driver.IsCreated) return;

        driver.ScheduleUpdate().Complete();
        CleanupConnections();

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

        if (isResetPending)
        {
            resetDelayTimer -= Time.deltaTime;
            if (resetDelayTimer <= 0f)
            {
                ExecuteServerReset();
            }
        }
    }

    private void OnGUI()
    {
        if (currentRoom == null) return;

        GUI.Box(new Rect(10, 10, 350, 250), "Server Room Status");
        
        GUI.Label(new Rect(20, 40, 330, 20), $"P1 Connected: {currentRoom.p1.IsCreated} | Ready: {currentRoom.isP1Ready}");
        GUI.Label(new Rect(20, 60, 330, 20), $"P2 Connected: {currentRoom.p2.IsCreated} | Ready: {currentRoom.isP2Ready}");
        
        GUI.Label(new Rect(20, 90, 330, 20), $"P1 Side: {(currentRoom.stateModel.p1PreferredSide == 0 ? "Left" : "Right")}");
        GUI.Label(new Rect(20, 110, 330, 20), $"P2 Side: {(currentRoom.stateModel.p2PreferredSide == 0 ? "Left" : "Right")}");
        
        GUI.Label(new Rect(20, 140, 330, 20), $"P1 Char Index: {currentRoom.stateModel.p1CharacterIndex} (Locked: {currentRoom.stateModel.isP1CharacterLocked})");
        GUI.Label(new Rect(20, 160, 330, 20), $"P2 Char Index: {currentRoom.stateModel.p2CharacterIndex} (Locked: {currentRoom.stateModel.isP2CharacterLocked})");
        
        GUI.Label(new Rect(20, 190, 330, 20), $"Countdown: {(currentRoom.isCountdownStarted ? currentRoom.countdownTimer.ToString("F1") : (currentRoom.isCountdownFinished ? "Finished" : "Wait"))}");
        GUI.Label(new Rect(20, 220, 330, 20), $"P1 Start: {currentRoom.isP1StartRequested} / P2 Start: {currentRoom.isP2StartRequested}");

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

    private void LogEvent(string msg)
    {
        Debug.Log($"[Server] {msg}");
        serverLogs.Add(msg);
        if (serverLogs.Count > MAX_LOG_LINES)
        {
            serverLogs.RemoveAt(0);
        }
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
        currentRoom = new ServerRoom();
        isMatchActive = false;
        
        LogEvent("Dedicated Server started on port 9000.");
    }

    public void SetMatchActive(bool active)
    {
        isMatchActive = active;
        LogEvent($"Match active state changed to: {active}");
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

    private void HandleNewConnection(NetworkConnection conn)
    {
        if (!currentRoom.p1.IsCreated)
        {
            currentRoom.p1 = conn;
            LogEvent("P1 connected. Assigned to Slot 0.");
            SendSlotId(conn, 0);
        }
        else if (!currentRoom.p2.IsCreated)
        {
            currentRoom.p2 = conn;
            LogEvent("P2 connected. Assigned to Slot 1.");
            SendSlotId(conn, 1);
        }
        else
        {
            LogEvent("Room is full. Rejecting new connection.");
            driver.Disconnect(conn);
            return;
        }

        BroadcastSelectState(currentRoom);
    }

    private void UpdateRoomTimers()
    {
        if (currentRoom != null && currentRoom.isCountdownStarted)
        {
            currentRoom.countdownTimer -= Time.deltaTime;
            
            if (currentRoom.countdownTimer <= 0f)
            {
                currentRoom.isCountdownStarted = false;
                currentRoom.isCountdownFinished = true;
                LogEvent("Countdown finished. Broadcast StartButtonActive.");
                BroadcastStartButtonActive(currentRoom);
            }
        }
    }

    private void ProcessData(NetworkConnection conn, ref DataStreamReader stream)
    {
        byte packetType = stream.ReadByte();
        
        if (packetType == 22) 
        {
            float sentTime = stream.ReadFloat();
            LogEvent($"Received ServerPing from {(conn == currentRoom.p1 ? "P1" : "P2")}");
            SendServerPong(conn, sentTime);
        }
        else if (packetType == NetworkPacketType.SelectUpdate)
        {
            int playerIdx = stream.ReadInt();
            int charIdx = stream.ReadInt();
            byte isLockedByte = stream.ReadByte();
            bool isLocked = isLockedByte == 1;

            if (currentRoom != null)
            {
                if (conn == currentRoom.p1) 
                {
                    currentRoom.stateModel.p1CharacterIndex = charIdx;
                    currentRoom.stateModel.isP1CharacterLocked = isLocked;
                    LogEvent($"P1 Character Update: Idx {charIdx}, Locked: {isLocked}");
                }
                else if (conn == currentRoom.p2) 
                {
                    currentRoom.stateModel.p2CharacterIndex = charIdx;
                    currentRoom.stateModel.isP2CharacterLocked = isLocked;
                    LogEvent($"P2 Character Update: Idx {charIdx}, Locked: {isLocked}");
                }

                BroadcastSelectState(currentRoom);

                if (currentRoom.stateModel.IsAllReadyToStart())
                {
                    if (!currentRoom.isCountdownStarted && !currentRoom.isCountdownFinished)
                    {
                        currentRoom.isCountdownStarted = true;
                        currentRoom.countdownTimer = 3f;
                        LogEvent("Both players locked. Starting countdown.");
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
        }
        else if (packetType == NetworkPacketType.SideUpdate)
        {
            int side = stream.ReadInt();
            if (currentRoom != null)
            {
                if (conn == currentRoom.p1) currentRoom.stateModel.p1PreferredSide = side;
                else if (conn == currentRoom.p2) currentRoom.stateModel.p2PreferredSide = side;
                
                LogEvent($"{(conn == currentRoom.p1 ? "P1" : "P2")} Side Update: {(side == 0 ? "Left" : "Right")}");
                BroadcastSelectState(currentRoom);
            }
        }
        else if (packetType == NetworkPacketType.StartRequest)
        {
            if (currentRoom != null && currentRoom.isCountdownFinished)
            {
                if (conn == currentRoom.p1) currentRoom.isP1StartRequested = true;
                else if (conn == currentRoom.p2) currentRoom.isP2StartRequested = true;

                LogEvent($"Start requested by {(conn == currentRoom.p1 ? "P1" : "P2")}");

                if (currentRoom.isP1StartRequested && currentRoom.isP2StartRequested)
                {
                    LogEvent("Both players requested start. Initiating SceneChange.");
                    BroadcastSceneChange(currentRoom);
                }
            }
        }
        else if (packetType == NetworkPacketType.Handshake)
        {
            ProcessHandshake(conn);
        }
        else if (packetType == NetworkPacketType.ReportDisconnect)
        {
            LogEvent($"Received ReportDisconnect from {(conn == currentRoom.p1 ? "P1" : "P2")}");
            ResolveDisconnect(conn);
        }
    }

    private void ProcessHandshake(NetworkConnection conn)
    {
        if (currentRoom != null)
        {
            if (conn == currentRoom.p1) currentRoom.isP1Ready = true;
            else if (conn == currentRoom.p2) currentRoom.isP2Ready = true;

            LogEvent($"Handshake received from {(conn == currentRoom.p1 ? "P1" : "P2")}");

            if (currentRoom.isP1Ready && currentRoom.isP2Ready)
            {
                LogEvent("Handshake complete. Broadcasting GameStart.");
                SetMatchActive(true);
                BroadcastGameStart(currentRoom);
            }
        }
    }

    private void HandleDisconnect(NetworkConnection conn)
    {
        if (currentRoom == null) return;

        bool isMatched = false;

        if (conn == currentRoom.p1)
        {
            LogEvent("P1 Disconnected.");
            currentRoom.p1 = default;
            currentRoom.stateModel.isP1CharacterLocked = false;
            currentRoom.isP1Ready = false;
            currentRoom.isP1StartRequested = false;
            isMatched = true;
        }
        else if (conn == currentRoom.p2)
        {
            LogEvent("P2 Disconnected.");
            currentRoom.p2 = default;
            currentRoom.stateModel.isP2CharacterLocked = false;
            currentRoom.isP2Ready = false;
            currentRoom.isP2StartRequested = false;
            isMatched = true;
        }

        if (isMatched)
        {
            if (isMatchActive)
            {
                NetworkConnection survivor = (conn == currentRoom.p1) ? currentRoom.p2 : currentRoom.p1;
                LogEvent("Match active. Broadcasting MatchAborted to survivor.");
                BroadcastMatchAborted(survivor, (int)GameSceneType.OnlineMatchedRoom);
                ScheduleServerReset();
                return;
            }

            currentRoom.isCountdownStarted = false;
            currentRoom.isCountdownFinished = false;
            currentRoom.countdownTimer = 3f;
            isMatchActive = false;
            
            LogEvent("Resetting lobby state due to disconnection.");
            BroadcastCountdownState(currentRoom, false);
            BroadcastSelectState(currentRoom);
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
            LogEvent($"Sending ServerPong to {(conn == currentRoom.p1 ? "P1" : "P2")}");
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

    private void ResolveDisconnect(NetworkConnection reporterConn)
    {
        isMatchActive = false;
        
        bool isP1Reporter = (reporterConn == currentRoom.p1);
        NetworkConnection suspectConn = isP1Reporter ? currentRoom.p2 : currentRoom.p1;
        
        int targetSceneInt = (int)GameSceneType.OnlineMatchedRoom;

        if (reporterConn.IsCreated) BroadcastMatchAborted(reporterConn, targetSceneInt);
        if (suspectConn.IsCreated) BroadcastMatchAborted(suspectConn, targetSceneInt);

        ScheduleServerReset();
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

    private void ScheduleServerReset()
    {
        LogEvent("Scheduling server reset in 0.5 seconds.");
        isResetPending = true;
        resetDelayTimer = 0.5f;
    }

    private void ExecuteServerReset()
    {
        LogEvent("Executing full server reset.");
        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated)
            {
                driver.Disconnect(connections[i]);
            }
        }
        
        connections.Clear();
        currentRoom = new ServerRoom();
        isMatchActive = false;
        isResetPending = false;
    }
}