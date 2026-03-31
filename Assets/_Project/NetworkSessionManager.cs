// using UnityEngine;
// using Unity.Networking.Transport;
// using Unity.Collections;
// using System.Collections.Generic;
// using System;


// public class NetworkBufferContext
// {
//     public Dictionary<int, ushort> remoteInputBuffer;
//     public Dictionary<int, ushort> localInputHistory;
//     public Dictionary<int, ulong> remoteHashBuffer;

//     public NetworkBufferContext()
//     {
//         remoteInputBuffer = new Dictionary<int, ushort>();
//         localInputHistory = new Dictionary<int, ushort>();
//         remoteHashBuffer = new Dictionary<int, ulong>();
//     }

//     public void Clear()
//     {
//         remoteInputBuffer.Clear();
//         localInputHistory.Clear();
//         remoteHashBuffer.Clear();
//     }
// }

// public class NetworkSessionManager : MonoBehaviour
// {
//     public static NetworkSessionManager Instance { get; private set; }

//     [SerializeField] private float p2pPingInterval = 0.5f;
//     [SerializeField] private float p2pTimeoutLimit = 7.0f;

//     private NetworkDriver serverDriver;
//     private NetworkDriver p2pDriver;
//     private NetworkConnection serverConnection;
//     private NetworkConnection peerConnection;
    
//     private bool isInitialized;
//     private bool isConnected;
//     private bool isHostingPeer;
//     private bool isP2PDisconnected;
//     private bool isSessionResetRequired;
//     private const int REDUNDANCY_COUNT = 15;
//     private NetworkBufferContext bufferContext;

//     private float lastP2PPingSendTime;
//     public float lastP2PPacketReceiveTime;
//     public float lastServerPacketReceiveTime;
//     private int currentPingMs;

//     public event Action<int, bool, int, int, bool, int> OnSelectBroadcastReceived;
//     public event Action<bool> OnCountdownUpdateReceived;
//     public event Action OnStartButtonActiveReceived;
//     public event Action OnConnectionEstablished;
//     public event Action OnSceneChangeReceived;
//     public event Action OnGameStartReceived;
//     public event Action<string> OnPeerAddressReceived;
//     public event Action<int> OnSlotAssignedReceived;
//     public event Action<int> OnPingUpdated;
//     public event Action<GameSceneType> OnMatchAbortedReceived;

//     public event Action<byte, RoomMetadata[]> OnSearchRoomResponseReceived;
//     public event Action<bool, string, bool> OnJoinRoomResponseReceived;

//     private void Awake()
//     {
//         if (Instance != null && Instance != this)
//         {
//             Destroy(gameObject);
//             return;
//         }

//         Instance = this;
//         DontDestroyOnLoad(gameObject);

//         Application.runInBackground = true;
//         Screen.SetResolution(1024, 768, FullScreenMode.Windowed);

//         bufferContext = new NetworkBufferContext();
//     }

//     private void OnApplicationQuit()
//     {
//         CleanupDrivers();
//     }

//     private void OnDestroy()
//     {
//         CleanupDrivers();
//     }

//     public bool GetIsConnected() => isConnected;

//     public void InitializeNetwork(string serverIp)
//     {
//         if (isInitialized) return;

//         serverDriver = CreateConfiguredDriver();
//         NetworkEndpoint endpoint = NetworkEndpoint.Parse(serverIp, (ushort)9000);
//         serverConnection = serverDriver.Connect(endpoint);
        
//         lastServerPacketReceiveTime = 0f;
//         isInitialized = true;
//     }

//     public void StartP2PListen(ushort port = 9001)
//     {
//         if (isHostingPeer) return;
        
//         p2pDriver = CreateConfiguredDriver();
//         NetworkEndpoint p2pEndpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
//         p2pDriver.Bind(p2pEndpoint);
//         p2pDriver.Listen();
//         isHostingPeer = true;
        
//         lastP2PPacketReceiveTime = 0f;
//         lastP2PPingSendTime = 0f;
//     }

//     public void ConnectToPeer(string peerIp)
//     {
//         if (isHostingPeer) return;
        
//         p2pDriver = CreateConfiguredDriver();
//         NetworkEndpoint endpoint = NetworkEndpoint.Parse(peerIp, (ushort)9001);
//         peerConnection = p2pDriver.Connect(endpoint);
        
//         lastP2PPacketReceiveTime = 0f;
//         lastP2PPingSendTime = 0f;
//     }

//     public void UpdateNetwork()
//     {
//         if (isSessionResetRequired)
//         {
//             ExecuteSessionReset();
//             return;
//         }

//         if (!isInitialized) return;

//         if (serverDriver.IsCreated)
//         {
//             serverDriver.ScheduleUpdate().Complete();
//         }

