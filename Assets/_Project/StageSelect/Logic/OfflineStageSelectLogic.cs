using UnityEngine;
using System.Collections.Generic;

public class OfflineStageSelectLogic : IStageSelectLogic
{
    private StageSelectManager manager;
    private int lastChangedPlayerId = 1;

    public void Initialize(StageSelectManager manager)
    {
        this.manager = manager;
    }

    public void Cleanup() {}

    public void HandleInputs(int p1Move, bool p1Select, int p2Move, bool p2Select)
    {
        UpdatePlayerInput(manager.p1Context, 1, p1Move);
        UpdatePlayerInput(manager.p2Context, 2, p2Move);

        EvaluateSceneTransition();
    }

    private void UpdatePlayerInput(StagePlayerContext context, int playerId, int move)
    {
        if (!context.isLocked)
        {
            if (move != 0) 
            { 
                manager.MoveCursor(context, move); lastChangedPlayerId = playerId; UpdateBackground(); 
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
        if (manager.p1Context.isLocked && manager.p2Context.isLocked && !manager.isRouletteActive)
        {
            int p1Idx = manager.p1Context.currentIndex;
            int p2Idx = manager.p2Context.currentIndex;

            bool isP1Random = manager.stageRoster[p1Idx].stageName == "Random";
            bool isP2Random = manager.stageRoster[p2Idx].stageName == "Random";

            int finalIndex = 0;
            bool triggerRoulette = false;
            List<int> validIndices = manager.GetValidStageIndices();

            if (isP1Random || isP2Random)
            {
                if (validIndices.Count > 0)
                {
                    finalIndex = validIndices[Random.Range(0, validIndices.Count)];
                }
                triggerRoulette = true;
            }
            else if (p1Idx != p2Idx)
            {
                finalIndex = (Random.Range(0, 2) == 0) ? p1Idx : p2Idx;
                triggerRoulette = true;
            }
            else
            {
                finalIndex = p1Idx;
                triggerRoulette = false;
            }

            if (triggerRoulette)
            {
                manager.StartRoulette(finalIndex);
            }
            else
            {
                int safeIndex = Mathf.Clamp(finalIndex, 0, manager.stageRoster.Length - 1);
                MatchDataManager.SelectedStageData = manager.stageRoster[safeIndex];
                
                GameFlowManager.Instance.ChangeScene(GameSceneType.GamePlay);
            }
        }
    }

    public bool IsPlayerActive(int playerId)
    {
        return true;
    }
}