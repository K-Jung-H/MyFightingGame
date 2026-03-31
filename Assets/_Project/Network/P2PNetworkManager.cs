using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;
using System;

public class P2PNetworkManager : MonoBehaviour
{
    private NetworkDriver p2pDriver;
    private NetworkConnection peerConnection;
    
    private Dictionary<int, ushort> remoteInputBuffer;
    private Dictionary<int, ulong> remoteHashBuffer;
    private Dictionary<int, ushort> localInputHistory;

    private bool isInitialized;
    private bool isConnected;
    private bool isHostingPeer;
    private bool isP2PDisconnected;

    private int currentPingMs;
    private float lastP2PPacketReceiveTime;
    private float lastP2PPingSendTime;

    private const int REDUNDANT_INPUT_COUNT = 15;
    private const float P2P_PING_INTERVAL = 0.5f;
    private const float P2P_TIMEOUT_LIMIT = 7.0f;

    private void Awake()
    {
        Application.runInBackground = true;
        remoteInputBuffer = new Dictionary<int, ushort>();
        remoteHashBuffer = new Dictionary<int, ulong>();
        localInputHistory = new Dictionary<int, ushort>();
    }

    private void OnDestroy()
    {
        CleanupDriver();
    }

    /*
     * 호스트 권한일 경우 지정된 포트를 바인딩하고 리슨 상태로 진입합니다.
     */
    public void InitializeDriverAsHost(ushort port)
    {
        if (isInitialized) return;

        p2pDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
        p2pDriver.Bind(endpoint);
        p2pDriver.Listen();
        
        isHostingPeer = true;
        isInitialized = true;
    }

    /*
     * 클라이언트 권한일 경우 타겟 IP와 포트로 접속을 시도합니다.
     */
    public void ConnectToPeer(string ip, ushort port)
    {
        if (isInitialized || isHostingPeer) return;

        p2pDriver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(ip, port);
        peerConnection = p2pDriver.Connect(endpoint);
        
        isInitialized = true;
    }

    /*
     * 인게임 시뮬레이션 고정 틱에서 호출되어 드라이버를 펌핑하고 이벤트를 처리합니다.
     */
    public void PumpNetworkTick()
    {
        if (!isInitialized || !p2pDriver.IsCreated) return;

        p2pDriver.ScheduleUpdate().Complete();
        ProcessPeerEvents();
        ProcessPingTimers();
    }

    /*
     * 현재 입력과 과거 N프레임의 입력을 함께 압축하여 중복(Redundancy) 전송합니다.
     */
    public void SendLocalInput(int currentTick, ushort localInput)
    {
        if (!isConnected || !peerConnection.IsCreated) return;

        localInputHistory[currentTick] = localInput;

        int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.Input);
            writer.WriteInt(currentTick);
            
            for (int i = 0; i < REDUNDANT_INPUT_COUNT; i++)
            {
                int pastTick = currentTick - i;
                ushort inputToSend = localInputHistory.TryGetValue(pastTick, out ushort val) ? val : (ushort)0;
                writer.WriteUShort(inputToSend);
            }
            
