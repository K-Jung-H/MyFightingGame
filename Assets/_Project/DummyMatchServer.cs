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

    public ServerRoom(NetworkConnection p1Conn, NetworkConnection p2Conn)
    {
        p1 = p1Conn;
        p2 = p2Conn;
        stateModel = new RoomStateModel();
        stateModel.isStageLocked = true;
        isP1Ready = false;
        isP2Ready = false;
    }
}

public class DummyMatchServer : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private List<ServerRoom> activeRooms;
    private NetworkConnection waitingPlayer;

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

    private void HandleNewConnection(NetworkConnection conn)
    {
        if (waitingPlayer == default)
        {
            waitingPlayer = conn;
        }
        else
        {
            ServerRoom newRoom = new ServerRoom(waitingPlayer, conn);
            activeRooms.Add(newRoom);
            waitingPlayer = default;

            BroadcastSelectState(newRoom);
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
                    BroadcastSceneChange(room);
                }
            }
        }
        else if (packetType == NetworkPacketType.Handshake)
        {
            ProcessHandshake(conn);
        }
    }

    private void ProcessHandshake(NetworkConnection conn)
    {
        ServerRoom room = FindRoomByConnection(conn);
        if (room != null)
        {
            if (conn == room.p1) room.isP1Ready = true;
            else if (conn == room.p2) room.isP2Ready = true;

            if (room.isP1Ready && room.isP2Ready)
            {
                BroadcastGameStart(room);
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
            if (room.p1 == conn || room.p2 == conn) return room;
        }
        return null;
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
        writer.WriteInt(room.stateModel.p2CharacterIndex);
        writer.WriteByte((byte)(room.stateModel.isP2CharacterLocked ? 1 : 0));
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
}