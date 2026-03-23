using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;

public class ServerRoom
{
    public NetworkConnection P1;
    public NetworkConnection P2;
    public int P1CharIdx;
    public int P2CharIdx;
    
    public MatchedRoomManager RoomManager { get; private set; }

    public ServerRoom(NetworkConnection p1, NetworkConnection p2)
    {
        P1 = p1;
        P2 = p2;
        RoomManager = new MatchedRoomManager();
        RoomManager.Initialize();
    }
}


public class DummyMatchServer : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private List<ServerRoom> activeRooms;
    private NetworkConnection waitingPlayer;

    public void StartServer()
    {
        driver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(9000);
        
        if (driver.Bind(endpoint) != 0) return;
        
        driver.Listen();
        connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        activeRooms = new List<ServerRoom>();
        waitingPlayer = default;
    }

    private void OnDestroy()
    {
        if (driver.IsCreated)
        {
            driver.Dispose();
            connections.Dispose();
        }
    }

    private void Update()
    {
        if (!driver.IsCreated) return;

        driver.ScheduleUpdate().Complete();

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
    }

    private void HandleNewConnection(NetworkConnection conn)
    {
        if (waitingPlayer == default)
        {
            waitingPlayer = conn;
        }
        else
        {
            ServerRoom newRoom = new ServerRoom(waitingPlayer, conn);
            newRoom.RoomManager.OnMatchStartServerCommand += () => BroadcastMatchStart(newRoom);
            
            activeRooms.Add(newRoom);
            waitingPlayer = default;
        }
    }

    private void ProcessData(NetworkConnection conn, ref DataStreamReader stream)
    {
        byte packetType = stream.ReadByte();
        if (packetType == NetworkPacketType.SelectUpdate)
        {
            int playerIdx = stream.ReadInt();
            int charIdx = stream.ReadInt();
            byte isLockedByte = stream.ReadByte();
            bool isLocked = isLockedByte == 1;

            ServerRoom room = FindRoomByConnection(conn);
            if (room != null)
            {
                if (conn == room.P1) room.P1CharIdx = charIdx;
                else if (conn == room.P2) room.P2CharIdx = charIdx;

                room.RoomManager.UpdatePlayerLockState(conn == room.P1 ? 1 : 2, isLocked);
                BroadcastSelectState(room);
            }
        }
    }

    private void HandleDisconnect(NetworkConnection conn)
    {
        if (waitingPlayer == conn)
        {
            waitingPlayer = default;
            return;
        }
        
        ServerRoom room = FindRoomByConnection(conn);
        if (room != null)
        {
            activeRooms.Remove(room);
        }
    }

    private ServerRoom FindRoomByConnection(NetworkConnection conn)
    {
        foreach (var room in activeRooms)
        {
            if (room.P1 == conn || room.P2 == conn) return room;
        }
        return null;
    }

    private void BroadcastSelectState(ServerRoom room)
    {
        if (room.P1.IsCreated) SendSelectState(room.P1, room);
        if (room.P2.IsCreated) SendSelectState(room.P2, room);
    }

    private void SendSelectState(NetworkConnection conn, ServerRoom room)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.SelectBroadcast);
        writer.WriteInt(room.P1CharIdx);
        writer.WriteByte((byte)(room.RoomManager.P1Locked ? 1 : 0));
        writer.WriteInt(room.P2CharIdx);
        writer.WriteByte((byte)(room.RoomManager.P2Locked ? 1 : 0));
        driver.EndSend(writer);
    }

    private void BroadcastMatchStart(ServerRoom room)
    {
        if (room.P1.IsCreated) SendMatchStart(room.P1);
        if (room.P2.IsCreated) SendMatchStart(room.P2);
    }

    private void SendMatchStart(NetworkConnection conn)
    {
        driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        writer.WriteByte(NetworkPacketType.MatchStart);
        FixedString64Bytes peerIp = new FixedString64Bytes("127.0.0.1");
        writer.WriteFixedString64(peerIp);
        driver.EndSend(writer);
    }
}