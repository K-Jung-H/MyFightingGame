using UnityEngine;
using UnityEngine.SceneManagement;

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
        }
        else if (currentMode == ConnectionMode.OnlineHost)
        {
            StartLocalServer();
            NetworkSessionManager.Instance.InitializeNetwork("127.0.0.1", true);
            currentSession = new OnlineClientSession();
        }
        else if (currentMode == ConnectionMode.OnlineClient)
        {
            NetworkSessionManager.Instance.InitializeNetwork("127.0.0.1", false);
            currentSession = new OnlineClientSession();
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
        string netStatus = NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.GetIsConnected() ? "[Server Connected]" : "[Connecting...]";
        GUI.Label(new Rect(10f, 10f, debugWidth, debugHeight), $"{modeStr} | {netStatus}");
    }

    private void DrawConnectionGUI()
    {
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
            InitializeMatchFlow(ConnectionMode.OnlineHost);
        }

        if (GUI.Button(new Rect(startX, startY + (height + spacing) * 2f, width, height), "Start as Client"))
        {
            InitializeMatchFlow(ConnectionMode.OnlineClient);
        }
    }
}