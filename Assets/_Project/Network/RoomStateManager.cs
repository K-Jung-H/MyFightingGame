using UnityEngine;
using System;

public enum RoomStateType : byte
{
    None = 0,
    Lobby = 1,
    CharacterSelect = 2,
    StageSelect = 3,
    InGame = 4
}

public class RoomStateManager : MonoBehaviour
{
    public static RoomStateManager Instance { get; private set; }

    public RoomStateModel roomModel { get; private set; }
    
    public event Action<int, bool, int, int, bool, int> OnCharacterSelectUpdated;
    public event Action<bool> OnCountdownUpdated;
    public event Action OnStageSelectTransitionAvailable;
    public event Action<int, bool, int, bool> OnStageSelectUpdated;
    public event Action<int> OnStageRouletteStarted;

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
            ServerNetworkManager.Instance.OnTransitionAvailableToStageSelectReceived += HandleStageSelectTransitionAvailable;
            ServerNetworkManager.Instance.OnSceneChangeReceived += HandleSceneChangeCommand;
            ServerNetworkManager.Instance.OnRoomStateBroadcastReceived += HandleRoomStateBroadcast;
            ServerNetworkManager.Instance.OnStageSelectBroadcastReceived += HandleStageSelectBroadcast;
            ServerNetworkManager.Instance.OnStageRouletteStartReceived += HandleStageRouletteStart;
        }
    }

    private void OnDestroy()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnSlotAssignedReceived -= HandleSlotAssigned;
            ServerNetworkManager.Instance.OnSelectBroadcastReceived -= HandleSelectBroadcast;
            ServerNetworkManager.Instance.OnCountdownUpdateReceived -= HandleCountdownUpdate;
            ServerNetworkManager.Instance.OnTransitionAvailableToStageSelectReceived -= HandleStageSelectTransitionAvailable;
            ServerNetworkManager.Instance.OnSceneChangeReceived -= HandleSceneChangeCommand;
            ServerNetworkManager.Instance.OnRoomStateBroadcastReceived -= HandleRoomStateBroadcast;
            ServerNetworkManager.Instance.OnStageSelectBroadcastReceived -= HandleStageSelectBroadcast;
            ServerNetworkManager.Instance.OnStageRouletteStartReceived -= HandleStageRouletteStart;
        }

        if (Instance == this)
        {
            Instance = null;
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
        else if (currentRoomState == RoomStateType.StageSelect)
        {
            GameFlowManager.Instance.ChangeScene(GameSceneType.StageSelect);
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
        roomModel.isP1Connected = newModel.isP1Connected;
        roomModel.isP2Connected = newModel.isP2Connected;
        roomModel.maxRounds = newModel.maxRounds;
        roomModel.roundTimeLimit = newModel.roundTimeLimit;
        roomModel.p1Wins = newModel.p1Wins;
        roomModel.p1Losses = newModel.p1Losses;
        roomModel.p2Wins = newModel.p2Wins;
        roomModel.p2Losses = newModel.p2Losses;
        roomModel.isP1Ready = newModel.isP1Ready;
        roomModel.isP2Ready = newModel.isP2Ready;
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

    private void HandleStageSelectTransitionAvailable()
    {
        if (currentRoomState == RoomStateType.CharacterSelect)
        {
            OnStageSelectTransitionAvailable?.Invoke();
        }
    }


    private void HandleSceneChangeCommand(GameSceneType targetScene)
    {
        if (targetScene == GameSceneType.OnlineMatchedRoom)
        {
            ResetLocalSelectionState();
            ChangeRoomState(RoomStateType.Lobby);
        }
        else if (targetScene == GameSceneType.CharacterSelect)
        {
            ResetLocalSelectionState();
            ChangeRoomState(RoomStateType.CharacterSelect);
        }
        else if (targetScene == GameSceneType.StageSelect)
        {
            ChangeRoomState(RoomStateType.StageSelect);
        }
        else if (targetScene == GameSceneType.GamePlay)
        {
            ChangeRoomState(RoomStateType.InGame);
        }
    }


    private void ResetLocalSelectionState()
    {
        roomModel.p1CharacterIndex = 0;
        roomModel.p2CharacterIndex = 0;
        roomModel.isP1CharacterLocked = false;
        roomModel.isP2CharacterLocked = false;

        roomModel.p1StageIndex = 0;
        roomModel.p2StageIndex = 0;
        roomModel.isP1StageLocked = false;
        roomModel.isP2StageLocked = false;
        roomModel.selectedStageIndex = 0;
    }

    private void HandleStageSelectBroadcast(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock)
    {
        roomModel.p1StageIndex = p1Idx;
        roomModel.isP1StageLocked = p1Lock;
        roomModel.p2StageIndex = p2Idx;
        roomModel.isP2StageLocked = p2Lock;

        if (currentRoomState == RoomStateType.StageSelect)
        {
            OnStageSelectUpdated?.Invoke(p1Idx, p1Lock, p2Idx, p2Lock);
        }
    }

    private void HandleStageRouletteStart(int finalIndex)
    {
        roomModel.selectedStageIndex = finalIndex;

        if (currentRoomState == RoomStateType.StageSelect)
        {
            OnStageRouletteStarted?.Invoke(finalIndex);
        }
    }
}