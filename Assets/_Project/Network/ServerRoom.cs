using Unity.Networking.Transport;
using System.Collections.Generic;

public class ServerRoom
{
    public string roomCode;
    public string roomTitle;
    public bool isPrivate;
    public string password;

    public RoomStateType currentState;

    public NetworkConnection p1;
    public NetworkConnection p2;
    public int p1Ping = 0;
    public int p2Ping = 0;
    public RoomStateModel stateModel;
    
    public bool isCountdownStarted;
    public bool isCountdownFinished;
    public float countdownTimer;
    public bool isP1StartRequested;
    public bool isP2StartRequested;

    public bool isP1RoundReported;
    public bool isP2RoundReported;

    public int p1RoundWins;
    public int p2RoundWins;

    public int p1ReportedWinner;
    public int p2ReportedWinner;
    
    public int p1ReportedP1Wins;
    public int p1ReportedP2Wins;
    public int p2ReportedP1Wins;
    public int p2ReportedP2Wins;

    public bool isVotingStarted;
    public float votingTimer;
    public bool hasP1Voted;
    public bool hasP2Voted;

    public MatchEndActionType p1VoteAction;
    public MatchEndActionType p2VoteAction;

    public List<string> roomLogs;
    private const int MAX_ROOM_LOGS = 8;

    public ServerRoom(string code, string title, bool isPriv, string pwd)
    {
        roomCode = code;
        roomTitle = title;
        isPrivate = isPriv;
        password = pwd;

        currentState = RoomStateType.Lobby;

        p1 = default;
        p2 = default;
        stateModel = new RoomStateModel();
        stateModel.isStageLocked = true;
        
        isCountdownStarted = false;
        isCountdownFinished = false;
        countdownTimer = 3f;
        isP1StartRequested = false;
        isP2StartRequested = false;

        isP1RoundReported = false;
        isP2RoundReported = false;

        p1RoundWins = 0;
        p2RoundWins = 0;

        p1ReportedWinner = -1;
        p2ReportedWinner = -1;
        
        p1ReportedP1Wins = 0;
        p1ReportedP2Wins = 0;
        p2ReportedP1Wins = 0;
        p2ReportedP2Wins = 0;

        hasP1Voted = false;
        hasP2Voted = false;
        isVotingStarted = false;
        votingTimer = 15f;

        roomLogs = new List<string>();
    }

    public void LogRoomEvent(string msg)
    {
        roomLogs.Add(msg);
        if (roomLogs.Count > MAX_ROOM_LOGS)
        {
            roomLogs.RemoveAt(0);
        }
    }

    public bool IsFull() => p1.IsCreated && p2.IsCreated;
    public bool IsEmpty() => !p1.IsCreated && !p2.IsCreated;
    public bool HasPassword() => !string.IsNullOrEmpty(password);
}