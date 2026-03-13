using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;

public struct InputPacket
{
    public int tick;
    public ushort inputFlags;
}

public class NetworkSessionManager : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private Dictionary<int, ushort> remoteInputBuffer;
    private bool isServer;
    private bool isInitialized;
    private bool isConnected;

    public event System.Action OnConnectionEstablished;

    private void Awake()
    {
        Application.runInBackground = true;
        Screen.SetResolution(1024, 768, FullScreenMode.Windowed);
        remoteInputBuffer = new Dictionary<int, ushort>();
    }

    private void OnGUI()
    {
        if (isInitialized) return;

        if (GUI.Button(new Rect(10, 10, 150, 50), "Start Host (P1)"))
        {
            InitializeNetwork(true);
        }

        if (GUI.Button(new Rect(10, 70, 150, 50), "Start Client (P2)"))
        {
            InitializeNetwork(false);
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
        if (!isInitialized || !driver.IsCreated) return;

        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated)
            {
                driver.Disconnect(connections[i]);
            }
        }
        
        driver.ScheduleUpdate().Complete();
    }

    private void OnDestroy()
    {
        if (driver.IsCreated)
        {
            driver.Dispose();
            if (connections.IsCreated) connections.Dispose();
        }
    }

    public void BroadcastLocalInput(int currentTick, InputFlags localInput)
    {
        if (connections.Length == 0) return;

        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated)
            {
                driver.BeginSend(NetworkPipeline.Null, connections[i], out DataStreamWriter writer);
                writer.WriteInt(currentTick);
                writer.WriteUShort((ushort)localInput);
                driver.EndSend(writer);
            }
        }
    }

    public bool TryGetRemoteInput(int targetTick, out InputFlags remoteInput)
    {
        bool hasInput = remoteInputBuffer.TryGetValue(targetTick, out ushort rawInput);
        remoteInput = (InputFlags)rawInput;
        return hasInput;
    }

    public void ClearBuffer()
    {
        remoteInputBuffer.Clear();
    }

    public bool GetIsConnected() => isConnected;
    public bool GetIsInitialized() => isInitialized;
    public bool GetIsServer() => isServer;

    private void InitializeNetwork(bool startAsServer)
    {
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

    private void CleanUpConnections()
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

    private void AcceptNewConnections()
    {
        NetworkConnection c;
        while ((c = driver.Accept()) != default)
        {
            connections.Add(c);
            if (!isConnected)
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
                if (cmd == NetworkEvent.Type.Connect)
                {
                    if (!isConnected)
                    {
                        isConnected = true;
                        OnConnectionEstablished?.Invoke();
                    }
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    int receivedTick = stream.ReadInt();
                    ushort receivedFlags = stream.ReadUShort();
                    remoteInputBuffer[receivedTick] = receivedFlags;
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    isConnected = false;
                    connections[i] = default;
                }
            }
        }
    }
}