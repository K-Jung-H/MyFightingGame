using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public enum ConnectionMode
{
    None,
    Offline,
    OnlineHost,
    OnlineClient,
    DedicatedServer
}

public enum GameSceneType
{
    Start,
    GameModeSelect,
    OnlineMatching,
    OnlineMatchedRoom,
    CharacterSelect,
    GamePlay,
    Server
}

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public ConnectionMode currentMode = ConnectionMode.None;
    public GameSceneType currentScene = GameSceneType.Start;

    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private string gameModeSelectSceneName = "GameModeSelectScene";
    [SerializeField] private string onlineMatchingSceneName = "OnlineMatchingScene";
    [SerializeField] private string onlineMatchedRoomSceneName = "OnlineMatchedRoomScene";
    [SerializeField] private string characterSelectSceneName = "CharacterSelectScene";
    [SerializeField] private string gamePlaySceneName = "GamePlayScene";
    [SerializeField] private string serverSceneName = "EmptyServerScene";

    private IMatchSession currentSession;
    private DummyMatchServer dummyServer;
    private bool isFlowInitialized;
    private int selectedPreferredSide = 0;

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

    private void Update()
    {
        if (currentScene != GameSceneType.GamePlay && currentScene != GameSceneType.Server)
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.UpdateNetwork();
            }
        }
    }

    private void OnGUI()
    {
        if (isFlowInitialized && currentScene != GameSceneType.OnlineMatching && currentScene != GameSceneType.Start)
        {
            DrawStatusGUI();
            if (currentScene == GameSceneType.Server) return;
        }

        if (currentScene == GameSceneType.Start)
        {
            DrawStartGUI();
        }
        else if (currentScene == GameSceneType.GameModeSelect)
        {
            DrawModeSelectGUI();
        }
        else if (currentScene == GameSceneType.OnlineMatching)
        {
            DrawOnlineMatchingGUI();
        }
    }

    private void OnDestroy()
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnMatchAbortedReceived -= HandleMatchAborted;
        }
    }

    public IMatchSession GetCurrentSession()
    {
        return currentSession;
    }

    public void OnReceiveSceneChangeCommand(string targetScene)
    {
        if (targetScene == "GamePlayScene" || targetScene == gamePlaySceneName)
        {
            ChangeScene(GameSceneType.GamePlay);
        }
    }

    public void ChangeScene(GameSceneType targetSceneType)
    {
        currentScene = targetSceneType;

        if (currentScene == GameSceneType.Start)
        {
            SceneManager.LoadScene(startSceneName);
        }
        else if (currentScene == GameSceneType.GameModeSelect)
        {
            SceneManager.LoadScene(gameModeSelectSceneName);
        }
        else if (currentScene == GameSceneType.OnlineMatching)
        {
            SceneManager.LoadScene(onlineMatchingSceneName);
        }
        else if (currentScene == GameSceneType.OnlineMatchedRoom)
        {
            SceneManager.LoadScene(onlineMatchedRoomSceneName);
        }
        else if (currentScene == GameSceneType.CharacterSelect)
        {
            SceneManager.LoadScene(characterSelectSceneName);
        }
        else if (currentScene == GameSceneType.GamePlay)
        {
            SceneManager.LoadScene(gamePlaySceneName);
        }
        else if (currentScene == GameSceneType.Server)
        {
            SceneManager.LoadScene(serverSceneName);
        }
    }

    private void DrawStartGUI()
    {
        float width = 250f;
        float height = 50f;
        float spacing = 15f;
        float startX = (Screen.width - width) * 0.5f;
        float startY = (Screen.height - (height * 2f + spacing)) * 0.5f;

        if (GUI.Button(new Rect(startX, startY, width, height), "Play Game (Player)"))
        {
            ChangeScene(GameSceneType.GameModeSelect);
        }

        if (GUI.Button(new Rect(startX, startY + height + spacing, width, height), "Run Dedicated Server"))
        {
            currentMode = ConnectionMode.DedicatedServer;
            isFlowInitialized = true;
            
            dummyServer = gameObject.AddComponent<DummyMatchServer>();
            dummyServer.StartServer();
            ChangeScene(GameSceneType.Server);
        }
    }

    private void DrawModeSelectGUI()
    {
        float width = 200f;
        float height = 50f;
        float spacing = 15f;
        float startX = (Screen.width - width) * 0.5f;
        float startY = (Screen.height - (height * 2f + spacing)) * 0.5f;

        if (GUI.Button(new Rect(startX, startY, width, height), "Offline Mode"))
        {
            currentMode = ConnectionMode.Offline;
            isFlowInitialized = true;
            currentSession = new OfflineMatchSession();
            currentSession.SendSideUpdate(0);
            ChangeScene(GameSceneType.CharacterSelect);
        }

        if (GUI.Button(new Rect(startX, startY + height + spacing, width, height), "Online Client"))
        {
            currentMode = ConnectionMode.OnlineClient;
            ChangeScene(GameSceneType.OnlineMatching);
        }
    }

    private void DrawOnlineMatchingGUI()
    {
        float width = 200f;
        float height = 50f;
        float spacing = 15f;
        float startX = (Screen.width - width) * 0.5f;
        float startY = (Screen.height - (height * 2f + spacing * 2f)) * 0.5f;

        selectedPreferredSide = GUI.Toolbar(new Rect(startX, startY, width, 30f), selectedPreferredSide, new string[] { "Left Side", "Right Side" });

        if (GUI.Button(new Rect(startX, startY + 70f, width, height), "Select Side & Connect"))
        {
            isFlowInitialized = true;
            NetworkSessionManager.Instance.InitializeNetwork("127.0.0.1");
            NetworkSessionManager.Instance.OnMatchAbortedReceived -= HandleMatchAborted;
            NetworkSessionManager.Instance.OnMatchAbortedReceived += HandleMatchAborted;
            SetupOnlineClientSession();
        }
    }

    private void SetupOnlineClientSession()
    {
        currentSession = new OnlineClientSession();
        
        Action onConnected = null;
        onConnected = () => 
        {
            currentSession.SendSideUpdate(selectedPreferredSide);
            ChangeScene(GameSceneType.CharacterSelect);
            NetworkSessionManager.Instance.OnConnectionEstablished -= onConnected;
        };
        
        NetworkSessionManager.Instance.OnConnectionEstablished += onConnected;
    }

    private void HandleMatchAborted(GameSceneType targetScene)
    {
        currentSession = null;
        isFlowInitialized = false;
        
        NetworkSessionManager.Instance.ResetNetworkSession();
        
        if (targetScene == GameSceneType.OnlineMatching)
        {
            currentMode = ConnectionMode.OnlineClient;
        }
        
        ChangeScene(targetScene);
    }

    private void DrawStatusGUI()
    {
        float debugWidth = 300f;
        float debugHeight = 30f;
        
        string modeStr = currentMode.ToString();
        bool isConnected = currentSession != null && NetworkSessionManager.Instance.GetIsConnected();
        
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
}