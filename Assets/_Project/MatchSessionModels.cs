using System;


public struct RoomMetadata
{
    public string RoomCode;
    public string RoomTitle;
    public byte PlayerCount;
    public bool HasPassword;
}

public class RoomStateModel
{
    public int p1CharacterIndex;
    public int p2CharacterIndex;
    public int selectedStageIndex;
    public bool isP1CharacterLocked;
    public bool isP2CharacterLocked;
    public bool isStageLocked;

    public int p1PreferredSide = 0;
    public int p2PreferredSide = 1;

    public bool IsAllReadyToStart()
    {
        return isP1CharacterLocked && isP2CharacterLocked && isStageLocked;
    }
}

public interface IMatchSession
{
    RoomStateModel GetRoomState();
    int GetLocalPlayerSlot();
    void UpdateCharacterSelect(int playerId, int characterIndex, bool isLocked);
    void UpdateStageSelect(int stageIndex, bool isLocked);
    void SendSideUpdate(int side);
    void SyncRemoteState(RoomStateModel remoteState);
    void SendStartRequest(int playerId);
    void UpdateSession(float deltaTime);

    event Action<bool> OnCountdownUpdate;
    event Action OnStartButtonActive;
    event Action OnSceneChange;
}

public class OfflineMatchSession : IMatchSession
{
    private RoomStateModel roomState;
    private bool isCountdownActive;
    private float countdownTimer;
    private bool isStartButtonReady;

    public event Action<bool> OnCountdownUpdate;
    public event Action OnStartButtonActive;
    public event Action OnSceneChange;

    public OfflineMatchSession()
    {
        roomState = new RoomStateModel();
        roomState.isStageLocked = true; 
    }

    public RoomStateModel GetRoomState()
    {
        return roomState;
    }

    public int GetLocalPlayerSlot()
    {
        return 0;
    }

    public void UpdateCharacterSelect(int playerId, int characterIndex, bool isLocked)
    {
        if (playerId == 1)
        {
            roomState.p1CharacterIndex = characterIndex;
            roomState.isP1CharacterLocked = isLocked;
        }
        else if (playerId == 2)
        {
            roomState.p2CharacterIndex = characterIndex;
            roomState.isP2CharacterLocked = isLocked;
        }

        EvaluateRoomState();
    }

    public void UpdateStageSelect(int stageIndex, bool isLocked)
    {
        roomState.selectedStageIndex = stageIndex;
        roomState.isStageLocked = isLocked;
        EvaluateRoomState();
    }

    public void SendSideUpdate(int side)
    {
        roomState.p1PreferredSide = side;
    }

    public void SyncRemoteState(RoomStateModel remoteState)
    {
    }

    public void SendStartRequest(int playerId)
    {
        if (!isStartButtonReady) return;

        OnSceneChange?.Invoke();
    }

    public void UpdateSession(float deltaTime)
    {
        if (isCountdownActive)
        {
            countdownTimer -= deltaTime;
            if (countdownTimer <= 0f)
            {
                isCountdownActive = false;
                isStartButtonReady = true;
                OnStartButtonActive?.Invoke();
            }
        }
    }

    private void EvaluateRoomState()
    {
        if (roomState.IsAllReadyToStart())
        {
            if (!isCountdownActive && !isStartButtonReady)
            {
                isCountdownActive = true;
                countdownTimer = 3f;
                OnCountdownUpdate?.Invoke(true);
            }
        }
        else
        {
            isCountdownActive = false;
            isStartButtonReady = false;
            OnCountdownUpdate?.Invoke(false);
        }
    }
}

public class OnlineClientSession : IMatchSession
{
    private RoomStateModel roomState;
    private int localPlayerSlot = -1;

    public event Action<bool> OnCountdownUpdate;
    public event Action OnStartButtonActive;
    public event Action OnSceneChange;

    public OnlineClientSession()
    {
        roomState = new RoomStateModel();

        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnCountdownUpdateReceived += HandleNetworkCountdown;
            NetworkSessionManager.Instance.OnStartButtonActiveReceived += HandleNetworkStartActive;
            NetworkSessionManager.Instance.OnSceneChangeReceived += HandleNetworkSceneChange;
            NetworkSessionManager.Instance.OnSlotAssignedReceived += HandleSlotAssigned;
            NetworkSessionManager.Instance.OnSelectBroadcastReceived += HandleNetworkSelectBroadcast;
        }
    }

    public RoomStateModel GetRoomState()
    {
        return roomState;
    }

    public int GetLocalPlayerSlot()
    {
        return localPlayerSlot;
    }

    public void UpdateCharacterSelect(int playerId, int characterIndex, bool isLocked)
    {
        NetworkSessionManager.Instance.SendSelectUpdate(playerId, characterIndex, isLocked);
    }

    public void UpdateStageSelect(int stageIndex, bool isLocked)
    {
    }

    public void SendSideUpdate(int side)
    {
        NetworkSessionManager.Instance.SendSideUpdate(side);
    }

    public void SyncRemoteState(RoomStateModel remoteState)
    {
        roomState = remoteState;
    }

    public void SendStartRequest(int playerId)
    {
        NetworkSessionManager.Instance.SendStartRequest();
    }

    public void UpdateSession(float deltaTime)
    {
    }

    private void HandleNetworkCountdown(bool isStarted)
    {
        OnCountdownUpdate?.Invoke(isStarted);
    }

    private void HandleNetworkStartActive()
    {
        OnStartButtonActive?.Invoke();
    }

    private void HandleNetworkSceneChange()
    {
        OnSceneChange?.Invoke();
    }

    private void HandleSlotAssigned(int slotId)
    {
        localPlayerSlot = slotId;
    }

    private void HandleNetworkSelectBroadcast(int p1Idx, bool p1Lock, int p1Side, int p2Idx, bool p2Lock, int p2Side)
    {
        roomState.p1CharacterIndex = p1Idx;
        roomState.isP1CharacterLocked = p1Lock;
        roomState.p1PreferredSide = p1Side;
        roomState.p2CharacterIndex = p2Idx;
        roomState.isP2CharacterLocked = p2Lock;
        roomState.p2PreferredSide = p2Side;
    }
}