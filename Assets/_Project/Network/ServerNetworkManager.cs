using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System;

public class ServerNetworkManager : MonoBehaviour
{
    public static ServerNetworkManager Instance { get; private set; }

    public event Action<int, int> OnRoomPingsUpdated;
    public event Action<string> OnConnectionFailed;
    public event Action OnConnectionEstablished;
    public event Action<byte, RoomMetadata[]> OnSearchRoomResponseReceived;
    public event Action<bool, string, bool> OnJoinRoomResponseReceived;
    public event Action<byte, string> OnChatMessageReceived;
    public event Action<RoomStateModel> OnRoomStateBroadcastReceived;

    public event Action<int, bool, int, int, bool, int> OnSelectBroadcastReceived;
    public event Action<bool> OnCountdownUpdateReceived;
    public event Action OnTransitionAvailableToStageSelectReceived;
    public event Action<GameSceneType> OnSceneChangeReceived;
    public event Action OnGameStartReceived;
    public event Action<int> OnSlotAssignedReceived;
    public event Action<GameSceneType> OnMatchAbortedReceived;
    public event Action OnRoundVerifiedReceived;
    public event Action<bool, bool> OnRematchSyncReceived;


    public event Action<int, bool, int, bool> OnStageSelectBroadcastReceived;
    public event Action<int> OnStageRouletteStartReceived;

    private NetworkDriver serverDriver;
    private NetworkConnection serverConnection;
    
