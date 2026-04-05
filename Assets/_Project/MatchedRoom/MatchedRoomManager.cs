using UnityEngine;
using System;

public class MatchedRoomManager : MonoBehaviour
{
    [SerializeField] private MatchedRoomUIManager uiManager;

    private int localPlayerSlot;
    private void Start()
    {
        if (RoomStateManager.Instance != null)
        {
            localPlayerSlot = RoomStateManager.Instance.GetLocalPlayerSlot();
        }

        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnRoomStateBroadcastReceived += HandleRoomStateBroadcast;
            ServerNetworkManager.Instance.OnSlotAssignedReceived += HandleSlotAssigned;
        }

        SyncInitialState();
    }

    private void OnDestroy()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnRoomStateBroadcastReceived -= HandleRoomStateBroadcast;
            ServerNetworkManager.Instance.OnSlotAssignedReceived -= HandleSlotAssigned;
        }
    }

    public void RequestRuleUpdate(int rounds, int timeLimit)
    {
        if (localPlayerSlot != 0) return;
        
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendRuleUpdate(rounds, timeLimit);
        }
    }

    public void ToggleReadyState()
    {
        RoomStateModel model = RoomStateManager.Instance.roomModel;
        bool isCurrentReady = (localPlayerSlot == 0) ? model.isP1Ready : model.isP2Ready;
        
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendReadyStateUpdate(!isCurrentReady);
        }
    }

    public void LeaveRoom()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendRoomLeaveRequest();
        }
        
        GameFlowManager.Instance.ChangeScene(GameSceneType.OnlineLobby);
    }

    public void AttemptStartMatch()
    {
        RoomStateModel model = RoomStateManager.Instance.roomModel;
        
        bool isBothReady = model.isP1Ready && model.isP2Ready;
        
        if (localPlayerSlot == 0 && isBothReady)
        {
            if (ServerNetworkManager.Instance != null)
            {
                ServerNetworkManager.Instance.SendLobbyStartRequest();
            }
        }
    }

    private void SyncInitialState()
    {
        if (RoomStateManager.Instance != null && uiManager != null)
        {
            uiManager.RefreshUI(RoomStateManager.Instance.roomModel, localPlayerSlot);
        }
    }

    private void HandleRoomStateBroadcast(RoomStateModel updatedModel)
    {
        if (RoomStateManager.Instance != null)
        {
            RoomStateManager.Instance.UpdateRoomModel(updatedModel);
            
            if (uiManager != null)
            {
                uiManager.RefreshUI(updatedModel, localPlayerSlot);
            }
        }
    }

    private void HandleSlotAssigned(int newSlot)
    {
        localPlayerSlot = newSlot;
        
        if (uiManager != null && RoomStateManager.Instance != null)
        {
            uiManager.RefreshUI(RoomStateManager.Instance.roomModel, localPlayerSlot);
        }
    }

}