using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;
using System;

public static class NetworkPacketType
{
    public const byte Input = 0;
    public const byte Hash = 1;
    public const byte SelectUpdate = 10;
    public const byte SelectBroadcast = 11;
    public const byte SceneChange = 12;
    public const byte Handshake = 13;
    public const byte GameStart = 14;

    public const byte CountdownUpdate = 15;
    public const byte StartButtonActive = 16;
    public const byte StartRequest = 17;
    public const byte AssignSlot = 18;
    public const byte SideUpdate = 19;
}

public class NetworkBufferContext
{
    public Dictionary<int, ushort> remoteInputBuffer;
    public Dictionary<int, ushort> localInputHistory;
    public Dictionary<int, ulong> remoteHashBuffer;

    public NetworkBufferContext()
    {
        remoteInputBuffer = new Dictionary<int, ushort>();
        localInputHistory = new Dictionary<int, ushort>();
        remoteHashBuffer = new Dictionary<int, ulong>();
    }

    public void Clear()
    {
        remoteInputBuffer.Clear();
        localInputHistory.Clear();
        remoteHashBuffer.Clear();
    }
}

public class NetworkSessionManager : MonoBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    private NetworkDriver serverDriver;
    private NetworkDriver p2pDriver;
    private NetworkConnection serverConnection;
    private NetworkConnection peerConnection;
    
    private bool isInitialized;
    private bool isConnected;
    private bool isHostingPeer;
    private const int REDUNDANCY_COUNT = 15;
    private NetworkBufferContext bufferContext;

    public event Action<int, bool, int, int, bool, int> OnSelectBroadcastReceived;
    public event Action<bool> OnCountdownUpdateReceived;
    public event Action OnStartButtonActiveReceived;

    public event Action OnConnectionEstablished;
    public event Action OnSceneChangeReceived;
    public event Action OnGameStartReceived;
    public event Action<string> OnPeerAddressReceived;
    public event Action<int> OnSlotAssignedReceived;

    public bool GetIsConnected() => isConnected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.runInBackground = true;
        Screen.SetResolution(1024, 768, FullScreenMode.Windowed);

        bufferContext = new NetworkBufferContext();
    }

    private void OnApplicationQuit()
    {
        CleanupDrivers();
    }

    private void OnDestroy()
    {
        CleanupDrivers();
    }

    private void CleanupDrivers()
    {
        if (serverDriver.IsCreated)
        {
            if (serverConnection.IsCreated) serverDriver.Disconnect(serverConnection);
            serverDriver.ScheduleUpdate().Complete();
            serverDriver.Dispose();
        }

        if (p2pDriver.IsCreated)
        {
            if (peerConnection.IsCreated) p2pDriver.Disconnect(peerConnection);
            p2pDriver.ScheduleUpdate().Complete();
            p2pDriver.Dispose();
        }
    }

    public void InitializeNetwork(string serverIp)
    {
        if (isInitialized) return;

        serverDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(serverIp, (ushort)9000);
        serverConnection = serverDriver.Connect(endpoint);
        
        isInitialized = true;
    }

    public void StartP2PListen(ushort port = 9001)
    {
        if (isHostingPeer) return;
        
        p2pDriver = NetworkDriver.Create();
        NetworkEndpoint p2pEndpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
        p2pDriver.Bind(p2pEndpoint);
        p2pDriver.Listen();
        isHostingPeer = true;
    }

    public void ConnectToPeer(string peerIp)
    {
        if (isHostingPeer) return;
        
        p2pDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(peerIp, (ushort)9001);
        peerConnection = p2pDriver.Connect(endpoint);
    }

    public void UpdateNetwork()
    {
        if (!isInitialized) return;

        if (serverDriver.IsCreated)
        {
            serverDriver.ScheduleUpdate().Complete();
        }

        if (p2pDriver.IsCreated)
        {
            p2pDriver.ScheduleUpdate().Complete();
        }

        if (isHostingPeer && p2pDriver.IsCreated && !peerConnection.IsCreated)
        {
            NetworkConnection c;
            while ((c = p2pDriver.Accept()) != default)
            {
                peerConnection = c;
                isConnected = true;
            }
        }

        ProcessEvents();
    }

    private void ProcessEvents()
    {
        if (serverDriver.IsCreated && serverConnection.IsCreated)
        {
            ProcessConnectionEvents(serverDriver, serverConnection, true);
        }

        if (p2pDriver.IsCreated && peerConnection.IsCreated)
        {
            ProcessConnectionEvents(p2pDriver, peerConnection, false);
        }
    }

    private void ProcessConnectionEvents(NetworkDriver driver, NetworkConnection conn, bool isServerSource)
    {
        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = driver.PopEventForConnection(conn, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                if (isServerSource)
                {
                    OnConnectionEstablished?.Invoke();
                }
                else
                {
                    isConnected = true; 
                }
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                byte packetType = stream.ReadByte();
                if (isServerSource) HandleServerData(packetType, ref stream);
                else HandlePeerData(packetType, ref stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                if (isServerSource)
                {
                    serverConnection = default;
                }
                else
                {
                    isConnected = false;
                    peerConnection = default;
                }
            }
        }
    }

    private void HandleServerData(byte packetType, ref DataStreamReader stream)
    {
        if (packetType == NetworkPacketType.SelectBroadcast)
        {
            int p1Idx = stream.ReadInt();
            bool p1Lock = stream.ReadByte() == 1;
            int p1Side = stream.ReadInt();
            int p2Idx = stream.ReadInt();
            bool p2Lock = stream.ReadByte() == 1;
            int p2Side = stream.ReadInt();
            OnSelectBroadcastReceived?.Invoke(p1Idx, p1Lock, p1Side, p2Idx, p2Lock, p2Side);
        }
        else if (packetType == NetworkPacketType.SceneChange)
        {
            OnSceneChangeReceived?.Invoke();
        }
        else if (packetType == NetworkPacketType.GameStart)
        {
            FixedString64Bytes peerIp = stream.ReadFixedString64();
            OnPeerAddressReceived?.Invoke(peerIp.ToString());
            OnGameStartReceived?.Invoke();
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
        else if (packetType == NetworkPacketType.AssignSlot)
        {
            int assignedSlot = stream.ReadInt();
            OnSlotAssignedReceived?.Invoke(assignedSlot);
        }
    }

    private void HandlePeerData(byte packetType, ref DataStreamReader stream)
    {
        if (packetType == NetworkPacketType.Input)
        {
            int startTick = stream.ReadInt();
            byte count = stream.ReadByte();
            for (int j = 0; j < count; j++)
            {
                ushort input = stream.ReadUShort();
                int tick = startTick + j;
                if (!bufferContext.remoteInputBuffer.ContainsKey(tick))
                {
                    bufferContext.remoteInputBuffer[tick] = input;
                }
            }
        }
        else if (packetType == NetworkPacketType.Hash)
        {
            int tick = stream.ReadInt();
            ulong hash = stream.ReadULong();
            bufferContext.remoteHashBuffer[tick] = hash;
        }
    }

    public void SendSelectUpdate(int playerIndex, int characterIndex, bool isLocked)
    {
        if (!serverConnection.IsCreated) return;

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

    public void SendSideUpdate(int side)
    {
        if (!serverConnection.IsCreated) return;
        
        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.SideUpdate);
            writer.WriteInt(side);
            serverDriver.EndSend(writer);
        }
    }

    public void SendHandshake()
    {
        if (!serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.Handshake);
            serverDriver.EndSend(writer);
        }
    }

    public void SendStartRequest()
    {
        if (!serverConnection.IsCreated) return;
        
        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.StartRequest);
            serverDriver.EndSend(writer);
        }
    }

    public void SendLocalInput(int currentTick, ushort localInput)
    {
        if (!peerConnection.IsCreated) return;

        bufferContext.localInputHistory[currentTick] = localInput;

        int startTick = Mathf.Max(0, currentTick - REDUNDANCY_COUNT + 1);
        byte count = (byte)(currentTick - startTick + 1);

        int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.Input);
            writer.WriteInt(startTick);
            writer.WriteByte(count);

            for (int t = startTick; t <= currentTick; t++)
            {
                ushort savedInput = bufferContext.localInputHistory.TryGetValue(t, out var val) ? val : (ushort)0;
                writer.WriteUShort(savedInput);
            }

            p2pDriver.EndSend(writer);
        }

        bufferContext.localInputHistory.Remove(currentTick - REDUNDANCY_COUNT);
    }

    public void SendSyncHash(int tick, ulong hash)
    {
        if (!peerConnection.IsCreated) return;

        int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.Hash);
            writer.WriteInt(tick);
            writer.WriteULong(hash);
            p2pDriver.EndSend(writer);
        }
    }

    public bool TryGetRemoteInput(int targetTick, out ushort remoteInput)
    {
        return bufferContext.remoteInputBuffer.TryGetValue(targetTick, out remoteInput);
    }

    public bool TryGetRemoteHash(int targetTick, out ulong hash)
    {
        return bufferContext.remoteHashBuffer.TryGetValue(targetTick, out hash);
    }

    public void ClearBuffer()
    {
        bufferContext.Clear();
    }
}