using System;
using System.Threading.Tasks;

public class MatchedRoomManager
{
    private bool p1Locked;
    private bool p2Locked;
    private bool isMatchStarting;

    public bool P1Locked => p1Locked;
    public bool P2Locked => p2Locked;

    public event Action OnMatchStartServerCommand;
    public static event Action OnMatchStartCommand;

    public void Initialize()
    {
        p1Locked = false;
        p2Locked = false;
        isMatchStarting = false;
    }

    public void UpdatePlayerLockState(int playerIndex, bool isLocked)
    {
        if (playerIndex == 1) p1Locked = isLocked;
        else p2Locked = isLocked;

        if (p1Locked && p2Locked && !isMatchStarting)
        {
            isMatchStarting = true;

            if (GameFlowManager.Instance.currentMode == ConnectionMode.Offline)
            {
                _ = MatchStartRoutineAsync();
            }
            else
            {
                SendMatchStartPacket();
            }
        }
    }

    private void SendMatchStartPacket()
    {
        OnMatchStartServerCommand?.Invoke();
    }

    private async Task MatchStartRoutineAsync()
    {
        await Task.Delay(1000);
        OnMatchStartCommand?.Invoke();
    }
}