//         if (p2pDriver.IsCreated)
//         {
//             p2pDriver.ScheduleUpdate().Complete();
//         }

//         if (isHostingPeer && p2pDriver.IsCreated && !peerConnection.IsCreated)
//         {
//             NetworkConnection c;
//             while ((c = p2pDriver.Accept()) != default)
//             {
//                 peerConnection = c;
//                 isConnected = true;
                
//                 float currentTime = Time.realtimeSinceStartup;
//                 lastP2PPacketReceiveTime = currentTime;
//                 lastP2PPingSendTime = currentTime;
//             }
//         }

//         ProcessEvents();
//         ProcessPingTimers();
//     }

//     public void ResetNetworkSession()
//     {
//         isSessionResetRequired = true;
//     }

//     public void SendCreateRoomRequest(RoomCreateData data)
//     {
//         if (!serverConnection.IsCreated) return;

//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.CreateRoomRequest);
//             writer.WriteFixedString64(new FixedString64Bytes(data.RoomName));
//             writer.WriteByte((byte)(data.IsPrivate ? 1 : 0));
//             writer.WriteByte((byte)(data.UsePassword ? 1 : 0));
//             writer.WriteFixedString64(new FixedString64Bytes(data.Password));
//             serverDriver.EndSend(writer);
//         }
//     }

//     public void SendSearchRoomRequest(byte searchType, string query)
//     {
//         if (!serverConnection.IsCreated) return;

//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.SearchRoomRequest);
//             writer.WriteByte(searchType);
//             writer.WriteFixedString64(new FixedString64Bytes(query));
//             serverDriver.EndSend(writer);
//         }
//     }

//     public void SendJoinRoomRequest(string roomCode, string password)
//     {
//         if (!serverConnection.IsCreated) return;

//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.JoinRoomRequest);
//             writer.WriteFixedString64(new FixedString64Bytes(roomCode));
//             writer.WriteFixedString64(new FixedString64Bytes(password));
//             serverDriver.EndSend(writer);
//         }
//     }

//     public void SendSelectUpdate(int playerIndex, int characterIndex, bool isLocked)
//     {
//         if (!serverConnection.IsCreated) return;

//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.SelectUpdate);
//             writer.WriteInt(playerIndex);
//             writer.WriteInt(characterIndex);
//             writer.WriteByte((byte)(isLocked ? 1 : 0));
//             serverDriver.EndSend(writer);
//         }
//     }

//     public void SendSideUpdate(int side)
//     {
//         if (!serverConnection.IsCreated) return;
        
//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.SideUpdate);
//             writer.WriteInt(side);
//             serverDriver.EndSend(writer);
//         }
//     }

//     public void SendHandshake()
//     {
//         if (!serverConnection.IsCreated) return;

//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.Handshake);
//             serverDriver.EndSend(writer);
//         }
//     }

//     public void SendStartRequest()
//     {
//         if (!serverConnection.IsCreated) return;
        
//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.StartRequest);
//             serverDriver.EndSend(writer);
//         }
//     }

//     public void SendLocalInput(int currentTick, ushort localInput)
//     {
//         if (!peerConnection.IsCreated) return;

//         bufferContext.localInputHistory[currentTick] = localInput;

//         int startTick = Mathf.Max(0, currentTick - REDUNDANCY_COUNT + 1);
//         byte count = (byte)(currentTick - startTick + 1);

//         int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.Input);
//             writer.WriteInt(startTick);
//             writer.WriteByte(count);

//             for (int t = startTick; t <= currentTick; t++)
//             {
//                 ushort savedInput = bufferContext.localInputHistory.TryGetValue(t, out var val) ? val : (ushort)0;
//                 writer.WriteUShort(savedInput);
//             }

//             p2pDriver.EndSend(writer);
//         }

//         bufferContext.localInputHistory.Remove(currentTick - REDUNDANCY_COUNT);
//     }

//     public void SendSyncHash(int tick, ulong hash)
//     {
//         if (!peerConnection.IsCreated) return;

//         int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.Hash);
//             writer.WriteInt(tick);
//             writer.WriteULong(hash);
//             p2pDriver.EndSend(writer);
//         }
//     }

//     public bool TryGetRemoteInput(int targetTick, out ushort remoteInput)
//     {
//         return bufferContext.remoteInputBuffer.TryGetValue(targetTick, out remoteInput);
//     }

//     public bool TryGetRemoteHash(int targetTick, out ulong hash)
//     {
//         return bufferContext.remoteHashBuffer.TryGetValue(targetTick, out hash);
//     }

//     public void ClearBuffer()
//     {
//         bufferContext.Clear();
//     }

//     private void ExecuteSessionReset()
//     {
//         CleanupDrivers();
//         isInitialized = false;
//         isConnected = false;
//         isHostingPeer = false;
//         isP2PDisconnected = false;
//         isSessionResetRequired = false;
//         serverConnection = default;
//         peerConnection = default;
//         currentPingMs = 0;
//         bufferContext.Clear();
//     }

