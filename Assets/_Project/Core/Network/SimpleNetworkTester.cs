using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;

public class SimpleNetworkTester : MonoBehaviour
{
    private NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private bool isServer;
    private bool isInitialized;
    private float nextSendTime;

    private void Awake()
    {
        Application.runInBackground = true;
        Screen.SetResolution(1024, 768, FullScreenMode.Windowed);
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

    private void InitializeNetwork(bool startAsServer)
    {
        driver = NetworkDriver.Create();
        connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        isServer = startAsServer;
        isInitialized = true;

        if (isServer)
        {
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(9000);
            if (driver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port 9000.");
                return;
            }
            driver.Listen();
            Debug.Log("Host started. Listening on port 9000...");
        }
        else
        {
            NetworkEndpoint endpoint = NetworkEndpoint.Parse("127.0.0.1", 9000);
            connections.Add(driver.Connect(endpoint));
            Debug.Log("Client started. Connecting to Host...");
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

    private void Update()
    {
        if (!isInitialized) return;

        driver.ScheduleUpdate().Complete();

        CleanUpConnections();
        AcceptNewConnections();
        ProcessEvents();
        BroadcastDummyPacket();
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
            Debug.Log("Accepted a connection.");
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
                    Debug.Log("Successfully connected to the server.");
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    uint receivedTick = stream.ReadUInt();
                    Debug.Log($"Received dummy tick from network: {receivedTick}");
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected.");
                    connections[i] = default;
                }
            }
        }
    }

    private void BroadcastDummyPacket()
    {
        if (connections.Length == 0) return;

        if (Time.time >= nextSendTime)
        {
            uint currentTickToBroadcast = (uint)(Time.frameCount);
            
            for (int i = 0; i < connections.Length; i++)
            {
                if (connections[i].IsCreated)
                {
                    driver.BeginSend(NetworkPipeline.Null, connections[i], out DataStreamWriter writer);
                    writer.WriteUInt(currentTickToBroadcast);
                    driver.EndSend(writer);
                }
            }
            nextSendTime = Time.time + 1.0f;
        }
    }
}