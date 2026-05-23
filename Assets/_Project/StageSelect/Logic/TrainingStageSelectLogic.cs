using UnityEngine;

public class TrainingStageSelectLogic : IStageSelectLogic
{
    private StageSelectManager manager;
    private int localPlayerSide = 0;

    public void Initialize(StageSelectManager manager)
    {
        this.manager = manager;
        localPlayerSide = MatchDataManager.TrainingLocalPlayerSide;
    }

    public void ProcessInput()
    {
        StagePlayerContext localContext = (localPlayerSide == 0) ? manager.p1Context : manager.p2Context;

        if (!localContext.isLocked)
        {
            int moveInput = manager.GetMovementInput(localContext);
            if (moveInput != 0)
            {
                manager.MoveCursor(localContext, moveInput);
                UpdateBackground();
            }

            if (manager.GetOfflineLockInput(localContext))
            {
                manager.LockSelection(localContext);
                EvaluateSceneTransition();
            }
        }
        else
        {
            if (manager.GetOfflineUnlockInput(localContext))
            {
                manager.UnlockSelection(localContext);
            }
        }
    }

    public void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock) { }

    public void UpdateBackground()
    {
        if (manager.stageRoster == null || manager.canvasBackgroundImage == null) return;
        
        StagePlayerContext localContext = (localPlayerSide == 0) ? manager.p1Context : manager.p2Context;
        int targetIndex = localContext.currentIndex;

        if (targetIndex >= 0 && targetIndex < manager.stageRoster.Length)
        {
            manager.canvasBackgroundImage.sprite = manager.stageRoster[targetIndex].thumbnail;
        }
    }

    public void EvaluateSceneTransition()
    {
        StagePlayerContext localContext = (localPlayerSide == 0) ? manager.p1Context : manager.p2Context;

        if (localContext.isLocked)
        {
            MatchDataManager.SelectedStageData = manager.stageRoster[localContext.currentIndex];
            GameFlowManager.Instance.ChangeScene(GameSceneType.GamePlay);
        }
    }

    public bool IsPlayerActive(int playerId)
    {
        if (playerId == 1) return localPlayerSide == 0;
        if (playerId == 2) return localPlayerSide == 1;
        return false;
    }
}