    private bool isInitialized;
    private bool isConnected;
    private float lastPingTime;
    private float lastServerPacketReceiveTime;
    private const float PING_INTERVAL = 2.0f;
    private const float SERVER_TIMEOUT_LIMIT = 5.0f;

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
    }

    private void Update()
    {
        PumpServerEvents();
        ProcessServerPing();
    }

    private void OnDestroy()
    {
        CleanupDriver();
    }

    public void InitializeNetwork(string serverIp, ushort port)
    {
        if (isInitialized) return;

        if (serverDriver.IsCreated)
        {
            serverDriver.ScheduleUpdate().Complete();
            serverDriver.Dispose();
        }

        serverDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(serverIp, port);
        serverConnection = serverDriver.Connect(endpoint);
        
        lastServerPacketReceiveTime = Time.realtimeSinceStartup;
        isInitialized = true;
    }

    public void SendCreateRoomRequest(RoomCreateData data)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.CreateRoomRequest);
            writer.WriteFixedString64(new FixedString64Bytes(data.RoomName));
            writer.WriteByte((byte)(data.IsPrivate ? 1 : 0));
            writer.WriteByte((byte)(data.UsePassword ? 1 : 0));
            writer.WriteFixedString64(new FixedString64Bytes(data.UsePassword ? data.Password : ""));
            
            serverDriver.EndSend(writer);
        }
    }

    public void SendSearchRoomRequest(byte searchType, string query)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.SearchRoomRequest);
            writer.WriteByte(searchType);
            writer.WriteFixedString64(new FixedString64Bytes(query));
            serverDriver.EndSend(writer);
        }
    }

    public void SendRandomMatchRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.RandomMatchRequest);
            serverDriver.EndSend(writer);
        }
    }

    public void SendJoinRoomRequest(string roomCode, string password)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.JoinRoomRequest);
            writer.WriteFixedString64(new FixedString64Bytes(roomCode));
            writer.WriteFixedString64(new FixedString64Bytes(password ?? ""));
            serverDriver.EndSend(writer);
        }
    }

    public void SendChatMessage(string message)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.ChatMessage);
            writer.WriteFixedString128(new FixedString128Bytes(message));
            serverDriver.EndSend(writer);
        }
    }

    public void SendRuleUpdate(int rounds, int timeLimit)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.RuleUpdate);
            writer.WriteInt(rounds);
            writer.WriteInt(timeLimit);
            serverDriver.EndSend(writer);
        }
    }

    public void SendReadyStateUpdate(bool isReady)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.ReadyStateUpdate);
            writer.WriteByte((byte)(isReady ? 1 : 0));
            serverDriver.EndSend(writer);
        }
    }
    
    public void SendLobbyStartRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.LobbyStartRequest);
            serverDriver.EndSend(writer);
        }
    }

    public void SendRoomLeaveRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.RoomLeaveRequest);
            serverDriver.EndSend(writer);
        }
    }
    
    public void SendCancelPhaseRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.CancelPhaseRequest);
            serverDriver.EndSend(writer);
        }
    }

    public void SendSelectUpdate(int playerIndex, int characterIndex, bool isLocked)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

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
        if (!isConnected || !serverConnection.IsCreated) return;
        
        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.SideUpdate);
            writer.WriteInt(side);
            serverDriver.EndSend(writer);
        }
    }

    public void SendStartRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;
        
        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.TransitionRequestToStageSelect);
            serverDriver.EndSend(writer);
        }
    }


    public void SendStageSelectUpdate(int index, bool isLocked, bool isRandom, int validCount)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.StageSelectUpdate);
            writer.WriteInt(index);
            writer.WriteByte((byte)(isLocked ? 1 : 0));
            writer.WriteByte((byte)(isRandom ? 1 : 0));
            writer.WriteInt(validCount);
            serverDriver.EndSend(writer);
        }
    }

    public void SendHandshake()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.Handshake);
            serverDriver.EndSend(writer);
            Debug.Log("[CL-NET] Handshake packet sent to driver.");
        }
    }

    public void SendRoundEndReport(int winnerSlot, int p1Wins, int p2Wins)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.RoundEndReport);
            writer.WriteInt(winnerSlot);
            writer.WriteInt(p1Wins);
            writer.WriteInt(p2Wins);
            serverDriver.EndSend(writer);
        }
    }

    public void SendMatchEndAction(MatchEndActionType actionType)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.MatchEndActionRequest);
            writer.WriteByte((byte)actionType);
            serverDriver.EndSend(writer);
        }
    }

    public void SendMatchAbortRequest()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.MatchAborted);
            serverDriver.EndSend(writer);
        }
    }

    public void SendReportDisconnect()
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.ReportDisconnect);
            serverDriver.EndSend(writer);
        }
    }

    private void ProcessServerPing()
    {
        if (!serverConnection.IsCreated) return;

        float currentTime = Time.realtimeSinceStartup;
        
        if (currentTime - lastServerPacketReceiveTime > SERVER_TIMEOUT_LIMIT)
        {
            HandleServerTimeout();
            return;
        }

        if (!isConnected) return;

        if (currentTime - lastPingTime > PING_INTERVAL)
        {
            lastPingTime = currentTime;
            
            int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
            if (sendStatus == 0)
            {
                writer.WriteByte(NetworkPacketType.ServerPing);
                writer.WriteFloat(currentTime);
                serverDriver.EndSend(writer);
            }
        }
    }

    private void PumpServerEvents()
    {
        if (!isInitialized || !serverDriver.IsCreated) return;

        serverDriver.ScheduleUpdate().Complete();
        ProcessConnectionEvents();
    }

    private void ProcessConnectionEvents()
    {
        DataStreamReader stream;
        NetworkEvent.Type cmd;

        while ((cmd = serverDriver.PopEventForConnection(serverConnection, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                isConnected = true;
                lastServerPacketReceiveTime = Time.realtimeSinceStartup;
                OnConnectionEstablished?.Invoke();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                byte packetType = stream.ReadByte();
                HandleServerData(packetType, ref stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                bool wasConnected = isConnected;
                isConnected = false;
                isInitialized = false;
                serverConnection = default;

                if (wasConnected)
                {
                    OnMatchAbortedReceived?.Invoke(GameSceneType.Start);
                }
                else
                {
                    OnConnectionFailed?.Invoke("Failed to connect to the server.");
                }
            }
        }
    }    

    private void HandleServerData(byte packetType, ref DataStreamReader stream)
    {
        lastServerPacketReceiveTime = Time.realtimeSinceStartup;

        if (packetType == NetworkPacketType.SearchRoomResponse)
        {
            byte searchType = stream.ReadByte();
            int count = stream.ReadInt();
            RoomMetadata[] rooms = new RoomMetadata[count];
            
            for (int i = 0; i < count; i++)
            {
                rooms[i] = new RoomMetadata();
                rooms[i].RoomCode = stream.ReadFixedString64().ToString();
                rooms[i].RoomTitle = stream.ReadFixedString64().ToString();
                rooms[i].PlayerCount = stream.ReadByte();
                rooms[i].HasPassword = stream.ReadByte() == 1;
                rooms[i].MaxPlayerCount = 2;
            }
            
            OnSearchRoomResponseReceived?.Invoke(searchType, rooms);
        }
        else if (packetType == NetworkPacketType.JoinRoomResponse)
        {
            bool isSuccess = stream.ReadByte() == 1;
            string roomCodeOrReason = stream.ReadFixedString64().ToString();
            bool isHost = stream.ReadByte() == 1;
            
            OnJoinRoomResponseReceived?.Invoke(isSuccess, roomCodeOrReason, isHost);
        }
        else if (packetType == NetworkPacketType.ChatMessage)
        {
            byte senderType = stream.ReadByte();
            string message = stream.ReadFixedString128().ToString();
            OnChatMessageReceived?.Invoke(senderType, message);
        }
        else if (packetType == NetworkPacketType.RoomStateBroadcast)
        {
            RoomStateModel updatedModel = new RoomStateModel();
            
            updatedModel.isP1Connected = stream.ReadByte() == 1;
            updatedModel.isP2Connected = stream.ReadByte() == 1;
            
            updatedModel.maxRounds = stream.ReadInt();
            updatedModel.roundTimeLimit = stream.ReadInt();
            
            updatedModel.p1Wins = stream.ReadInt();
            updatedModel.p1Losses = stream.ReadInt();
            updatedModel.p2Wins = stream.ReadInt();
            updatedModel.p2Losses = stream.ReadInt();
            
            updatedModel.isP1Ready = stream.ReadByte() == 1;
            updatedModel.isP2Ready = stream.ReadByte() == 1;

            OnRoomStateBroadcastReceived?.Invoke(updatedModel);
        }
        else if (packetType == NetworkPacketType.SelectBroadcast)
        {
            int p1Idx = stream.ReadInt();
            bool p1Lock = stream.ReadByte() == 1;
            int p1Side = stream.ReadInt();
            int p2Idx = stream.ReadInt();
            bool p2Lock = stream.ReadByte() == 1;
            int p2Side = stream.ReadInt();
            OnSelectBroadcastReceived?.Invoke(p1Idx, p1Lock, p1Side, p2Idx, p2Lock, p2Side);
        }
        else if (packetType == NetworkPacketType.AssignSlot)
        {
            int assignedSlot = stream.ReadInt();
            OnSlotAssignedReceived?.Invoke(assignedSlot);
        }
        else if (packetType == NetworkPacketType.CountdownUpdate)
        {
            bool isStarted = stream.ReadByte() == 1;
            OnCountdownUpdateReceived?.Invoke(isStarted);
        }
        else if (packetType == NetworkPacketType.TransitionAvailableToStageSelect)
        {
            OnTransitionAvailableToStageSelectReceived?.Invoke();
        }
        else if (packetType == NetworkPacketType.SceneChange)
        {
            int targetSceneIndex = stream.ReadInt();
            GameSceneType targetSceneType = (GameSceneType)targetSceneIndex;
            OnSceneChangeReceived?.Invoke(targetSceneType);
        }
        else if (packetType == NetworkPacketType.GameStart)
        {
            stream.ReadFixedString64();
            OnGameStartReceived?.Invoke();
        }
        else if (packetType == NetworkPacketType.ServerPong)
        {
            float sentTime = stream.ReadFloat();
            int pingMs = Mathf.RoundToInt((Time.realtimeSinceStartup - sentTime) * 1000f);
            
            SendPingReport(pingMs);
        }
        else if (packetType == NetworkPacketType.RoomPingUpdate)
        {
            int p1Ping = stream.ReadInt();
            int p2Ping = stream.ReadInt();
            OnRoomPingsUpdated?.Invoke(p1Ping, p2Ping);
        }
        else if (packetType == NetworkPacketType.MatchAborted)
        {
            int sceneTypeInt = stream.ReadInt();
            OnMatchAbortedReceived?.Invoke((GameSceneType)sceneTypeInt);
        }
        else if (packetType == NetworkPacketType.RoundVerified)
        {
            OnRoundVerifiedReceived?.Invoke();
        }
        else if (packetType == NetworkPacketType.RematchSyncBroadcast)
        {
            bool p1Ready = stream.ReadByte() == 1;
            bool p2Ready = stream.ReadByte() == 1;
            OnRematchSyncReceived?.Invoke(p1Ready, p2Ready);
        }
        else if (packetType == NetworkPacketType.StageSelectBroadcast)
        {
            int p1Idx = stream.ReadInt();
            bool p1Lock = stream.ReadByte() == 1;
            int p2Idx = stream.ReadInt();
            bool p2Lock = stream.ReadByte() == 1;
            OnStageSelectBroadcastReceived?.Invoke(p1Idx, p1Lock, p2Idx, p2Lock);
        }
        else if (packetType == NetworkPacketType.StageRouletteStart)
        {
            int finalIndex = stream.ReadInt();
            OnStageRouletteStartReceived?.Invoke(finalIndex);
        }
    }

    private void SendPingReport(int pingMs)
    {
        if (!isConnected || !serverConnection.IsCreated) return;

        int sendStatus = serverDriver.BeginSend(NetworkPipeline.Null, serverConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.ReportPing);
            writer.WriteInt(pingMs);
            serverDriver.EndSend(writer);
        }
    }

    private void HandleServerTimeout()
    {
        bool wasConnected = isConnected;
        isConnected = false;
        isInitialized = false;
        
        if (serverConnection.IsCreated)
        {
            serverDriver.Disconnect(serverConnection);
        }
        
        serverConnection = default;

        if (wasConnected)
        {
            OnMatchAbortedReceived?.Invoke(GameSceneType.Start);
        }
        else
        {
            OnConnectionFailed?.Invoke("Server connection timed out.");
        }
    }

    private void CleanupDriver()
    {
        if (serverDriver.IsCreated)
        {
            if (serverConnection.IsCreated)
            {
                serverDriver.Disconnect(serverConnection);
            }
            serverDriver.ScheduleUpdate().Complete();
            serverDriver.Dispose();
            serverDriver = default;
        }
        serverConnection = default;
        isConnected = false;
        isInitialized = false;
    }
}