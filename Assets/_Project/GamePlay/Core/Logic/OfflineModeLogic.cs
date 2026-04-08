using UnityEngine;
using System.Collections.Generic;

public class OfflineModeLogic : IGameModeLogic
{
    private GameLoopManager manager;
    private bool isShowP1DebugPanel = false;
    private bool isShowP2DebugPanel = false;

    public void Initialize(GameLoopManager manager) { this.manager = manager; }
    
    public void StartGame() { manager.InitializeMatch(); }

    public void ProcessFixedUpdate()
    {
        if (!manager.simState.isSimulationRunning) return;

        bool isP1Left = manager.GetIsP1VisuallyOnLeft();
        bool isP1FacingRight = manager.simState.isCameraFlipped ? !isP1Left : isP1Left;

        PlayerInput p1 = manager.inputProvider.GetCurrentInput(manager.simState.currentTick, 0, isP1FacingRight, manager.simState.isCameraFlipped);
        PlayerInput p2 = manager.inputProvider.GetCurrentInput(manager.simState.currentTick, 1, !isP1FacingRight, manager.simState.isCameraFlipped);
        
        manager.ProcessTick(p1, p2);
    }

    public void OnGUI() 
    { 
        float panelWidth = 280f;
        float bottomY = Screen.height - 30f;

        float p1StartX = 10f;
        float p2StartX = Screen.width - panelWidth - 10f;

        DrawDebugPanel(manager.GetPlayerOneController(), p1StartX, bottomY, panelWidth, 0, ref isShowP1DebugPanel);
        DrawDebugPanel(manager.GetPlayerTwoController(), p2StartX, bottomY, panelWidth, 1, ref isShowP2DebugPanel);
    }

    private void DrawDebugPanel(PlayerController controller, float startX, float bottomY, float width, int sideIndex, ref bool isShowPanel)
    {
        if (controller == null) return;

        string sideName = (sideIndex == 0) ? "P1" : "P2";
        string title = isShowPanel ? $"▼ {sideName} Input & Action Debug" : $"▲ {sideName} Input & Action Debug";
        
        if (GUI.Button(new Rect(startX, bottomY, width, 20), title))
        {
            isShowPanel = !isShowPanel;
        }

        if (isShowPanel)
        {
            float panelHeight = 150f; 
            DrawInputDebugContent(controller, startX, bottomY - panelHeight, width, panelHeight);
        }
    }

    private void DrawInputDebugContent(PlayerController controller, float startX, float startY, float width, float panelHeight)
    {
        GUI.Box(new Rect(startX, startY, width, panelHeight), "");

        InputFlags currentHold = controller.currentInput.flags;
        GUI.Label(new Rect(startX + 10, startY + 10, width - 20, 20), $"[Current Hold]: {InputFlagsToString(currentHold)}");

        ActionDataSO currentAction = controller.GetStateMachine().GetCurrentActionData();
        string actionName = currentAction != null ? currentAction.animationStateName : "None";
        GUI.Label(new Rect(startX + 10, startY + 35, width - 20, 20), $"[Current Action]: {actionName}");

        float yOffset = startY + 65;
        GUI.Label(new Rect(startX + 10, yOffset, width - 20, 20), "--- Active Combo Sequence ---");
        yOffset += 20;

        List<InputFlags> comboSeq = controller.GetActionController().GetComboSequence();
        List<string> formattedCombo = new List<string>();

        if (comboSeq != null && comboSeq.Count > 0)
        {
            foreach (var flag in comboSeq)
            {
                formattedCombo.Add(InputFlagsToString(flag));
            }
        }

        string comboString = (comboSeq != null && comboSeq.Count > 0) ? string.Join(" -> ", formattedCombo) : "Empty (Idle/Broken)";

        GUIStyle multilineStyle = new GUIStyle(GUI.skin.label);
        multilineStyle.wordWrap = true;
        GUI.Label(new Rect(startX + 10, yOffset, width - 20, 60), comboString, multilineStyle);
    }

    private string InputFlagsToString(InputFlags flags)
    {
        if (flags == InputFlags.None) return "None";

        string directionString = "";
        bool isUp = (flags & InputFlags.Up) != 0;
        bool isDown = (flags & InputFlags.Down) != 0;
        bool isForward = (flags & InputFlags.Forward) != 0;
        bool isBack = (flags & InputFlags.Back) != 0;

        if (isUp && isDown) directionString = "↕";
        else if (isBack && isForward) directionString = "↔";
        else if (isUp && isForward) directionString = "↗";
        else if (isUp && isBack) directionString = "↖";
        else if (isDown && isForward) directionString = "↘";
        else if (isDown && isBack) directionString = "↙";
        else if (isUp) directionString = "↑";
        else if (isDown) directionString = "↓";
        else if (isBack) directionString = "←";
        else if (isForward) directionString = "→";

        string attackString = "";
        bool isLP = (flags & InputFlags.LP) != 0;
        bool isRP = (flags & InputFlags.RP) != 0;
        bool isLK = (flags & InputFlags.LK) != 0;
        bool isRK = (flags & InputFlags.RK) != 0;

        if (isLP) attackString += (attackString.Length > 0 ? " + LP" : "LP");
        if (isRP) attackString += (attackString.Length > 0 ? " + RP" : "RP");
        if (isLK) attackString += (attackString.Length > 0 ? " + LK" : "LK");
        if (isRK) attackString += (attackString.Length > 0 ? " + RK" : "RK");

        if (!string.IsNullOrEmpty(directionString) && !string.IsNullOrEmpty(attackString)) return $"{directionString} + {attackString}";
        else if (!string.IsNullOrEmpty(directionString)) return directionString;
        else if (!string.IsNullOrEmpty(attackString)) return attackString;

        return flags.ToString();
    }

    public bool ShouldCheckRoundEnd() { return true; }
    public bool ShouldUpdateTimer() { return true; }

    public void HandleMatchEndAction(MatchEndActionType actionType)
    {
        if (actionType == MatchEndActionType.ReturnToMenu)
            GameFlowManager.Instance.ChangeScene(GameSceneType.GameModeSelect);
        else if (actionType == MatchEndActionType.ReturnToCharacterSelect)
            GameFlowManager.Instance.ChangeScene(GameSceneType.CharacterSelect);
        else if (actionType == MatchEndActionType.Rematch)
        {
            manager.HideMatchResultUI();
            manager.InitializeMatch();
            manager.simState.isSimulationRunning = true;
        }
    }
}