            p2pDriver.EndSend(writer);
        }

        localInputHistory.Remove(currentTick - REDUNDANT_INPUT_COUNT - 10);
    }

    /*
     * 검증용 해시 데이터를 상대방에게 전송합니다.
     */
    public void SendSyncHash(int tick, ulong hash)
    {
        if (!isConnected || !peerConnection.IsCreated) return;

        int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.Hash);
            writer.WriteInt(tick);
            writer.WriteULong(hash);
            p2pDriver.EndSend(writer);
        }
    }

    /*
     * 특정 틱의 상대방 입력 데이터가 수신되었는지 확인하고 반환합니다.
     */
    public bool TryGetRemoteInput(int tick, out ushort input)
    {
        return remoteInputBuffer.TryGetValue(tick, out input);
    }

    /*
     * 특정 틱의 상대방 해시 데이터가 수신되었는지 확인하고 반환합니다.
     */
    public bool TryGetRemoteHash(int tick, out ulong hash)
    {
        return remoteHashBuffer.TryGetValue(tick, out hash);
    }

    /*
     * 현재 P2P 커넥션이 완전히 연결된 상태인지 반환합니다.
     */
    public bool GetIsConnected()
    {
        return isConnected;
    }

    /*
     * 최근 계산된 P2P 핑(Ping) 값을 밀리초 단위로 반환합니다.
     */
    public int GetCurrentPingMs()
    {
        return currentPingMs;
    }

    /*
     * 새로운 매치를 위해 버퍼를 완전히 초기화합니다.
     */
    public void ClearBuffer()
    {
        remoteInputBuffer.Clear();
        remoteHashBuffer.Clear();
        localInputHistory.Clear();
    }

    /*
     * 네트워크 큐에서 이벤트를 꺼내어 연결, 데이터 수신, 연결 해제 처리를 수행합니다.
     */
    private void ProcessPeerEvents()
    {
        if (isHostingPeer && !peerConnection.IsCreated)
        {
            NetworkConnection incomingConnection;
            while ((incomingConnection = p2pDriver.Accept()) != default)
            {
                peerConnection = incomingConnection;
                isConnected = true;
                lastP2PPacketReceiveTime = Time.realtimeSinceStartup;
                lastP2PPingSendTime = Time.realtimeSinceStartup;
            }
        }

        if (!peerConnection.IsCreated) return;

        DataStreamReader stream;
        NetworkEvent.Type cmd;

        while ((cmd = p2pDriver.PopEventForConnection(peerConnection, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                isConnected = true;
                lastP2PPacketReceiveTime = Time.realtimeSinceStartup;
                lastP2PPingSendTime = Time.realtimeSinceStartup;
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                byte packetType = stream.ReadByte();
                HandlePeerData(packetType, ref stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                isConnected = false;
                peerConnection = default;
                if (!isP2PDisconnected) HandleP2PTimeout();
            }
        }
    }

    /*
     * 수신된 패킷 타입에 따라 입력, 해시, 핑/폰 데이터를 파싱하여 버퍼에 저장합니다.
     */
    private void HandlePeerData(byte packetType, ref DataStreamReader stream)
    {
        lastP2PPacketReceiveTime = Time.realtimeSinceStartup;

        if (packetType == NetworkPacketType.Input)
        {
            int baseTick = stream.ReadInt();
            
            for (int i = 0; i < REDUNDANT_INPUT_COUNT; i++)
            {
                int targetTick = baseTick - i;
                ushort receivedInput = stream.ReadUShort();
                
                if (targetTick >= 0 && !remoteInputBuffer.ContainsKey(targetTick))
                {
                    remoteInputBuffer[targetTick] = receivedInput;
                }
            }
        }
        else if (packetType == NetworkPacketType.Hash)
        {
            int tick = stream.ReadInt();
            ulong hash = stream.ReadULong();
            remoteHashBuffer[tick] = hash;
        }
        else if (packetType == NetworkPacketType.P2PPing)
        {
            uint sentTimeMs = stream.ReadUInt();
            SendPong(sentTimeMs);
        }
        else if (packetType == NetworkPacketType.P2PPong)
        {
            uint sentTimeMs = stream.ReadUInt();
            uint rtt = GetCurrentTimeMs() - sentTimeMs;
            
            if (currentPingMs == 0) currentPingMs = (int)rtt;
            else currentPingMs = Mathf.RoundToInt(currentPingMs * 0.8f + rtt * 0.2f);
        }
    }

    /*
     * 패킷 수신 간격을 측정하여 일정 시간 응답이 없으면 타임아웃 처리 및 핑을 발송합니다.
     */
    private void ProcessPingTimers()
    {
        if (!isConnected || isP2PDisconnected || lastP2PPacketReceiveTime <= 0f) return;

        float currentTime = Time.realtimeSinceStartup;

        if (currentTime - lastP2PPacketReceiveTime > P2P_TIMEOUT_LIMIT)
        {
            HandleP2PTimeout();
        }
        else if (currentTime - lastP2PPingSendTime > P2P_PING_INTERVAL)
        {
            if (p2pDriver.IsCreated && peerConnection.IsCreated)
            {
                SendPing();
            }
            lastP2PPingSendTime = currentTime;
        }
    }

    /*
     * 연결 확인용 Ping 패킷을 전송합니다.
     */
    private void SendPing()
    {
        int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.P2PPing);
            writer.WriteUInt(GetCurrentTimeMs());
            p2pDriver.EndSend(writer);
        }
    }

    /*
     * Ping을 보낸 상대에게 Pong으로 응답하여 왕복 지연 시간을 측정할 수 있게 합니다.
     */
    private void SendPong(uint receivedTimeMs)
    {
        int sendStatus = p2pDriver.BeginSend(NetworkPipeline.Null, peerConnection, out DataStreamWriter writer);
        if (sendStatus == 0)
        {
            writer.WriteByte(NetworkPacketType.P2PPong);
            writer.WriteUInt(receivedTimeMs);
            p2pDriver.EndSend(writer);
        }
    }

    /*
     * 타임아웃 발생 시 연결을 강제로 끊고 시스템에 보고합니다.
     */
    private void HandleP2PTimeout()
    {
        isP2PDisconnected = true;
        
        if (ServerNetworkManager.Instance != null)
        {
            // [주의] ServerNetworkManager에 24번 패킷을 보내는 SendReportDisconnect()가 존재해야 합니다.
            // ServerNetworkManager.Instance.SendReportDisconnect();
        }

        if (p2pDriver.IsCreated && peerConnection.IsCreated)
        {
            p2pDriver.Disconnect(peerConnection);
        }
        peerConnection = default;
    }

    /*
     * 네트워크 소켓 자원을 안전하게 해제합니다.
     */
    private void CleanupDriver()
    {
        if (p2pDriver.IsCreated)
        {
            if (peerConnection.IsCreated)
            {
                p2pDriver.Disconnect(peerConnection);
            }
            p2pDriver.ScheduleUpdate().Complete();
            p2pDriver.Dispose();
            p2pDriver = default;
        }
        
        peerConnection = default;
        isConnected = false;
        isInitialized = false;
        isHostingPeer = false;
    }

    /*
     * 시스템 시작 이후 경과 시간을 밀리초(ms) 단위의 부호 없는 정수로 반환합니다.
     */
    private uint GetCurrentTimeMs()
    {
        return (uint)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}