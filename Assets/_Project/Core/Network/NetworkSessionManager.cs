using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;
using System.Collections;

public static class NetworkPacketType
{
    public const byte Input = 0;
    public const byte Hash = 1;
    public const byte SelectUpdate = 10;
    public const byte SelectBroadcast = 11;
    public const byte MatchStart = 12;
    public const byte GameEvent = 13;
}

[System.Serializable]
public class SelectPhaseContext
{
    public int p1CharacterIndex;
    public int p2CharacterIndex;
    public bool isP1Locked;
    public bool isP2Locked;
    public bool isMatchStarting;

    public void Reset()
    {
        p1CharacterIndex = 0;
        p2CharacterIndex = 0;
        isP1Locked = false;
        isP2Locked = false;
        isMatchStarting = false;
    }
}

public class InGamePhaseContext
{
    public Dictionary<int, ushort> remoteInputBuffer;
    public Dictionary<int, ushort> localInputHistory;
    public Dictionary<int, ulong> remoteHashBuffer;

    public InGamePhaseContext()
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

    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private bool isServer;
    private bool isInitialized;
    private bool isConnected;
    private const int REDUNDANCY_COUNT = 15;

    public SelectPhaseContext selectContext = new SelectPhaseContext();
    public InGamePhaseContext inGameContext = new InGamePhaseContext();

