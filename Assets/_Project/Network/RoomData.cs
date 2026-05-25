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

[System.Serializable]
public class RoomStateModel
{
    public bool isP1Connected;
    public bool isP2Connected;

    public int p1CharacterIndex = 0;
    public int p2CharacterIndex = 0;
    public int selectedStageIndex = 0;
    public bool isP1CharacterLocked;
    public bool isP2CharacterLocked;
    public bool isStageLocked;

    public int p1PreferredSide = 0;
    public int p2PreferredSide = 1;

    public bool isP1Ready;
    public bool isP2Ready;

    public int maxRounds = 3;
    public int roundTimeLimit = 60;

    public int p1Wins = 0;
    public int p1Losses = 0;
    public int p2Wins = 0;
    public int p2Losses = 0;

    public int p1StageIndex = 0;
    public int p2StageIndex = 0;
    public bool isP1StageLocked = false;
    public bool isP2StageLocked = false;


    public RoomStateModel()
    {
        isP1Connected = true; 
        isP2Connected = false;
        isP1CharacterLocked = false;
        isP2CharacterLocked = false;
        isStageLocked = false;
        isP1Ready = false;
        isP2Ready = false;
        p1StageIndex = 0;
        p2StageIndex = 0;
        isP1StageLocked = false;
        isP2StageLocked = false;
    }

    public bool IsAllCharacterSelected()
    {
        return isP1CharacterLocked && isP2CharacterLocked;
    }

    public bool IsAllStageSelected()
    {
        return isP1StageLocked && isP2StageLocked;
    }
}