//     private void CleanupDrivers()
//     {
//         if (serverDriver.IsCreated)
//         {
//             if (serverConnection.IsCreated) serverDriver.Disconnect(serverConnection);
//             serverDriver.ScheduleUpdate().Complete();
//             serverDriver.Dispose();
//             serverDriver = default;
//         }

//         if (p2pDriver.IsCreated)
//         {
//             if (peerConnection.IsCreated) p2pDriver.Disconnect(peerConnection);
//             p2pDriver.ScheduleUpdate().Complete();
//             p2pDriver.Dispose();
//             p2pDriver = default;
//         }
        
//         serverConnection = default;
//         peerConnection = default;
//     }

//     private void ProcessEvents()
//     {
//         if (serverDriver.IsCreated)
//         {
//             ProcessConnectionEvents(serverDriver, serverConnection, true);
//         }

//         if (p2pDriver.IsCreated)
//         {
//             ProcessConnectionEvents(p2pDriver, peerConnection, false);
//         }
//     }

//     private void ProcessConnectionEvents(NetworkDriver driver, NetworkConnection conn, bool isServerSource)
//     {
//         DataStreamReader stream;
//         NetworkEvent.Type cmd;

//         while ((cmd = driver.PopEventForConnection(conn, out stream)) != NetworkEvent.Type.Empty)
//         {
//             if (cmd == NetworkEvent.Type.Connect)
//             {
//                 float currentTime = Time.realtimeSinceStartup;
//                 if (isServerSource)
//                 {
//                     lastServerPacketReceiveTime = currentTime;
//                     OnConnectionEstablished?.Invoke();
//                 }
//                 else
//                 {
//                     lastP2PPacketReceiveTime = currentTime;
//                     lastP2PPingSendTime = currentTime;
//                     isConnected = true; 
//                 }
//             }
//             else if (cmd == NetworkEvent.Type.Data)
//             {
//                 byte packetType = stream.ReadByte();
//                 if (isServerSource) HandleServerData(packetType, ref stream);
//                 else HandlePeerData(packetType, ref stream);
//             }
//             else if (cmd == NetworkEvent.Type.Disconnect)
//             {
//                 if (isServerSource)
//                 {
//                     serverConnection = default;
//                     OnMatchAbortedReceived?.Invoke(GameSceneType.Start);
//                 }
//                 else
//                 {
//                     isConnected = false;
//                     peerConnection = default;
//                     if (!isP2PDisconnected)
//                     {
//                         HandleP2PTimeout();
//                     }
//                 }
//             }
//         }
//     }

//     private void HandleServerData(byte packetType, ref DataStreamReader stream)
//     {
//         lastServerPacketReceiveTime = Time.realtimeSinceStartup;

//         if (packetType == NetworkPacketType.SelectBroadcast)
//         {
//             int p1Idx = stream.ReadInt();
//             bool p1Lock = stream.ReadByte() == 1;
//             int p1Side = stream.ReadInt();
//             int p2Idx = stream.ReadInt();
//             bool p2Lock = stream.ReadByte() == 1;
//             int p2Side = stream.ReadInt();
//             OnSelectBroadcastReceived?.Invoke(p1Idx, p1Lock, p1Side, p2Idx, p2Lock, p2Side);
//         }
//         else if (packetType == NetworkPacketType.SceneChange)
//         {
//             OnSceneChangeReceived?.Invoke();
//         }
//         else if (packetType == NetworkPacketType.GameStart)
//         {
//             FixedString64Bytes peerIp = stream.ReadFixedString64();
//             OnPeerAddressReceived?.Invoke(peerIp.ToString());
//             OnGameStartReceived?.Invoke();
//         }
//         else if (packetType == NetworkPacketType.CountdownUpdate)
//         {
//             bool isStarted = stream.ReadByte() == 1;
//             OnCountdownUpdateReceived?.Invoke(isStarted);
//         }
//         else if (packetType == NetworkPacketType.StartButtonActive)
//         {
//             OnStartButtonActiveReceived?.Invoke();
//         }
//         else if (packetType == NetworkPacketType.AssignSlot)
//         {
//             int assignedSlot = stream.ReadInt();
//             OnSlotAssignedReceived?.Invoke(assignedSlot);
//         }
//         else if (packetType == NetworkPacketType.MatchAborted)
//         {
//             int sceneTypeInt = stream.ReadInt();
//             OnMatchAbortedReceived?.Invoke((GameSceneType)sceneTypeInt);
//         }
//         else if (packetType == NetworkPacketType.SearchRoomResponse)
//         {
//             byte searchType = stream.ReadByte();
//             int roomCount = stream.ReadInt();
//             RoomMetadata[] rooms = new RoomMetadata[roomCount];
            
