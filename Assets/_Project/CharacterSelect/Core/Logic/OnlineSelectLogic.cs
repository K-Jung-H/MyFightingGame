using UnityEngine;

public class OnlineSelectLogic : ICharacterSelectLogic
{
    private CharacterSelectManager manager;

    public void Initialize(CharacterSelectManager manager)
    {
        this.manager = manager;
        if (manager.changeSideButton != null) manager.changeSideButton.gameObject.SetActive(false);
    }

    public void ProcessInput()
    {
        if (RoomStateManager.Instance == null) return;

        int localSlot = RoomStateManager.Instance.GetLocalPlayerSlot();
        RoomStateModel model = RoomStateManager.Instance.roomModel;
        int mySide = (localSlot == 0) ? model.p1PreferredSide : model.p2PreferredSide;
        PlayerSelectContext myContext = (mySide == 0) ? manager.leftContext : manager.rightContext;
        
        HandleContextInput(localSlot + 1, myContext);
    }

    private void HandleContextInput(int playerId, PlayerSelectContext context)
    {
        if (manager.isStartButtonReady)
        {
            if (manager.GetSelectInput(context) && !manager.isStartRequestSent)
            {
                manager.SetStartRequestSent();
                if (ServerNetworkManager.Instance != null) ServerNetworkManager.Instance.SendStartRequest();
            }
            return;
        }

        bool isSelect = manager.GetSelectInput(context);

        if (!context.isLocked && isSelect)
        {
            context.isLocked = true;
            manager.UpdateLockUI(context);
            manager.SaveCharacterData(playerId, context.currentIndex);
            SendStateToServer(playerId, context);
        }
        else if (context.isLocked && isSelect)
        {
            context.isLocked = false;
            manager.UpdateLockUI(context);
            manager.SaveCharacterData(playerId, context.currentIndex);
            SendStateToServer(playerId, context);
        }

        if (context.isLocked) return;

        int move = manager.GetMovementInput(context);
        if (move != 0)
        {
            int oldIdx = context.currentIndex;
            context.currentIndex = (context.currentIndex + move + manager.characterRoster.Length) % manager.characterRoster.Length;
            manager.UpdateCharacterDisplay(context);
            manager.UpdateSpecificTiles(oldIdx, context.currentIndex);
            SendStateToServer(playerId, context);
        }
    }

    private void SendStateToServer(int playerId, PlayerSelectContext context)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendSelectUpdate(playerId == 1 ? 0 : 1, context.currentIndex, context.isLocked);
        }
    }

    public void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p1Side, int p2Idx, bool p2Lock, int p2Side)
    {
        if (RoomStateManager.Instance == null) return;

        int localSlot = RoomStateManager.Instance.GetLocalPlayerSlot();
        int mySide = (localSlot == 0) ? p1Side : p2Side;
        int opponentSide = 1 - mySide; 

        PlayerSelectContext myContext = (mySide == 0) ? manager.leftContext : manager.rightContext;
        PlayerSelectContext opponentContext = (opponentSide == 0) ? manager.leftContext : manager.rightContext;

        if (localSlot == 0) 
        {
            UpdateRemoteState(myContext, p1Idx, p1Lock, 1);        
            UpdateRemoteState(opponentContext, p2Idx, p2Lock, 2);  
        }
        else 
        {
            UpdateRemoteState(opponentContext, p1Idx, p1Lock, 1);  
            UpdateRemoteState(myContext, p2Idx, p2Lock, 2);     
        }
    }

    private void UpdateRemoteState(PlayerSelectContext context, int newIdx, bool newLock, int playerId)
    {
        newIdx = Mathf.Clamp(newIdx, 0, manager.characterRoster.Length - 1);

        if (context.currentIndex != newIdx)
        {
            int oldIdx = context.currentIndex;
            context.currentIndex = newIdx;
            manager.UpdateCharacterDisplay(context);
            manager.UpdateSpecificTiles(oldIdx, newIdx);
        }

        if (context.isLocked != newLock)
        {
            context.isLocked = newLock;
            manager.UpdateLockUI(context);
            if (newLock) manager.SaveCharacterData(playerId, newIdx);
        }
    }
}