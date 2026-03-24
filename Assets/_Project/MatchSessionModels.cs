using System;

public class RoomStateModel
{
    public int p1CharacterIndex;
    public int p2CharacterIndex;
    public int selectedStageIndex;
    public bool isP1CharacterLocked;
    public bool isP2CharacterLocked;
    public bool isStageLocked;

    public bool IsAllReadyToStart()
    {
        return isP1CharacterLocked && isP2CharacterLocked && isStageLocked;
    }
}

public interface IMatchSession
{
    RoomStateModel GetRoomState();
    void UpdateCharacterSelect(int playerId, int characterIndex, bool isLocked);
    void UpdateStageSelect(int stageIndex, bool isLocked);
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
        }
    }

    public RoomStateModel GetRoomState()
    {
        return roomState;
    }

    public void UpdateCharacterSelect(int playerId, int characterIndex, bool isLocked)
    {
        NetworkSessionManager.Instance.SendSelectUpdate(playerId, characterIndex, isLocked);
    }

    public void UpdateStageSelect(int stageIndex, bool isLocked)
    {
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
}