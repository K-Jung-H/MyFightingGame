using UnityEngine;

public enum ConnectionMode
{
    None,
    Offline,
    OnlineHost,
    OnlineClient
}

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }
    
    public ConnectionMode currentMode = ConnectionMode.None;
    
    private MatchedRoomManager localRoomManager;
    private DummyMatchServer dummyServer;
    private bool isFlowInitialized;
    private bool isServerRunning;
    private bool isConnected;

    public MatchedRoomManager GetLocalRoomManager() => localRoomManager;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void InitializeMatchFlow(ConnectionMode mode)
    {
        if (isFlowInitialized || mode == ConnectionMode.None) return;
        
        currentMode = mode;
        isFlowInitialized = true;

        if (currentMode == ConnectionMode.Offline)
        {
            CreateLocalRoomManager();
        }
        else
        {
            NetworkSessionManager.Instance.InitializeNetwork("127.0.0.1", currentMode == ConnectionMode.OnlineHost);
        }
    }

    private void CreateLocalRoomManager()
    {
        localRoomManager = new MatchedRoomManager();
        localRoomManager.Initialize();
    }

    private void OnGUI()
    {
        if (isFlowInitialized)
        {
            float debugWidth = 200f;
            float debugHeight = 50f;
            string status = isServerRunning ? "Mode: Host" : (currentMode == ConnectionMode.Offline ? "Mode: Offline" : "Mode: Client");
            GUI.Label(new Rect(10f, 10f, debugWidth, debugHeight), status);
            return;
        }

        float width = 200f;
        float height = 50f;
        float spacing = 15f;
        float startX = (Screen.width - width) * 0.5f;
        float startY = (Screen.height - (height * 3f + spacing * 2f)) * 0.5f;

        if (GUI.Button(new Rect(startX, startY, width, height), "Offline Mode"))
        {
            InitializeMatchFlow(ConnectionMode.Offline);
        }

        if (GUI.Button(new Rect(startX, startY + height + spacing, width, height), "Start as Host"))
        {
            GameObject serverObj = new GameObject("DummyServer");
            dummyServer = serverObj.AddComponent<DummyMatchServer>();
            dummyServer.StartServer();
            isServerRunning = true;

            InitializeMatchFlow(ConnectionMode.OnlineHost);
            isConnected = true;
        }

        if (GUI.Button(new Rect(startX, startY + (height + spacing) * 2f, width, height), "Start as Client"))
        {
            InitializeMatchFlow(ConnectionMode.OnlineClient);
            isConnected = true;
        }
    }
}