using UnityEngine;
using System;

public enum RoomStateType : byte
{
    None = 0,
    Lobby = 1,
    CharacterSelect = 2,
    InGame = 3
}

public class RoomStateManager : MonoBehaviour
{
    public static RoomStateManager Instance { get; private set; }

    public RoomStateModel roomModel { get; private set; }
    
    public event Action<int, bool, int, int, bool, int> OnCharacterSelectUpdated;
    public event Action<bool> OnCountdownUpdated;
    public event Action OnStartButtonActivated;

    private RoomStateType currentRoomState;
    private string targetPeerIpAddress;
    private int localPlayerSlot = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            roomModel = new RoomStateModel();
            targetPeerIpAddress = "127.0.0.1";
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnSlotAssignedReceived += HandleSlotAssigned;
            ServerNetworkManager.Instance.OnSelectBroadcastReceived += HandleSelectBroadcast;
            ServerNetworkManager.Instance.OnCountdownUpdateReceived += HandleCountdownUpdate;
            ServerNetworkManager.Instance.OnStartButtonActiveReceived += HandleStartButtonActive;
            ServerNetworkManager.Instance.OnSceneChangeReceived += HandleSceneChangeCommand;
            ServerNetworkManager.Instance.OnRoomStateBroadcastReceived += HandleRoomStateBroadcast;
        }
    }

    private void OnDestroy()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnSlotAssignedReceived -= HandleSlotAssigned;
            ServerNetworkManager.Instance.OnSelectBroadcastReceived -= HandleSelectBroadcast;
            ServerNetworkManager.Instance.OnCountdownUpdateReceived -= HandleCountdownUpdate;
            ServerNetworkManager.Instance.OnStartButtonActiveReceived -= HandleStartButtonActive;
            ServerNetworkManager.Instance.OnSceneChangeReceived -= HandleSceneChangeCommand;
            ServerNetworkManager.Instance.OnRoomStateBroadcastReceived -= HandleRoomStateBroadcast;
        }
    }

    public RoomStateType GetCurrentState()
    {
        return currentRoomState;
    }

    public string GetTargetPeerIpAddress()
    {
        return targetPeerIpAddress;
    }

    public int GetLocalPlayerSlot()
    {
        return localPlayerSlot;
    }

    public void ChangeRoomState(RoomStateType newState)
    {
        currentRoomState = newState;

        if (currentRoomState == RoomStateType.Lobby)
        {
            GameFlowManager.Instance.ChangeScene(GameSceneType.OnlineMatchedRoom);
        }
        else if (currentRoomState == RoomStateType.CharacterSelect)
        {
            GameFlowManager.Instance.ChangeScene(GameSceneType.CharacterSelect);
        }
        else if (currentRoomState == RoomStateType.InGame)
        {
            GameFlowManager.Instance.ChangeScene(GameSceneType.GamePlay);
        }
    }

    private void HandleSlotAssigned(int slotId)
    {
        localPlayerSlot = slotId;
    }

    public void UpdateRoomModel(RoomStateModel newModel)
    {
        roomModel = newModel;
    }
    private void HandleRoomStateBroadcast(RoomStateModel newModel)
    {
        UpdateRoomModel(newModel);
    }

    private void HandleSelectBroadcast(int p1Idx, bool p1Lock, int p1Side, int p2Idx, bool p2Lock, int p2Side)
    {
        roomModel.p1CharacterIndex = p1Idx;
        roomModel.isP1CharacterLocked = p1Lock;
        roomModel.p1PreferredSide = p1Side;
        roomModel.p2CharacterIndex = p2Idx;
        roomModel.isP2CharacterLocked = p2Lock;
        roomModel.p2PreferredSide = p2Side;

        if (currentRoomState == RoomStateType.CharacterSelect)
        {
            OnCharacterSelectUpdated?.Invoke(p1Idx, p1Lock, p1Side, p2Idx, p2Lock, p2Side);
        }
    }

    private void HandleCountdownUpdate(bool isStarted)
    {
        if (currentRoomState == RoomStateType.CharacterSelect)
        {
            OnCountdownUpdated?.Invoke(isStarted);
        }
    }

    private void HandleStartButtonActive()
    {
        if (currentRoomState == RoomStateType.CharacterSelect)
        {
            OnStartButtonActivated?.Invoke();
        }
    }

    private void HandleSceneChangeCommand()
    {
        ChangeRoomState(RoomStateType.InGame);
    }
}