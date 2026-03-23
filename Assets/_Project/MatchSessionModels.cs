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
}

public class OfflineMatchSession : IMatchSession
{
    private RoomStateModel roomState;

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

    private void EvaluateRoomState()
    {
        if (roomState.IsAllReadyToStart())
        {
            GameFlowManager.Instance.OnReceiveSceneChangeCommand("MainScene");
        }
    }
}

public class OnlineClientSession : IMatchSession
{
    private RoomStateModel roomState;

    public OnlineClientSession()
    {
        roomState = new RoomStateModel();
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
}