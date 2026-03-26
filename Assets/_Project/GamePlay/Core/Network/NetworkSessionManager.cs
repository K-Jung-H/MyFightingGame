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
    public const byte P2PPing = 20;
    public const byte P2PPong = 21;
    public const byte ServerPing = 22;
    public const byte ServerPong = 23;
    public const byte ReportDisconnect = 24;
    public const byte MatchAborted = 25;
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

    [SerializeField] private float p2pPingInterval = 0.5f;
    [SerializeField] private float serverPingInterval = 3.0f;
    [SerializeField] private float p2pTimeoutLimit = 7.0f;

    private NetworkDriver serverDriver;
    private NetworkDriver p2pDriver;
    private NetworkConnection serverConnection;
    private NetworkConnection peerConnection;
    
    private bool isInitialized;
    private bool isConnected;
    private bool isHostingPeer;
    private bool isP2PDisconnected;
    private bool requiresSessionReset;
    private const int REDUNDANCY_COUNT = 15;
    private NetworkBufferContext bufferContext;

    private float lastP2PPingSendTime;
    public float lastP2PPacketReceiveTime;
    private float lastServerPingSendTime;
    public float lastServerPacketReceiveTime;
    private int currentPingMs;

    public event Action<int, bool, int, int, bool, int> OnSelectBroadcastReceived;
    public event Action<bool> OnCountdownUpdateReceived;
    public event Action OnStartButtonActiveReceived;
    public event Action OnConnectionEstablished;
    public event Action OnSceneChangeReceived;
    public event Action OnGameStartReceived;
    public event Action<string> OnPeerAddressReceived;
    public event Action<int> OnSlotAssignedReceived;
    public event Action<int> OnPingUpdated;
    public event Action<GameSceneType> OnMatchAbortedReceived;

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

    public bool GetIsConnected() => isConnected;

    public void InitializeNetwork(string serverIp)
    {
        if (isInitialized) return;

        serverDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(serverIp, (ushort)9000);
        serverConnection = serverDriver.Connect(endpoint);
        
        lastServerPacketReceiveTime = 0f;
        lastServerPingSendTime = 0f;
        
        isInitialized = true;
        Debug.Log("[Network] Initializing connection to Dedicated Server...");
    }

    public void StartP2PListen(ushort port = 9001)
    {
        if (isHostingPeer) return;
        
        p2pDriver = NetworkDriver.Create();
        NetworkEndpoint p2pEndpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
        p2pDriver.Bind(p2pEndpoint);
        p2pDriver.Listen();
        isHostingPeer = true;
        
        lastP2PPacketReceiveTime = 0f;
        lastP2PPingSendTime = 0f;
        Debug.Log("[Network] Started listening for P2P connection.");
    }

    public void ConnectToPeer(string peerIp)
    {
        if (isHostingPeer) return;
        
        p2pDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(peerIp, (ushort)9001);
        peerConnection = p2pDriver.Connect(endpoint);
        
        lastP2PPacketReceiveTime = 0f;
        lastP2PPingSendTime = 0f;
        Debug.Log($"[Network] Connecting to P2P peer at {peerIp}...");
    }

    public void UpdateNetwork()
    {
        if (requiresSessionReset)
        {
            ExecuteSessionReset();
            return;
        }

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
                
                float currentTime = Time.realtimeSinceStartup;
                lastP2PPacketReceiveTime = currentTime;
                lastP2PPingSendTime = currentTime;
                Debug.Log("[Network] P2P Peer connected to local host.");
            }
        }

        ProcessEvents();
        ProcessPingTimers();
    }

    public void ResetNetworkSession()
    {
        Debug.Log("[Network] Network Session Reset Requested.");
        requiresSessionReset = true;
    }

    private void ExecuteSessionReset()
    {
        CleanupDrivers();
        isInitialized = false;
        isConnected = false;
        isHostingPeer = false;
        isP2PDisconnected = false;
        requiresSessionReset = false;
        serverConnection = default;
        peerConnection = default;
        currentPingMs = 0;
        bufferContext.Clear();
        Debug.Log("[Network] Network Session completely reset.");
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

    private void CleanupDrivers()
    {
        if (serverDriver.IsCreated)
        {
            if (serverConnection.IsCreated) serverDriver.Disconnect(serverConnection);
            serverDriver.ScheduleUpdate().Complete();
            serverDriver.Dispose();
            serverDriver = default;
        }

        if (p2pDriver.IsCreated)
        {
            if (peerConnection.IsCreated) p2pDriver.Disconnect(peerConnection);
            p2pDriver.ScheduleUpdate().Complete();
            p2pDriver.Dispose();
            p2pDriver = default;
        }
        
        serverConnection = default;
        peerConnection = default;
    }

    private void ProcessEvents()
    {
        if (serverDriver.IsCreated)
        {
            ProcessConnectionEvents(serverDriver, serverConnection, true);
        }

        if (p2pDriver.IsCreated)
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
                float currentTime = Time.realtimeSinceStartup;
                if (isServerSource)
                {
                    Debug.Log("[Network] Server connection established.");
                    lastServerPacketReceiveTime = currentTime;
                    lastServerPingSendTime = currentTime;
                    OnConnectionEstablished?.Invoke();
                }
                else
                {
                    Debug.Log("[Network] P2P connection established.");
                    lastP2PPacketReceiveTime = currentTime;
                    lastP2PPingSendTime = currentTime;
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
                    Debug.LogError("[Network] Lost connection to Dedicated Server.");
                    serverConnection = default;
                    OnMatchAbortedReceived?.Invoke(GameSceneType.Start);
                }
                else
                {
                    Debug.LogWarning("[Network] P2P Peer Explicitly Disconnected.");
                    isConnected = false;
                    peerConnection = default;
                    if (!isP2PDisconnected)
                    {
                        HandleP2PTimeout();
                    }
                }
            }
        }
    }

    private void HandleServerData(byte packetType, ref DataStreamReader stream)
    {
        lastServerPacketReceiveTime = Time.realtimeSinceStartup;

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
        else if (packetType == NetworkPacketType.ServerPing)
        {
            float sentTime = stream.ReadFloat();
            SendPong(true, sentTime);
        }
        else if (packetType == NetworkPacketType.ServerPong)
        {
            float sentTime = stream.ReadFloat();
        }
        else if (packetType == NetworkPacketType.MatchAborted)
        {
            int sceneTypeInt = stream.ReadInt();
            Debug.Log($"[Network] Match Aborted command received from server. Target Scene: {(GameSceneType)sceneTypeInt}");
            OnMatchAbortedReceived?.Invoke((GameSceneType)sceneTypeInt);
        }
    }

    private void HandlePeerData(byte packetType, ref DataStreamReader stream)
    {
        lastP2PPacketReceiveTime = Time.realtimeSinceStartup;

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
        else if (packetType == NetworkPacketType.P2PPing)
        {
            float sentTime = stream.ReadFloat();
            SendPong(false, sentTime);
        }
        else if (packetType == NetworkPacketType.P2PPong)
        {
            float sentTime = stream.ReadFloat();
            float rtt = (Time.realtimeSinceStartup - sentTime) * 1000f;
            currentPingMs = Mathf.RoundToInt(rtt);
            OnPingUpdated?.Invoke(currentPingMs);
        }
    }

    private void ProcessPingTimers()
    {
        float currentTime = Time.realtimeSinceStartup;

        if (lastServerPacketReceiveTime > 0f)
        {
            if (currentTime - lastServerPingSendTime > serverPingInterval)
            {
                if (serverDriver.IsCreated && serverConnection.IsCreated)
                {
                    SendPing(true);
                }
                lastServerPingSendTime = currentTime;
            }
        }

        if (isConnected && !isP2PDisconnected && lastP2PPacketReceiveTime > 0f)
        {
            if (currentTime - lastP2PPacketReceiveTime > p2pTimeoutLimit)
            {
                HandleP2PTimeout();
            }
            else if (currentTime - lastP2PPingSendTime > p2pPingInterval)
            {
                if (p2pDriver.IsCreated && peerConnection.IsCreated)
                {
                    SendPing(false);
                }
                lastP2PPingSendTime = currentTime;
            }
        }
    }

    private void SendPing(bool isServer)
    {
        NetworkDriver driver = isServer ? serverDriver : p2pDriver;
        NetworkConnection conn = isServer ? serverConnection : peerConnection;
        byte packetType = isServer ? NetworkPacketType.ServerPing : NetworkPacketType.P2PPing;

        int sendStatus = driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(packetType);
            writer.WriteFloat(Time.realtimeSinceStartup);
            driver.EndSend(writer);
        }
    }

    private void SendPong(bool isServer, float receivedTime)
    {
        NetworkDriver driver = isServer ? serverDriver : p2pDriver;
        NetworkConnection conn = isServer ? serverConnection : peerConnection;
        byte packetType = isServer ? NetworkPacketType.ServerPong : NetworkPacketType.P2PPong;

        int sendStatus = driver.BeginSend(NetworkPipeline.Null, conn, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(packetType);
            writer.WriteFloat(receivedTime);
            driver.EndSend(writer);
        }
    }

    private void HandleP2PTimeout()
    {
        Debug.LogError("[Network] P2P Connection Lost! Sending Report to Server...");
        isP2PDisconnected = true;
        SendReportDisconnect();
    }

    private void SendReportDisconnect()
    {
        if (!serverConnection.IsCreated) return;
        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.ReportDisconnect);
            serverDriver.EndSend(writer);
        }
    }
}