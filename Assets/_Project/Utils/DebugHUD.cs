using UnityEngine;
using System.Collections.Generic;

public class DebugHUD : MonoBehaviour
{
    [SerializeField] private GameLoopManager gameLoopManager;
    [SerializeField] private PlayerStateMachine p1StateMachine;
    [SerializeField] private PlayerStateMachine p2StateMachine;

    private Queue<string> p1InputLogQueue = new Queue<string>();
    private Queue<string> p2InputLogQueue = new Queue<string>();
    private int maxLogCount = 10;

    private bool showServer = true;
    private bool showP1 = true;
    private bool showP2 = true;

    private void Start()
    {
        TryConnectStateMachines();
    }

    private void TryConnectStateMachines()
    {
        if (p1StateMachine != null && p2StateMachine != null) return;
        if (gameLoopManager == null) return;
        
        if (p1StateMachine == null)
        {
            p1StateMachine = gameLoopManager.GetPlayerOneStateMachine();
        }

        if (p2StateMachine == null)
        {
            p2StateMachine = gameLoopManager.GetPlayerTwoStateMachine();
        }
    }

    private void OnGUI()
    {
        if (gameLoopManager == null) return;

        if (p1StateMachine == null || p2StateMachine == null)
        {
            TryConnectStateMachines();
        }

        float currentY = 10f;

        string serverTitle = showServer ? "▼ Server Status" : "▶ Server Status";
        if (GUI.Button(new Rect(10, currentY, 280, 20), serverTitle))
        {
            showServer = !showServer;
        }
        currentY += 20f;

        if (showServer)
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

        if (p1StateMachine != null)
        {
            string p1Title = showP1 ? "▼ P1 Input & Combo Debug" : "▶ P1 Input & Combo Debug";
            if (GUI.Button(new Rect(10, currentY, 280, 20), p1Title))
            {
                showP1 = !showP1;
            }
            currentY += 20f;

            if (showP1)
            {
                currentY = DrawInputDebugContent(p1StateMachine, p1InputLogQueue, currentY);
            }
            else
            {
                currentY += 5f;
            }
        }

        if (p2StateMachine != null)
        {
            string p2Title = showP2 ? "▼ P2 Input & Combo Debug" : "▶ P2 Input & Combo Debug";
            if (GUI.Button(new Rect(10, currentY, 280, 20), p2Title))
            {
                showP2 = !showP2;
            }
            currentY += 20f;

            if (showP2)
            {
                currentY = DrawInputDebugContent(p2StateMachine, p2InputLogQueue, currentY);
            }
        }
    }

    private float DrawInputDebugContent(PlayerStateMachine sm, Queue<string> logQueue, float startY)
    {
        float panelHeight = 310f;
        GUI.Box(new Rect(10, startY, 280, panelHeight), "");

        InputFlags currentHold = sm.currentInput.flags;
        GUI.Label(new Rect(20, startY + 10, 260, 20), $"[Current Hold]: {InputFlagsToString(currentHold)}");

        InputFlags keyDown = sm.GetKeyDownFlags();
        if (keyDown != InputFlags.None)
        {
            string logEntry = $"Tick {gameLoopManager.GetCurrentTick(),-5} | {InputFlagsToString(keyDown)}";
            logQueue.Enqueue(logEntry);

            if (logQueue.Count > maxLogCount)
            {
                logQueue.Dequeue();
            }
        }

        GUI.Label(new Rect(20, startY + 40, 260, 20), "--- Input Key Log ---");

        float yOffset = startY + 60;
        foreach (string log in logQueue)
        {
            GUI.Label(new Rect(20, yOffset, 260, 20), log);
            yOffset += 15;
        }

        yOffset = startY + 220;
        GUI.Label(new Rect(20, yOffset, 260, 20), "--- Active Combo Sequence ---");
        yOffset += 20;

        List<InputFlags> comboSeq = sm.GetComboSequence();
        List<string> formattedCombo = new List<string>();
        foreach (var flag in comboSeq)
        {
            formattedCombo.Add(InputFlagsToString(flag));
        }
        
        string comboString = formattedCombo.Count > 0 ? string.Join(" -> ", formattedCombo) : "Empty (Idle/Broken)";

        GUIStyle multilineStyle = new GUIStyle(GUI.skin.label);
        multilineStyle.wordWrap = true;
        GUI.Label(new Rect(20, yOffset, 260, 60), comboString, multilineStyle);

        return startY + panelHeight + 10f;
    }

    private string InputFlagsToString(InputFlags flags)
    {
        if (flags == InputFlags.None) return "None";

        string dirStr = "";
        bool up = (flags & InputFlags.Up) != 0;
        bool down = (flags & InputFlags.Down) != 0;
        bool left = (flags & InputFlags.Left) != 0;
        bool right = (flags & InputFlags.Right) != 0;

        if (up && down) dirStr = "↕";
        else if (left && right) dirStr = "↔";
        else if (up && right) dirStr = "↗";
        else if (up && left) dirStr = "↖";
        else if (down && right) dirStr = "↘";
        else if (down && left) dirStr = "↙";
        else if (up) dirStr = "↑";
        else if (down) dirStr = "↓";
        else if (left) dirStr = "←";
        else if (right) dirStr = "→";

        string atkStr = "";
        if ((flags & InputFlags.LightAttack) != 0) atkStr += "L";
        if ((flags & InputFlags.HeavyAttack) != 0) atkStr += (atkStr.Length > 0 ? " + R" : "R");

        if (!string.IsNullOrEmpty(dirStr) && !string.IsNullOrEmpty(atkStr))
        {
            return $"{dirStr} + {atkStr}";
        }
        else if (!string.IsNullOrEmpty(dirStr))
        {
            return dirStr;
        }
        else if (!string.IsNullOrEmpty(atkStr))
        {
            return atkStr;
        }

        return flags.ToString(); 
    }
}