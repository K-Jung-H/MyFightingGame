using UnityEngine;
using System.Collections.Generic;

public class DebugHUD : MonoBehaviour
{
    [SerializeField] private GameLoopManager gameLoopManager;
    [SerializeField] private PlayerController p1Controller;
    [SerializeField] private PlayerController p2Controller;

    private Queue<string> p1InputLogQueue = new Queue<string>();
    private Queue<string> p2InputLogQueue = new Queue<string>();
    private int maxLogCount = 10;

    private bool isShowServer = true;
    private bool isShowP1 = true;
    private bool isShowP2 = true;

    private void Start()
    {
        TryConnectControllers();
    }

    private void TryConnectControllers()
    {
        bool isAlreadyConnected = p1Controller != null && p2Controller != null;
        if (isAlreadyConnected) return;
        
        bool isManagerMissing = gameLoopManager == null;
        if (isManagerMissing) return;
        
        bool isP1Missing = p1Controller == null;
        if (isP1Missing)
        {
            p1Controller = gameLoopManager.GetPlayerOneController();
        }

        bool isP2Missing = p2Controller == null;
        if (isP2Missing)
        {
            p2Controller = gameLoopManager.GetPlayerTwoController();
        }
    }

    private void OnGUI()
    {
        bool isManagerMissing = gameLoopManager == null;
        if (isManagerMissing) return;

        bool isAnyControllerMissing = p1Controller == null || p2Controller == null;
        if (isAnyControllerMissing)
        {
            TryConnectControllers();
        }

        float currentY = 10f;

        string serverTitle = isShowServer ? "▼ Server Status" : "▶ Server Status";
        if (GUI.Button(new Rect(10, currentY, 280, 20), serverTitle))
        {
            isShowServer = !isShowServer;
        }
        currentY += 20f;

        if (isShowServer)
        {
            GUI.Box(new Rect(10, currentY, 280, 110), "");
            GUI.Label(new Rect(20, currentY + 10, 260, 20), $"Current Tick: {gameLoopManager.GetCurrentTick()}");
            GUI.Label(new Rect(20, currentY + 40, 260, 20), $"P1 State: {gameLoopManager.GetP1State()}");
            GUI.Label(new Rect(20, currentY + 55, 260, 20), $"P1 Pos: {gameLoopManager.GetP1Pos()}");
            GUI.Label(new Rect(20, currentY + 75, 260, 20), $"P2 State: {gameLoopManager.GetP2State()}");
            GUI.Label(new Rect(20, currentY + 90, 260, 20), $"P2 Pos: {gameLoopManager.GetP2Pos()}");
            currentY += 120f;
        }
        else
        {
            currentY += 5f;
        }

        bool hasP1Controller = p1Controller != null;
        if (hasP1Controller)
        {
            string p1Title = isShowP1 ? "▼ P1 Input & Action Debug" : "▶ P1 Input & Action Debug";
            if (GUI.Button(new Rect(10, currentY, 280, 20), p1Title))
            {
                isShowP1 = !isShowP1;
            }
            currentY += 20f;

            if (isShowP1)
            {
                currentY = DrawInputDebugContent(p1Controller, p1InputLogQueue, currentY);
            }
            else
            {
                currentY += 5f;
            }
        }

        bool hasP2Controller = p2Controller != null;
        if (hasP2Controller)
        {
            string p2Title = isShowP2 ? "▼ P2 Input & Action Debug" : "▶ P2 Input & Action Debug";
            if (GUI.Button(new Rect(10, currentY, 280, 20), p2Title))
            {
                isShowP2 = !isShowP2;
            }
            currentY += 20f;

            if (isShowP2)
            {
                currentY = DrawInputDebugContent(p2Controller, p2InputLogQueue, currentY);
            }
        }
    }

    private float DrawInputDebugContent(PlayerController controller, Queue<string> logQueue, float startY)
    {
        float panelHeight = 350f;
        GUI.Box(new Rect(10, startY, 280, panelHeight), "");

        InputFlags currentHold = controller.currentInput.flags;
        GUI.Label(new Rect(20, startY + 10, 260, 20), $"[Current Hold]: {InputFlagsToString(currentHold)}");

        ActionDataSO currentAction = controller.GetStateMachine().GetCurrentActionData();
        bool hasCurrentAction = currentAction != null;
        string actionName = hasCurrentAction ? currentAction.animationStateName : "None";
        GUI.Label(new Rect(20, startY + 35, 260, 20), $"[Current Action]: {actionName}");

        InputFlags keyDown = controller.currentKeyDownFlags;
        bool hasNewInput = keyDown != InputFlags.None;
        if (hasNewInput)
        {
            string logEntry = $"Tick {gameLoopManager.GetCurrentTick(),-5} | {InputFlagsToString(keyDown)}";
            logQueue.Enqueue(logEntry);

            bool isQueueFull = logQueue.Count > maxLogCount;
            if (isQueueFull)
            {
                logQueue.Dequeue();
            }
        }

        GUI.Label(new Rect(20, startY + 65, 260, 20), "--- Input Key Log ---");

        float yOffset = startY + 85;
        foreach (string log in logQueue)
        {
            GUI.Label(new Rect(20, yOffset, 260, 20), log);
            yOffset += 15;
        }

        yOffset = startY + 245;
        GUI.Label(new Rect(20, yOffset, 260, 20), "--- Active Combo Sequence ---");
        yOffset += 20;

        List<InputFlags> comboSeq = controller.GetActionController().GetComboSequence();
        List<string> formattedCombo = new List<string>();
        
        bool hasComboSequence = comboSeq != null && comboSeq.Count > 0;
        if (hasComboSequence)
        {
            foreach (var flag in comboSeq)
            {
                formattedCombo.Add(InputFlagsToString(flag));
            }
        }
        
        string comboString = hasComboSequence ? string.Join(" -> ", formattedCombo) : "Empty (Idle/Broken)";

        GUIStyle multilineStyle = new GUIStyle(GUI.skin.label);
        multilineStyle.wordWrap = true;
        GUI.Label(new Rect(20, yOffset, 260, 60), comboString, multilineStyle);

        return startY + panelHeight + 10f;
    }

    private string InputFlagsToString(InputFlags flags)
    {
        bool isNone = flags == InputFlags.None;
        if (isNone) return "None";

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

        bool isDirectionPresent = !string.IsNullOrEmpty(directionString);
        bool isAttackPresent = !string.IsNullOrEmpty(attackString);

        if (isDirectionPresent && isAttackPresent)
        {
            return $"{directionString} + {attackString}";
        }
        else if (isDirectionPresent)
        {
            return directionString;
        }
        else if (isAttackPresent)
        {
            return attackString;
        }

        return flags.ToString(); 
    }
}