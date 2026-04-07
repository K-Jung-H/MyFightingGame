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
    OnlineLobby,
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

    private DummyMatchServer dummyServer;

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

    public void ChangeScene(GameSceneType targetSceneType)
    {
        currentScene = targetSceneType;

        if (currentScene == GameSceneType.Start) SceneManager.LoadScene(startSceneName);
        else if (currentScene == GameSceneType.GameModeSelect) SceneManager.LoadScene(gameModeSelectSceneName);
        else if (currentScene == GameSceneType.OnlineLobby) SceneManager.LoadScene(onlineMatchingSceneName);
        else if (currentScene == GameSceneType.OnlineMatchedRoom) SceneManager.LoadScene(onlineMatchedRoomSceneName);
        else if (currentScene == GameSceneType.CharacterSelect) SceneManager.LoadScene(characterSelectSceneName);
        else if (currentScene == GameSceneType.GamePlay) SceneManager.LoadScene(gamePlaySceneName);
        else if (currentScene == GameSceneType.Server) SceneManager.LoadScene(serverSceneName);
    }

    public void StartDedicatedServer()
    {
        currentMode = ConnectionMode.DedicatedServer;            
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
        ChangeScene(GameSceneType.CharacterSelect);
    }

    public void SelectOnlineMode()
    {
        currentMode = ConnectionMode.OnlineClient;
        ChangeScene(GameSceneType.OnlineLobby);
    }

    public void GoBack()
    {
        if (currentMode == ConnectionMode.OnlineClient)
        {
            switch (currentScene)
            {
                case GameSceneType.OnlineLobby:
                    currentMode = ConnectionMode.None;
                    
                    if (ServerNetworkManager.Instance != null) 
                    {
                        Destroy(ServerNetworkManager.Instance.gameObject);
                    }
                    
                    ChangeScene(GameSceneType.GameModeSelect);
                    break;
                case GameSceneType.OnlineMatchedRoom:
                    if (ServerNetworkManager.Instance != null) ServerNetworkManager.Instance.SendRoomLeaveRequest();
                    break;
                case GameSceneType.CharacterSelect:
                    if (ServerNetworkManager.Instance != null) ServerNetworkManager.Instance.SendCancelPhaseRequest();
                    break;
            }
            return;
        }

        switch (currentScene)
        {
            case GameSceneType.GameModeSelect:
                ChangeScene(GameSceneType.Start);
                break;
            case GameSceneType.Training:
            case GameSceneType.CharacterSelect:
                ChangeScene(GameSceneType.GameModeSelect);
                break;
        }
    }
}