using UnityEngine;

public class OnlineStageSelectLogic : IStageSelectLogic
{
    private StageSelectManager manager;

    public void Initialize(StageSelectManager manager)
    {
        this.manager = manager;

        if (RoomStateManager.Instance != null)
        {
            RoomStateManager.Instance.OnStageSelectUpdated += OnStateUpdatedFromServer;
            RoomStateManager.Instance.OnStageRouletteStarted += HandleRouletteStarted;
            UpdateFromRoomModel(RoomStateManager.Instance.roomModel);
        }
    }
    
    public void Cleanup()
    {
        if (RoomStateManager.Instance != null)
        {
            RoomStateManager.Instance.OnStageSelectUpdated -= OnStateUpdatedFromServer;
            RoomStateManager.Instance.OnStageRouletteStarted -= HandleRouletteStarted;
        }
    }

    public void HandleInputs(int p1Move, bool p1Select, int p2Move, bool p2Select)
    {
        if (RoomStateManager.Instance == null) return;

        RoomStateModel model = RoomStateManager.Instance.roomModel;
        int localSlot = RoomStateManager.Instance.GetLocalPlayerSlot();
        
        int mySide = (localSlot == 0) ? model.p1PreferredSide : model.p2PreferredSide;
        StagePlayerContext myContext = (mySide == 0) ? manager.p1Context : manager.p2Context;

        bool select = (mySide == 0) ? p1Select : p2Select;
        int move = (mySide == 0) ? p1Move : p2Move;

        if (select)
        {
            myContext.isLocked = !myContext.isLocked;
            if (myContext.isLocked) manager.LockSelection(myContext);
            else manager.UnlockSelection(myContext);
            
            SendUpdateToServer(myContext);
        }
        else if (move != 0 && !myContext.isLocked)
        {
            manager.MoveCursor(myContext, move);
            UpdateBackground();
            SendUpdateToServer(myContext);
        }
    }

    private void SendUpdateToServer(StagePlayerContext context)
    {
        if (ServerNetworkManager.Instance != null)
        {
            bool isRandom = manager.stageRoster[context.currentIndex].stageName == "Random";
            ServerNetworkManager.Instance.SendStageSelectUpdate(context.currentIndex, context.isLocked, isRandom, manager.stageRoster.Length);
        }
    }

    public void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock)
    {
        if (RoomStateManager.Instance == null) return;
        RoomStateModel model = RoomStateManager.Instance.roomModel;

        int localSlot = RoomStateManager.Instance.GetLocalPlayerSlot();
        int p1Side = model.p1PreferredSide;
        int p2Side = model.p2PreferredSide;
        
        int mySide = (localSlot == 0) ? p1Side : p2Side;
        int opponentSide = 1 - mySide; 

        StagePlayerContext myContext = (mySide == 0) ? manager.p1Context : manager.p2Context;
        StagePlayerContext opponentContext = (opponentSide == 0) ? manager.p1Context : manager.p2Context;

        if (localSlot == 0) 
        {
            UpdateRemoteState(myContext, p1Idx, p1Lock);        
            UpdateRemoteState(opponentContext, p2Idx, p2Lock);  
        }
        else 
        {
            UpdateRemoteState(opponentContext, p1Idx, p1Lock);  
            UpdateRemoteState(myContext, p2Idx, p2Lock);     
        }
        
        manager.UpdateAllVisuals();

        if (p1Lock && p2Lock && p1Idx == p2Idx)
        {
            bool isRandom = manager.stageRoster[p1Idx].stageName == "Random";
            if (!isRandom)
            {
                MatchDataManager.SelectedStageData = manager.stageRoster[Mathf.Clamp(p1Idx, 0, manager.stageRoster.Length - 1)];
            }
        }
    }

    private void UpdateRemoteState(StagePlayerContext context, int newIdx, bool newLock)
    {
        if (context.currentIndex == newIdx && context.isLocked == newLock) return;

        context.currentIndex = Mathf.Clamp(newIdx, 0, manager.stageRoster.Length - 1);
        context.isLocked = newLock;
        
        if (context.isLocked) manager.LockSelection(context);
        else manager.UnlockSelection(context);
    }

    private void HandleRouletteStarted(int finalIndex)
    {
        if (manager == null) return;

        int safeIndex = Mathf.Clamp(finalIndex, 0, manager.stageRoster.Length - 1);
        MatchDataManager.SelectedStageData = manager.stageRoster[safeIndex];
        manager.StartRoulette(finalIndex);
    }

    private void UpdateFromRoomModel(RoomStateModel model)
    {
        if (model == null) 
        {
            return;
        }   

        OnStateUpdatedFromServer(model.p1StageIndex, model.isP1StageLocked, model.p2StageIndex, model.isP2StageLocked);
    }

    public void UpdateBackground()
    {
        if (manager.stageRoster == null || manager.canvasBackgroundImage == null) return;
        if (RoomStateManager.Instance == null) return;

        int localSlot = RoomStateManager.Instance.GetLocalPlayerSlot();
        RoomStateModel model = RoomStateManager.Instance.roomModel;
        int mySide = (localSlot == 0) ? model.p1PreferredSide : model.p2PreferredSide;
        StagePlayerContext localContext = (mySide == 0) ? manager.p1Context : manager.p2Context;

        int targetIndex = localContext.currentIndex;
        if (targetIndex >= 0 && targetIndex < manager.stageRoster.Length)
        {
            manager.canvasBackgroundImage.sprite = manager.stageRoster[targetIndex].thumbnail;
        }
    }

    public void EvaluateSceneTransition() { }

    public bool IsPlayerActive(int playerId) => true;
}