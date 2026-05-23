using UnityEngine;

public class TrainingCharacterSelectLogic : ICharacterSelectLogic
{
    private CharacterSelectManager manager;
    private int playerSide = 0;

    public void Initialize(CharacterSelectManager manager)
    {
        this.manager = manager;
        if (manager.changeSideButton != null)
        {
            manager.changeSideButton.gameObject.SetActive(true);
            manager.changeSideButton.onClick.RemoveAllListeners();
            manager.changeSideButton.onClick.AddListener(OnSideChanged);
        }
        RefreshSideState();
    }

    public int GetPlayerSide()
    {
        return playerSide;
    }

    private void OnSideChanged()
    {
        playerSide = (playerSide == 0) ? 1 : 0;
        RefreshSideState();
    }

    private void RefreshSideState()
    {
        bool isLeft = (playerSide == 0);
        manager.SetContextVisibility(manager.leftContext, isLeft);
        manager.SetContextVisibility(manager.rightContext, !isLeft);

        if (isLeft)
        {
            manager.leftContext.isLocked = false;
            manager.rightContext.isLocked = true;
            manager.SaveCharacterData(2, 0);
        }
        else
        {
            manager.rightContext.isLocked = false;
            manager.leftContext.isLocked = true;
            manager.SaveCharacterData(1, 0);
        }
        
        manager.UpdateLockUI(manager.leftContext);
        manager.UpdateLockUI(manager.rightContext);
    }

    public void ProcessInput()
    {
        if (playerSide == 0) HandleContextInput(1, manager.leftContext);
        else HandleContextInput(2, manager.rightContext);
        
        manager.EvaluateOfflineState();
    }

    private void HandleContextInput(int playerId, PlayerSelectContext context)
    {
        if (manager.isStartButtonReady)
        {
            if (manager.GetSelectInput(context) && !manager.isStartRequestSent)
            {
                manager.SetStartRequestSent();
                MatchDataManager.TrainingLocalPlayerSide = playerSide;
                GameFlowManager.Instance.ChangeScene(GameSceneType.StageSelect);
            }
            return;
        }

        bool isSelect = manager.GetSelectInput(context);

        if (!context.isLocked && isSelect)
        {
            context.isLocked = true;
            manager.UpdateLockUI(context);
            manager.SaveCharacterData(playerId, context.currentIndex);
        }
        else if (context.isLocked && isSelect)
        {
            context.isLocked = false;
            manager.UpdateLockUI(context);
            manager.SaveCharacterData(playerId, context.currentIndex);
        }

        if (context.isLocked) return;

        int move = manager.GetMovementInput(context);
        if (move != 0)
        {
            int oldIdx = context.currentIndex;
            context.currentIndex = (context.currentIndex + move + manager.characterRoster.Length) % manager.characterRoster.Length;
            manager.UpdateCharacterDisplay(context);
            manager.UpdateSpecificTiles(oldIdx, context.currentIndex);
        }
    }

    public void OnStateUpdatedFromServer(int p1, bool p1L, int p1S, int p2, bool p2L, int p2S) { }
}