//             for (int i = 0; i < roomCount; i++)
//             {
//                 rooms[i] = new RoomMetadata
//                 {
//                     RoomCode = stream.ReadFixedString64().ToString(),
//                     RoomTitle = stream.ReadFixedString64().ToString(),
//                     PlayerCount = stream.ReadByte(),
//                     HasPassword = stream.ReadByte() == 1
//                 };
//             }
//             OnSearchRoomResponseReceived?.Invoke(searchType, rooms);
//         }
//         else if (packetType == NetworkPacketType.JoinRoomResponse)
//         {
//             bool isJoinSuccess = stream.ReadByte() == 1;
//             string joinedRoomCode = stream.ReadFixedString64().ToString();
//             bool isRoomHost = stream.ReadByte() == 1;
//             OnJoinRoomResponseReceived?.Invoke(isJoinSuccess, joinedRoomCode, isRoomHost);
//         }
//     }

//     private void HandlePeerData(byte packetType, ref DataStreamReader stream)
//     {
//         lastP2PPacketReceiveTime = Time.realtimeSinceStartup;

//         if (packetType == NetworkPacketType.Input)
//         {
//             int startTick = stream.ReadInt();
//             byte count = stream.ReadByte();
//             for (int j = 0; j < count; j++)
//             {
//                 ushort input = stream.ReadUShort();
//                 int tick = startTick + j;
//                 if (!bufferContext.remoteInputBuffer.ContainsKey(tick))
//                 {
//                     bufferContext.remoteInputBuffer[tick] = input;
//                 }
//             }
//         }
//         else if (packetType == NetworkPacketType.Hash)
//         {
//             int tick = stream.ReadInt();
//             ulong hash = stream.ReadULong();
//             bufferContext.remoteHashBuffer[tick] = hash;
//         }
//         else if (packetType == NetworkPacketType.P2PPing)
//         {
//             uint sentTimeMs = stream.ReadUInt();
//             SendPong(sentTimeMs);
//         }
//         else if (packetType == NetworkPacketType.P2PPong)
//         {
//             uint sentTimeMs = stream.ReadUInt();
//             uint rtt = GetCurrentTimeMs() - sentTimeMs;
            
//             if (currentPingMs == 0)
//             {
//                 currentPingMs = (int)rtt;
//             }
//             else
//             {
//                 currentPingMs = Mathf.RoundToInt(currentPingMs * 0.8f + rtt * 0.2f);
//             }
            
//             OnPingUpdated?.Invoke(currentPingMs);
//         }
//     }

//     private void ProcessPingTimers()
//     {
//         float currentTime = Time.realtimeSinceStartup;

//         if (isConnected && !isP2PDisconnected && lastP2PPacketReceiveTime > 0f)
//         {
//             if (currentTime - lastP2PPacketReceiveTime > p2pTimeoutLimit)
//             {
//                 HandleP2PTimeout();
//             }
//             else if (currentTime - lastP2PPingSendTime > p2pPingInterval)
//             {
//                 if (p2pDriver.IsCreated && peerConnection.IsCreated)
//                 {
//                     SendPing();
//                 }
//                 lastP2PPingSendTime = currentTime;
//             }
//         }
//     }

//     private void SendPing()
//     {
//         int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.P2PPing);
//             writer.WriteUInt(GetCurrentTimeMs());
//             p2pDriver.EndSend(writer);
//         }
//     }

//     private void SendPong(uint receivedTimeMs)
//     {
//         int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.P2PPong);
//             writer.WriteUInt(receivedTimeMs);
//             p2pDriver.EndSend(writer);
//         }
//     }

//     private void HandleP2PTimeout()
//     {
//         isP2PDisconnected = true;
//         SendReportDisconnect();
        
//         if (p2pDriver.IsCreated && peerConnection.IsCreated)
//         {
//             p2pDriver.Disconnect(peerConnection);
//         }
//         peerConnection = default;
//     }

//     private void SendReportDisconnect()
//     {
//         if (!serverConnection.IsCreated) return;
//         int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
//         if (sendStatus == 0)
//         {
//             writer.WriteByte(NetworkPacketType.ReportDisconnect);
//             serverDriver.EndSend(writer);
//         }
//     }

//     private uint GetCurrentTimeMs()
//     {
//         return (uint)(Time.realtimeSinceStartupAsDouble * 1000.0);
//     }

//     private NetworkDriver CreateConfiguredDriver()
//     {
//         NetworkSettings settings = new NetworkSettings();
//         settings.WithNetworkConfigParameters(
//             disconnectTimeoutMS: 5000,
//             heartbeatTimeoutMS: 500
//         );
//         return NetworkDriver.Create(settings);
//     }
// }