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

    public void HandleInputs(int p1Move, bool p1Select, int p2Move, bool p2Select)
    {
        StagePlayerContext localContext = (localPlayerSide == 0) ? manager.p1Context : manager.p2Context;
        int move = (localPlayerSide == 0) ? p1Move : p2Move;
        bool select = (localPlayerSide == 0) ? p1Select : p2Select;

        if (move != 0 && !localContext.isLocked)
        {
            manager.MoveCursor(localContext, move);
            UpdateBackground();
        }

        if (select)
        {
            if (!localContext.isLocked)
            {
                manager.LockSelection(localContext);
                EvaluateSceneTransition();
            }
            else
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