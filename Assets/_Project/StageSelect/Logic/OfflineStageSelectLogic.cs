using UnityEngine;

public class OfflineStageSelectLogic : IStageSelectLogic
{
    private StageSelectManager manager;
    private int lastChangedPlayerId = 1;

    public void Initialize(StageSelectManager manager)
    {
        this.manager = manager;
    }

    public void ProcessInput()
    {
        UpdatePlayerInput(manager.p1Context, 1);
        UpdatePlayerInput(manager.p2Context, 2);

        EvaluateSceneTransition();
    }

    private void UpdatePlayerInput(StagePlayerContext context, int playerId)
    {
        if (!context.isLocked)
        {
            int moveInput = manager.GetMovementInput(context);
            if (moveInput != 0)
            {
                manager.MoveCursor(context, moveInput);
                lastChangedPlayerId = playerId;
                UpdateBackground();
            }

            if (manager.GetOfflineLockInput(context))
            {
                manager.LockSelection(context);
            }
        }
        else
        {
            if (manager.GetOfflineUnlockInput(context))
            {
                manager.UnlockSelection(context);
            }
        }
    }

    public void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock) { }

    public void UpdateBackground()
    {
        if (manager.stageRoster == null || manager.canvasBackgroundImage == null) return;

        int targetIndex = (lastChangedPlayerId == 1) ? manager.p1Context.currentIndex : manager.p2Context.currentIndex;
        
        if (targetIndex >= 0 && targetIndex < manager.stageRoster.Length)
        {
            manager.canvasBackgroundImage.sprite = manager.stageRoster[targetIndex].thumbnail;
        }
    }

    public void EvaluateSceneTransition()
    {
        if (manager.p1Context.isLocked && manager.p2Context.isLocked)
        {
            if (manager.p1Context.currentIndex == manager.p2Context.currentIndex)
            {
                if (!manager.isLocalCountdownActive)
                {
                    manager.StartCountdown();
                }
            }
            else
            {
                manager.StopCountdown();
            }
        }
        else
        {
            manager.StopCountdown();
        }
    }

    public bool IsPlayerActive(int playerId)
    {
        return true;
    }
}