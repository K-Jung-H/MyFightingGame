using UnityEngine;

public class OnlineModeLogic : IGameModeLogic
{
    private GameLoopManager manager;
    private bool isShowServer = false;
    private bool isShowNetworkDetails = false;
    
    public void Initialize(GameLoopManager manager) { this.manager = manager; }

    public void StartGame()
    {
        manager.simState.isSimulationRunning = false;
        manager.connectionState.isWaitingForP2PConnection = true;
        manager.connectionState.isWaitingForServerSync = true;
        manager.connectionState.syncTimeoutTimer = 0f;
        manager.connectionState.p2pConnectTimeoutTimer = 0f;

        string targetIp = RoomStateManager.Instance != null ? RoomStateManager.Instance.GetTargetPeerIpAddress() : "127.0.0.1";
        manager.SetupP2PConnection(targetIp);
    }

    public void ProcessFixedUpdate()
    {
        if (manager.connectionState.isWaitingForP2PConnection)
        {
            manager.connectionState.p2pConnectTimeoutTimer += Time.fixedDeltaTime;
            if (manager.connectionState.p2pConnectTimeoutTimer > GameLoopManager.P2P_CONNECT_TIMEOUT_LIMIT)
            {
                manager.HandleMatchAborted(GameSceneType.OnlineMatchedRoom);
                return;
            }
            manager.ProcessP2PHandshake();
            return;
        }

        if (manager.connectionState.isWaitingForServerSync)
        {
            manager.connectionState.syncTimeoutTimer += Time.fixedDeltaTime;
            if (manager.connectionState.syncTimeoutTimer > GameLoopManager.SYNC_TIMEOUT_LIMIT)
            {
                manager.HandleMatchAborted(GameSceneType.OnlineMatchedRoom);
                return;
            }
            if (manager.connectionState.currentP2PNetwork != null) manager.connectionState.currentP2PNetwork.PumpNetworkTick();
            return;
        }

        if (!manager.simState.isSimulationRunning) return;

        if (manager.connectionState.currentP2PNetwork != null)
        {
            manager.connectionState.currentP2PNetwork.PumpNetworkTick();

            if (!manager.connectionState.currentP2PNetwork.GetIsConnected())
            {
                manager.TriggerDesyncError();
                return;
            }

            manager.syncController.VerifySyncState();

            bool isP1Left = manager.GetIsP1VisuallyOnLeft();
            bool isLocalFacingRight = manager.connectionState.localPlayerSlot == 0 ? isP1Left : !isP1Left;

            if (manager.simState.isCameraFlipped) isLocalFacingRight = !isLocalFacingRight;

            bool isTickProcessed = manager.syncController.TryProcessNetworkTick(
                manager.simState.currentTick, isLocalFacingRight, manager.simState.isCameraFlipped, out PlayerInput p1, out PlayerInput p2);

            if (isTickProcessed) manager.ProcessTick(p1, p2);
        }
    }

    public void OnGUI()
    {
        float panelWidth = 280f;
        float bottomY = Screen.height - 30f;
        float serverStartX = (Screen.width - panelWidth) / 2f;

        DrawServerStatusPanel(serverStartX, bottomY, panelWidth);
    }

