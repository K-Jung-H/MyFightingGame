using UnityEngine;

public class OfflineCharacterSelectLogic : ICharacterSelectLogic
{
    private CharacterSelectManager manager;
    public void Initialize(CharacterSelectManager manager) { this.manager = manager; }

    public void ProcessInput()
    {
        HandleContextInput(1, manager.leftContext);
        HandleContextInput(2, manager.rightContext);
        manager.EvaluateOfflineState();
    }

    private void HandleContextInput(int playerId, PlayerSelectContext context)
    {
        if (manager.isStartButtonReady)
        {
            if (manager.GetSelectInput(context) && !manager.isStartRequestSent)
            {
                manager.SetStartRequestSent();
                GameFlowManager.Instance.ChangeScene(GameSceneType.StageSelect);
            }
            return;
        }

        bool isLock = manager.GetOfflineLockInput(context);
        bool isUnlock = manager.GetOfflineUnlockInput(context);

        if (!context.isLocked && isLock)
        {
            context.isLocked = true;
            manager.UpdateLockUI(context);
            manager.SaveCharacterData(playerId, context.currentIndex);
        }
        else if (context.isLocked && isUnlock)
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