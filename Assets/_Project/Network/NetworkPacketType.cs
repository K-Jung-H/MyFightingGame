public static class NetworkPacketType
{
    public const byte Input = 0;
    public const byte Hash = 1;

    public const byte SelectUpdate = 10;
    public const byte SelectBroadcast = 11;
    public const byte SceneChange = 12;
    public const byte Handshake = 13;
    public const byte GameStart = 14;
    public const byte CountdownUpdate = 15;
    public const byte StartButtonActive = 16;
    public const byte StartRequest = 17;
    public const byte AssignSlot = 18;
    public const byte SideUpdate = 19;

    public const byte P2PPing = 20;
    public const byte P2PPong = 21;

    public const byte ServerPing = 22;
    public const byte ServerPong = 23;

    public const byte ReportDisconnect = 24;
    public const byte MatchAborted = 25;

    public const byte CreateRoomRequest = 30;
    public const byte SearchRoomRequest = 31;
    public const byte SearchRoomResponse = 32;
    public const byte JoinRoomRequest = 33;
    public const byte JoinRoomResponse = 34;
    
    public const byte RuleUpdate = 35;
    public const byte RoomLeaveRequest = 36;
    public const byte ReadyStateUpdate = 37;
    public const byte RoomStateBroadcast = 38;
    public const byte LobbyStartRequest = 39;
    
    public const byte RoundEndReport = 40;
    public const byte RoundVerified = 41;
    public const byte MatchEndActionRequest = 42;
    public const byte RematchSyncBroadcast = 43;

    public const byte CancelPhaseRequest = 44;
    public const byte RandomMatchRequest = 45;

    public const byte ChatMessage = 50;
}