    private void DrawServerStatusPanel(float startX, float bottomY, float width)
    {
        string serverTitle = isShowServer ? "▼ Server Status" : "▲ Server Status";
        if (GUI.Button(new Rect(startX, bottomY, width, 20), serverTitle)) isShowServer = !isShowServer;

        if (isShowServer)
        {
            float boxHeight = isShowNetworkDetails ? 210f : 160f;
            float contentY = bottomY - boxHeight;
            
            GUI.Box(new Rect(startX, contentY, width, boxHeight), "");

            float currentY = contentY + 10f;
            GUI.Label(new Rect(startX + 10, currentY, width - 20, 20), $"Current Tick: {manager.GetCurrentTick()}");
            currentY += 25f;

            currentY = DrawNetworkStateSection(startX, currentY, width);

            PlayerController p1 = manager.GetPlayerOneController();
            PlayerController p2 = manager.GetPlayerTwoController();

            int p1Hp = p1 != null ? p1.GetCombat().GetCurrentHealth() : 0;
            int p1Max = p1 != null ? p1.GetCombat().GetMaxHealth() : 0;
            int p2Hp = p2 != null ? p2.GetCombat().GetCurrentHealth() : 0;
            int p2Max = p2 != null ? p2.GetCombat().GetMaxHealth() : 0;

            GUI.Label(new Rect(startX + 10, currentY, width - 20, 20), $"P1 State: {manager.GetP1State()}");
            GUI.Label(new Rect(startX + 10, currentY + 15, width - 20, 20), $"P1 Pos: {manager.GetP1Pos()}");
            GUI.Label(new Rect(startX + 10, currentY + 30, width - 20, 20), $"P1 HP: {p1Hp} / {p1Max}");
            currentY += 50f;

            GUI.Label(new Rect(startX + 10, currentY, width - 20, 20), $"P2 State: {manager.GetP2State()}");
            GUI.Label(new Rect(startX + 10, currentY + 15, width - 20, 20), $"P2 Pos: {manager.GetP2Pos()}");
            GUI.Label(new Rect(startX + 10, currentY + 30, width - 20, 20), $"P2 HP: {p2Hp} / {p2Max}");
        }
    }

    private float DrawNetworkStateSection(float startX, float currentY, float width)
    {
        string netTitle = isShowNetworkDetails ? "▼ Network State" : "▶ Network State";
        if (GUI.Button(new Rect(startX + 10, currentY, width - 20, 18), netTitle)) isShowNetworkDetails = !isShowNetworkDetails;
        currentY += 20f;

        if (isShowNetworkDetails)
        {
            P2PNetworkManager p2pManager = manager.connectionState.currentP2PNetwork;
            bool isConnected = p2pManager != null && p2pManager.GetIsConnected();

            GUI.color = isConnected ? Color.green : Color.red;
            GUI.Label(new Rect(startX + 20, currentY, width - 30, 20), $"Connection: {(isConnected ? "Connected" : "Disconnected")}");
            currentY += 15f;

            NetworkSyncState syncState = manager.syncController.GetSyncState();
            int rollbackFrames = Mathf.Max(0, manager.simState.currentTick - syncState.latestConfirmedTick);

            GUI.color = Color.white;
            GUI.Label(new Rect(startX + 20, currentY, width - 30, 20), $"Ping: {syncState.currentPingMs} ms | Rollback: {rollbackFrames} F");
            currentY += 15f;

            bool isHardStalling = manager.GetIsHardStalling();
            bool isSoftStalling = manager.GetIsSoftStalling();

            string lockstepStatus = "Running";
            GUI.color = Color.green;

            if (isHardStalling) { lockstepStatus = "HARD STALLED"; GUI.color = Color.red; }
            else if (isSoftStalling) { lockstepStatus = "SOFT STALLED"; GUI.color = Color.yellow; }

            GUI.Label(new Rect(startX + 20, currentY, width - 30, 20), $"Lockstep: {lockstepStatus}");
            currentY += 15f;

            bool isDesync = manager.GetIsDesyncDetected();
            GUI.color = isDesync ? Color.red : Color.green;
            GUI.Label(new Rect(startX + 20, currentY, width - 30, 20), $"Sync: {(isDesync ? "DESYNC DETECTED" : "Synced")}");
            GUI.color = Color.white;
            currentY += 25f;
        }
        else
        {
            currentY += 5f;
        }

        return currentY;
    }

    public bool ShouldCheckRoundEnd() { return true; }
    public bool ShouldUpdateTimer() { return true; }

    public void HandleMatchEndAction(MatchEndActionType actionType)
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendMatchEndAction(actionType);
        }
    }
}