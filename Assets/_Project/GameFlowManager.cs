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

public enum BattleType
{
    None,
    Training,
    OfflineBattle,
    OnlineBattle
}

public enum GameSceneType
{
    Start,
    GameModeSelect,
    OnlineLobby,
    OnlineMatchedRoom,
    CharacterSelect,
    GamePlay,
    Server
}

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public ConnectionMode currentConnectionMode = ConnectionMode.None;
    public BattleType currentBattleType = BattleType.None;
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
        currentConnectionMode = ConnectionMode.DedicatedServer;            
        currentBattleType = BattleType.None;
        dummyServer = gameObject.AddComponent<DummyMatchServer>();
        dummyServer.StartServer();
        ChangeScene(GameSceneType.Server);
    }

    public void SelectTrainingMode()
    {
        currentConnectionMode = ConnectionMode.Offline;
        currentBattleType = BattleType.Training;
        ChangeScene(GameSceneType.CharacterSelect);
    }

    public void SelectOfflineMode()
    {
        currentConnectionMode = ConnectionMode.Offline;
        currentBattleType = BattleType.OfflineBattle;
        ChangeScene(GameSceneType.CharacterSelect);
    }

    public void SelectOnlineMode()
    {
        currentConnectionMode = ConnectionMode.OnlineClient;
        currentBattleType = BattleType.OnlineBattle;
        ChangeScene(GameSceneType.OnlineLobby);
    }

    public void GoBack()
    {
        if (currentConnectionMode == ConnectionMode.OnlineClient)
        {
            switch (currentScene)
            {
                case GameSceneType.OnlineLobby:
                    currentConnectionMode = ConnectionMode.None;
                    currentBattleType = BattleType.None;
                    
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
            case GameSceneType.CharacterSelect:
                ChangeScene(GameSceneType.GameModeSelect);
                break;
        }
    }
}