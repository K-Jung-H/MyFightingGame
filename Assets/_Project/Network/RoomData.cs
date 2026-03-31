[System.Serializable]
public struct RoomCreateData
{
    public string RoomName;
    public bool IsPrivate;
    public bool UsePassword;
    public string Password;
}

[System.Serializable]
public class RoomMetadata
{
    public string RoomCode;
    public string RoomTitle;
    public int PlayerCount;
    public int MaxPlayerCount;
    public bool IsPrivate;
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