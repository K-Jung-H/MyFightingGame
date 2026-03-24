using UnityEngine;
using UnityEngine.SceneManagement;

public enum ConnectionMode
{
    None,
    Offline,
    OnlineHost,
    OnlineClient,
    DedicatedServer
}

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }
    
    public ConnectionMode currentMode = ConnectionMode.None;
    
    [SerializeField] private string gameplaySceneName = "GameplayScene";
    [SerializeField] private string serverSceneName = "EmptyServerScene";
    
    private IMatchSession currentSession;
    private DummyMatchServer dummyServer;
    private bool isFlowInitialized;

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

    private void OnGUI()
    {
        if (isFlowInitialized)
        {
            DrawStatusGUI();
            return;
        }
        DrawConnectionGUI();
    }

    public IMatchSession GetCurrentSession()
    {
        return currentSession;
    }

    public void InitializeMatchFlow(ConnectionMode mode)
    {
        if (isFlowInitialized || mode == ConnectionMode.None) return;
        
        currentMode = mode;
        isFlowInitialized = true;

        if (currentMode == ConnectionMode.Offline)
        {
            currentSession = new OfflineMatchSession();
            SceneManager.LoadScene(gameplaySceneName);
        }
        else if (currentMode == ConnectionMode.OnlineHost)
        {
            StartLocalServer();
            NetworkSessionManager.Instance.InitializeNetwork("127.0.0.1", true);
            currentSession = new OnlineClientSession();
            SceneManager.LoadScene(gameplaySceneName);
        }
        else if (currentMode == ConnectionMode.OnlineClient)
        {
            NetworkSessionManager.Instance.InitializeNetwork("127.0.0.1", false);
            currentSession = new OnlineClientSession();
            SceneManager.LoadScene(gameplaySceneName);
        }
        else if (currentMode == ConnectionMode.DedicatedServer)
        {
            StartLocalServer();
            SceneManager.LoadScene(serverSceneName);
        }
    }

    public void OnReceiveSceneChangeCommand(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void StartLocalServer()
    {
        GameObject serverObj = new GameObject("DummyServer");
        DontDestroyOnLoad(serverObj);
        
        dummyServer = serverObj.AddComponent<DummyMatchServer>();
        dummyServer.StartServer();
    }

    private void DrawStatusGUI()
    {
        float debugWidth = 300f;
        float debugHeight = 50f;
        string modeStr = $"Mode: {currentMode}";
        
        bool hasNetworkSession = NetworkSessionManager.Instance != null;
        bool isConnected = hasNetworkSession && NetworkSessionManager.Instance.GetIsConnected();
        
        string netStatus = "";
        if (currentMode == ConnectionMode.DedicatedServer)
        {
            netStatus = "[Dedicated Server Running]";
        }
        else
        {
            netStatus = isConnected ? "[Server Connected]" : "[Connecting...]";
        }
        
        GUI.Label(new Rect(10f, 10f, debugWidth, debugHeight), $"{modeStr} | {netStatus}");
    }

    private void DrawConnectionGUI()
    {
        float width = 200f;
        float height = 50f;
        float spacing = 15f;
        float startX = (Screen.width - width) * 0.5f;
        float startY = (Screen.height - (height * 4f + spacing * 3f)) * 0.5f;

        if (GUI.Button(new Rect(startX, startY, width, height), "Offline Mode"))
        {
            InitializeMatchFlow(ConnectionMode.Offline);
        }

        if (GUI.Button(new Rect(startX, startY + height + spacing, width, height), "Start as Host"))
        {
            InitializeMatchFlow(ConnectionMode.OnlineHost);
        }

        if (GUI.Button(new Rect(startX, startY + (height + spacing) * 2f, width, height), "Start as Client"))
        {
            InitializeMatchFlow(ConnectionMode.OnlineClient);
        }

        if (GUI.Button(new Rect(startX, startY + (height + spacing) * 3f, width, height), "Start Dedicated Server"))
        {
            InitializeMatchFlow(ConnectionMode.DedicatedServer);
        }
    }
}