    public event System.Action<int, bool, int, bool> OnSelectBroadcastReceived;
    public event System.Action OnConnectionEstablished;
    public event System.Action OnMatchStartCommand;

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
    }

    public void InitializeNetwork(bool startAsServer)
    {
        bool hasInitialized = isInitialized;
        if (hasInitialized) return;

        driver = NetworkDriver.Create();
        connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        isServer = startAsServer;
        isInitialized = true;

        if (isServer)
        {
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(9000);
            driver.Bind(endpoint);
            driver.Listen();
        }
        else
        {
            NetworkEndpoint endpoint = NetworkEndpoint.Parse("127.0.0.1", 9000);
            connections.Add(driver.Connect(endpoint));
        }
    }

    public void UpdateNetwork()
    {
        bool hasInitialized = isInitialized;
        if (!hasInitialized) return;

        driver.ScheduleUpdate().Complete();

        CleanUpConnections();
        AcceptNewConnections();
        ProcessEvents();
    }

    private void OnApplicationQuit()
    {
        bool canCleanUp = isInitialized && driver.IsCreated;
        if (!canCleanUp) return;

        for (int i = 0; i < connections.Length; i++)
        {
            bool isConnectionCreated = connections[i].IsCreated;
            if (isConnectionCreated)
            {
                driver.Disconnect(connections[i]);
            }
        }

        driver.ScheduleUpdate().Complete();
    }

    private void OnDestroy()
    {
        bool isDriverCreated = driver.IsCreated;
        if (isDriverCreated)
        {
            driver.Dispose();
            bool isConnectionsCreated = connections.IsCreated;
            if (isConnectionsCreated) connections.Dispose();
        }
    }

    public void SendSelectUpdate(int playerIndex, int characterIndex, bool isLocked)
    {
        if (isServer)
        {
            bool isP1 = playerIndex == 1;
            if (isP1)
            {
                selectContext.p1CharacterIndex = characterIndex;
                selectContext.isP1Locked = isLocked;
            }
            else
            {
                selectContext.p2CharacterIndex = characterIndex;
                selectContext.isP2Locked = isLocked;
            }

            BroadcastSelectState(
                selectContext.p1CharacterIndex,
                selectContext.isP1Locked,
                selectContext.p2CharacterIndex,
                selectContext.isP2Locked
            );

            CheckMatchReadyState();
            return;
        }

        bool hasNoConnections = connections.Length == 0;
        if (hasNoConnections) return;

        for (int i = 0; i < connections.Length; i++)
        {
            bool isConnectionCreated = connections[i].IsCreated;
            if (isConnectionCreated)
            {
                int sendStatus = driver.BeginSend(NetworkPipeline.Null, connections[i], out DataStreamWriter writer);
                bool isSendSuccess = sendStatus == 0;

                if (isSendSuccess)
                {
                    writer.WriteByte(NetworkPacketType.SelectUpdate);
                    writer.WriteInt(playerIndex);
                    writer.WriteInt(characterIndex);
                    writer.WriteByte((byte)(isLocked ? 1 : 0));
                    driver.EndSend(writer);
                }
            }
        }
    }

    private void BroadcastSelectState(int p1Index, bool p1Lock, int p2Index, bool p2Lock)
    {
        for (int i = 0; i < connections.Length; i++)
        {
            bool isConnectionCreated = connections[i].IsCreated;
            if (isConnectionCreated)
            {
                int sendStatus = driver.BeginSend(NetworkPipeline.Null, connections[i], out DataStreamWriter writer);
                bool isSendSuccess = sendStatus == 0;

                if (isSendSuccess)
                {
                    writer.WriteByte(NetworkPacketType.SelectBroadcast);
                    writer.WriteInt(p1Index);
                    writer.WriteByte((byte)(p1Lock ? 1 : 0));
                    writer.WriteInt(p2Index);
                    writer.WriteByte((byte)(p2Lock ? 1 : 0));
                    driver.EndSend(writer);
                }
            }
        }
    }

    private void CheckMatchReadyState()
    {
        bool areBothReady = selectContext.isP1Locked && selectContext.isP2Locked;
        bool canStart = areBothReady && !selectContext.isMatchStarting;

        if (canStart)
        {
            selectContext.isMatchStarting = true;
            StartCoroutine(MatchStartRoutine());
        }
    }

    private IEnumerator MatchStartRoutine()
    {
        yield return new WaitForSeconds(1.0f);

        for (int i = 0; i < connections.Length; i++)
        {
            bool isConnectionCreated = connections[i].IsCreated;
            if (isConnectionCreated)
            {
                int sendStatus = driver.BeginSend(NetworkPipeline.Null, connections[i], out DataStreamWriter writer);
                bool isSendSuccess = sendStatus == 0;

                if (isSendSuccess)
                {
                    writer.WriteByte(NetworkPacketType.MatchStart);
                    driver.EndSend(writer);
                }
            }
        }

        OnMatchStartCommand?.Invoke();
    }

    private void CleanUpConnections()
    {
        for (int i = 0; i < connections.Length; i++)
        {
            bool isConnectionLost = !connections[i].IsCreated;
            if (isConnectionLost)
            {
                connections.RemoveAtSwapBack(i);
                i--;
            }
        }
    }

    private void AcceptNewConnections()
    {
        NetworkConnection c;
        while ((c = driver.Accept()) != default)
        {
            connections.Add(c);
            bool isFirstConnection = !isConnected;
            if (isFirstConnection)
            {
                isConnected = true;
                OnConnectionEstablished?.Invoke();
            }
        }
    }

    private void ProcessEvents()
    {
        for (int i = 0; i < connections.Length; i++)
        {
            DataStreamReader stream;
            NetworkEvent.Type cmd;
            while ((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                bool isConnectEvent = cmd == NetworkEvent.Type.Connect;
                bool isDataEvent = cmd == NetworkEvent.Type.Data;
                bool isDisconnectEvent = cmd == NetworkEvent.Type.Disconnect;

                if (isConnectEvent)
                {
                    bool isFirstConnection = !isConnected;
                    if (isFirstConnection)
                    {
                        isConnected = true;
                        OnConnectionEstablished?.Invoke();
                    }
                }
                else if (isDataEvent)
                {
                    byte packetType = stream.ReadByte();
                    HandleIncomingData(packetType, stream);
                }
                else if (isDisconnectEvent)
                {
                    isConnected = false;
                    connections[i] = default;
                }
            }
        }
    }

    private void HandleIncomingData(byte packetType, DataStreamReader stream)
    {
        bool isSelectPhasePacket = packetType == NetworkPacketType.SelectUpdate ||
                                   packetType == NetworkPacketType.SelectBroadcast ||
                                   packetType == NetworkPacketType.MatchStart;

        if (isSelectPhasePacket)
        {
            HandleSelectPhaseData(packetType, ref stream);
        }
        else
        {
            HandleInGamePhaseData(packetType, ref stream);
        }
    }

    private void HandleSelectPhaseData(byte packetType, ref DataStreamReader stream)
    {
        bool isSelectUpdate = packetType == NetworkPacketType.SelectUpdate;
        bool isSelectBroadcast = packetType == NetworkPacketType.SelectBroadcast;
        bool isMatchStart = packetType == NetworkPacketType.MatchStart;

        if (isSelectUpdate && isServer)
        {
            int playerIndex = stream.ReadInt();
            int characterIndex = stream.ReadInt();
            bool isLocked = stream.ReadByte() == 1;

            bool isP1 = playerIndex == 1;
            if (isP1)
            {
                selectContext.p1CharacterIndex = characterIndex;
                selectContext.isP1Locked = isLocked;
            }
            else
            {
                selectContext.p2CharacterIndex = characterIndex;
                selectContext.isP2Locked = isLocked;
            }

            OnSelectBroadcastReceived?.Invoke(
                selectContext.p1CharacterIndex,
                selectContext.isP1Locked,
                selectContext.p2CharacterIndex,
                selectContext.isP2Locked
            );

            BroadcastSelectState(
                selectContext.p1CharacterIndex,
                selectContext.isP1Locked,
                selectContext.p2CharacterIndex,
                selectContext.isP2Locked
            );

            CheckMatchReadyState();
        }
        else if (isSelectBroadcast && !isServer)
        {
            int p1Index = stream.ReadInt();
            bool p1Lock = stream.ReadByte() == 1;
            int p2Index = stream.ReadInt();
            bool p2Lock = stream.ReadByte() == 1;

            OnSelectBroadcastReceived?.Invoke(p1Index, p1Lock, p2Index, p2Lock);
        }
        else if (isMatchStart)
        {
            OnMatchStartCommand?.Invoke();
        }
    }

    private void HandleInGamePhaseData(byte packetType, ref DataStreamReader stream)
    {
        bool isInputPacket = packetType == NetworkPacketType.Input;
        bool isHashPacket = packetType == NetworkPacketType.Hash;

        if (isInputPacket)
        {
            int startTick = stream.ReadInt();
            byte count = stream.ReadByte();

            for (int j = 0; j < count; j++)
            {
                ushort receivedFlags = stream.ReadUShort();
                int tick = startTick + j;

                bool hasInput = inGameContext.remoteInputBuffer.ContainsKey(tick);
                if (!hasInput)
                {
                    inGameContext.remoteInputBuffer[tick] = receivedFlags;
                }
            }
        }
        else if (isHashPacket)
        {
            int tick = stream.ReadInt();
            ulong hash = stream.ReadULong();
            inGameContext.remoteHashBuffer[tick] = hash;
        }
    }

    public bool GetIsConnected() => isConnected;
    public bool GetIsInitialized() => isInitialized;
    public bool GetIsServer() => isServer;

    public void BroadcastLocalInput(int currentTick, InputFlags localInput)
    {
        if (connections.Length == 0) return;

        inGameContext.localInputHistory[currentTick] = (ushort)localInput;

        int startTick = Mathf.Max(0, currentTick - REDUNDANCY_COUNT + 1);
        byte count = (byte)(currentTick - startTick + 1);

        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated)
            {
                driver.BeginSend(NetworkPipeline.Null, connections[i], out DataStreamWriter writer);
                writer.WriteByte(NetworkPacketType.Input);
                writer.WriteInt(startTick);
                writer.WriteByte(count);

                for (int t = startTick; t <= currentTick; t++)
                {
                    if (inGameContext.localInputHistory.TryGetValue(t, out ushort savedInput))
                    {
                        writer.WriteUShort(savedInput);
                    }
                    else
                    {
                        writer.WriteUShort(0);
                    }
                }

                driver.EndSend(writer);
            }
        }

        int obsoleteTick = currentTick - REDUNDANCY_COUNT;
        if (inGameContext.localInputHistory.ContainsKey(obsoleteTick))
        {
            inGameContext.localInputHistory.Remove(obsoleteTick);
        }
    }

    public void BroadcastSyncHash(int tick, ulong hash)
    {
        if (connections.Length == 0) return;

        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated)
            {
                driver.BeginSend(NetworkPipeline.Null, connections[i], out DataStreamWriter writer);
                writer.WriteByte(NetworkPacketType.Hash);
                writer.WriteInt(tick);
                writer.WriteULong(hash);
                driver.EndSend(writer);
            }
        }
    }

    public bool TryGetRemoteInput(int targetTick, out InputFlags remoteInput)
    {
        bool hasInput = inGameContext.remoteInputBuffer.TryGetValue(targetTick, out ushort rawInput);
        remoteInput = (InputFlags)rawInput;
        return hasInput;
    }

    public bool TryGetRemoteHash(int targetTick, out ulong hash)
    {
        return inGameContext.remoteHashBuffer.TryGetValue(targetTick, out hash);
    }

    public void ClearBuffer()
    {
        selectContext.Reset();
        inGameContext.Clear();
    }
}