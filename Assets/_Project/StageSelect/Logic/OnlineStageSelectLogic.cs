// OnlineStageSelectLogic.cs
using UnityEngine;

public class OnlineStageSelectLogic : IStageSelectLogic
{
    private StageSelectManager manager;
    private int localPlayerSide = 1; // 정보 부족: 네트워크 룸 진입 시 할당된 본인 Side (예: P1=1, P2=2)

    public void Initialize(StageSelectManager manager)
    {
        this.manager = manager;
        // 정보 부족: 룸 데이터나 네트워크 매니저로부터 현재 클라이언트의 로컬 Side 할당 로직이 연동되어야 함
        // 예: localPlayerSide = ServerNetworkManager.Instance.IsHost ? 1 : 2;
    }


    public void ProcessInput()
    {
        StagePlayerContext localContext = (localPlayerSide == 1) ? manager.p1Context : manager.p2Context;

        if (!localContext.isLocked)
        {
            int moveInput = manager.GetMovementInput(localContext);
            if (moveInput != 0)
            {
                manager.MoveCursor(localContext, moveInput);
                UpdateBackground();
                // ServerNetworkManager.Instance.SendStageSelectIndex(localContext.currentIndex);
            }

            if (manager.GetOfflineLockInput(localContext))
            {
                // ServerNetworkManager.Instance.SendStageLockRequest(true);
            }
        }
    }


    public void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock)
    {
        manager.p1Context.currentIndex = p1Idx;
        manager.p1Context.isLocked = p1Lock;
        manager.p2Context.currentIndex = p2Idx;
        manager.p2Context.isLocked = p2Lock;

        manager.UpdateAllVisuals();
        UpdateBackground();
        EvaluateSceneTransition();
    }


    public void UpdateBackground()
    {
        if (manager.stageRoster == null || manager.canvasBackgroundImage == null) return;

        int targetIndex = (localPlayerSide == 1) ? manager.p1Context.currentIndex : manager.p2Context.currentIndex;
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
                if (!manager.isLocalCountdownActive) manager.StartCountdown();
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