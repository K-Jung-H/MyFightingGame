using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public enum ConnectionMode
{
    None,
    Offline,
    OnlineClient,
    DedicatedServer
}

public enum GameSceneType
{
    Start,
    GameModeSelect,
    Training,
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
    public GameSceneType previousScene = GameSceneType.Start;

    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private string gameModeSelectSceneName = "GameModeSelectScene";
    [SerializeField] private string onlineMatchingSceneName = "OnlineMatchingScene";
    [SerializeField] private string onlineMatchedRoomSceneName = "OnlineMatchedRoomScene";
    [SerializeField] private string characterSelectSceneName = "CharacterSelectScene";
    [SerializeField] private string gamePlaySceneName = "GamePlayScene";
    [SerializeField] private string serverSceneName = "EmptyServerScene";

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
        bool isOnlineMode = (currentMode == ConnectionMode.OnlineClient);
        if (isFlowInitialized && isOnlineMode)
        {
            DrawNetworkStatusGUI();
            if (currentScene == GameSceneType.Server) return;
        }
    }

    public Vector2 GetReferenceResolution()
    {
        return referenceResolution;
    }

    public void ChangeScene(GameSceneType targetSceneType)
    {
        previousScene = currentScene;
        currentScene = targetSceneType;

        if (currentScene == GameSceneType.Start) SceneManager.LoadScene(startSceneName);
        else if (currentScene == GameSceneType.GameModeSelect) SceneManager.LoadScene(gameModeSelectSceneName);
        else if (currentScene == GameSceneType.OnlineMatching) SceneManager.LoadScene(onlineMatchingSceneName);
        else if (currentScene == GameSceneType.OnlineMatchedRoom) SceneManager.LoadScene(onlineMatchedRoomSceneName);
        else if (currentScene == GameSceneType.CharacterSelect) SceneManager.LoadScene(characterSelectSceneName);
        else if (currentScene == GameSceneType.GamePlay) SceneManager.LoadScene(gamePlaySceneName);
        else if (currentScene == GameSceneType.Server) SceneManager.LoadScene(serverSceneName);
    }

    public void StartDedicatedServer()
    {
        currentMode = ConnectionMode.DedicatedServer;
        isFlowInitialized = true;
            
        dummyServer = gameObject.AddComponent<DummyMatchServer>();
        dummyServer.StartServer();
        ChangeScene(GameSceneType.Server);
    }

    public void SelectTrainingMode()
    {
        Debug.Log("Training Mode Selected");
    }

    public void SelectOfflineMode()
    {
        currentMode = ConnectionMode.Offline;
        isFlowInitialized = true;
        ChangeScene(GameSceneType.CharacterSelect);
    }

    public void SelectOnlineMode()
    {
        currentMode = ConnectionMode.OnlineClient;

        if (ServerNetworkManager.Instance == null)
        {
            GameObject serverObj = new GameObject("ServerNetworkManager");
            serverObj.AddComponent<ServerNetworkManager>();
        }

        if (RoomStateManager.Instance == null)
        {
            GameObject roomObj = new GameObject("RoomStateManager");
            roomObj.AddComponent<RoomStateManager>();
        }

        ChangeScene(GameSceneType.OnlineMatching);
    }

    public void GoBack()
    {
        if (currentScene != previousScene)
        {
            if (currentMode != ConnectionMode.None && previousScene == GameSceneType.GameModeSelect)
            {
                currentMode = ConnectionMode.None;
                isFlowInitialized = false;

                if (ServerNetworkManager.Instance != null)
                {
                    Destroy(ServerNetworkManager.Instance.gameObject);
                }

                if (RoomStateManager.Instance != null)
                {
                    Destroy(RoomStateManager.Instance.gameObject);
                }
            }

            ChangeScene(previousScene);
        }
    }
    
    private void HandleMatchAborted(GameSceneType targetScene)
    {
        isFlowInitialized = false;
        
        if (targetScene == GameSceneType.Start || targetScene == GameSceneType.GameModeSelect)
        {
            currentMode = ConnectionMode.None;
            
            if (ServerNetworkManager.Instance != null) Destroy(ServerNetworkManager.Instance.gameObject);
            if (RoomStateManager.Instance != null) Destroy(RoomStateManager.Instance.gameObject);
        }
        else if (targetScene == GameSceneType.OnlineMatching)
        {
            currentMode = ConnectionMode.OnlineClient;
        }
        
        ChangeScene(targetScene);
    }

    private void DrawNetworkStatusGUI()
    {
        float boxWidth = 420f;
        float boxHeight = 60f;
        float startX = 10f;
        float startY = 360f;
        
        string modeStr = currentMode.ToString();
        string netStatus = (currentMode == ConnectionMode.DedicatedServer) ? "[Dedicated Server Running]" : "[Online Mode]";

        string displayText = $"{modeStr} | {netStatus}";
        
        GUIStyle customLabelStyle = new GUIStyle(GUI.skin.label);
        customLabelStyle.fontSize = 18;
        customLabelStyle.alignment = TextAnchor.MiddleLeft;

        GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "");
        GUI.Label(new Rect(startX + 10f, startY + 5f, boxWidth - 20f, boxHeight - 10f), displayText, customLabelStyle);
    }
}