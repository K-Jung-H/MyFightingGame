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

    private bool isShowAllHUD = false;
    private bool isShowServer = false;
    private bool isShowNetworkDetails = false;
    private bool isShowP1 = false;
    private bool isShowP2 = false;

    private int p1CurrentHealth;
    private int p1MaxHealth;
    private int p2CurrentHealth;
    private int p2MaxHealth;

    private bool isP1HealthBound = false;
    private bool isP2HealthBound = false;

    private void Start()
    {
        bool isOnline = GameFlowManager.Instance.currentMode != ConnectionMode.Offline;
        isShowAllHUD = isOnline;
        isShowServer = isOnline;
        
        TryConnectControllers();
    }

    private void OnDestroy()
    {
        bool hasP1Combat = p1Controller != null && p1Controller.GetCombat() != null;
        if (hasP1Combat)
        {
            p1Controller.GetCombat().OnHealthChanged -= UpdateP1Health;
        }

        bool hasP2Combat = p2Controller != null && p2Controller.GetCombat() != null;
        if (hasP2Combat)
        {
            p2Controller.GetCombat().OnHealthChanged -= UpdateP2Health;
        }
    }

    private void OnGUI()
    {
        Vector2 refRes = GameFlowManager.Instance.GetReferenceResolution();
        Vector3 scale = new Vector3(Screen.width / refRes.x, Screen.height / refRes.y, 1f);
        float minScale = Mathf.Min(scale.x, scale.y);
        scale = new Vector3(minScale, minScale, 1f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);

        isShowAllHUD = GUI.Toggle(new Rect(10f, 250f, 150f, 20f), isShowAllHUD, " Toggle All Debug HUD");

        if (!isShowAllHUD) return;

        bool isManagerMissing = gameLoopManager == null;
        if (isManagerMissing) return;

        bool isAnyControllerMissing = p1Controller == null || p2Controller == null;
        if (isAnyControllerMissing)
        {
            TryConnectControllers();
        }

        float panelWidth = 280f;
        float bottomY = refRes.y - 30f;

        float p1StartX = 10f;
        float serverStartX = (refRes.x - panelWidth) / 2f;
        float p2StartX = refRes.x - panelWidth - 10f;

        DrawServerStatusPanel(serverStartX, bottomY, panelWidth);
        DrawP1DebugPanel(p1StartX, bottomY, panelWidth);
        DrawP2DebugPanel(p2StartX, bottomY, panelWidth);
    }

    /*
     * 씬 내의 게임 매니저와 플레이어 컨트롤러를 찾아 디버그 HUD에 연결합니다.
     */
    private void TryConnectControllers()
    {
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

        bool canBindP1 = p1Controller != null && !isP1HealthBound;
        if (canBindP1)
        {
            PlayerCombat p1Combat = p1Controller.GetCombat();
            p1Combat.OnHealthChanged += UpdateP1Health;

            UpdateP1Health(p1Combat.GetCurrentHealth(), p1Combat.GetMaxHealth());

            isP1HealthBound = true;
        }

        bool canBindP2 = p2Controller != null && !isP2HealthBound;
        if (canBindP2)
        {
            PlayerCombat p2Combat = p2Controller.GetCombat();
            p2Combat.OnHealthChanged += UpdateP2Health;

            UpdateP2Health(p2Combat.GetCurrentHealth(), p2Combat.GetMaxHealth());

            isP2HealthBound = true;
        }
    }

    /*
     * 플레이어 1의 체력 정보를 갱신하여 캐싱합니다.
     */
    private void UpdateP1Health(int current, int max)
    {
        p1CurrentHealth = current;
        p1MaxHealth = max;
    }

    /*
     * 플레이어 2의 체력 정보를 갱신하여 캐싱합니다.
     */
    private void UpdateP2Health(int current, int max)
    {
        p2CurrentHealth = current;
        p2MaxHealth = max;
    }

    /*
     * 화면 중앙 하단에 네트워크 및 서버 상태 정보를 렌더링합니다.
     */
    private void DrawServerStatusPanel(float startX, float bottomY, float width)
    {
        string serverTitle = isShowServer ? "▼ Server Status" : "▲ Server Status";
        if (GUI.Button(new Rect(startX, bottomY, width, 20), serverTitle))
        {
            isShowServer = !isShowServer;
        }

        if (isShowServer)
        {
            float boxHeight = isShowNetworkDetails ? 210f : 160f;
            float contentY = bottomY - boxHeight;
            
            GUI.Box(new Rect(startX, contentY, width, boxHeight), "");

            float currentY = contentY + 10f;
            GUI.Label(new Rect(startX + 10, currentY, width - 20, 20), $"Current Tick: {gameLoopManager.GetCurrentTick()}");
            currentY += 25f;

            currentY = DrawNetworkStateSection(startX, currentY, width);

            GUI.Label(new Rect(startX + 10, currentY, width - 20, 20), $"P1 State: {gameLoopManager.GetP1State()}");
            GUI.Label(new Rect(startX + 10, currentY + 15, width - 20, 20), $"P1 Pos: {gameLoopManager.GetP1Pos()}");
            GUI.Label(new Rect(startX + 10, currentY + 30, width - 20, 20), $"P1 HP: {p1CurrentHealth} / {p1MaxHealth}");
            currentY += 50f;

            GUI.Label(new Rect(startX + 10, currentY, width - 20, 20), $"P2 State: {gameLoopManager.GetP2State()}");
            GUI.Label(new Rect(startX + 10, currentY + 15, width - 20, 20), $"P2 Pos: {gameLoopManager.GetP2Pos()}");
            GUI.Label(new Rect(startX + 10, currentY + 30, width - 20, 20), $"P2 HP: {p2CurrentHealth} / {p2MaxHealth}");
        }
    }

    /*
     * 세부 네트워크 상태(연결 여부, 락스텝 스톨, 디싱크)를 렌더링합니다.
     */
    private float DrawNetworkStateSection(float startX, float currentY, float width)
    {
        string netTitle = isShowNetworkDetails ? "▼ Network State" : "▶ Network State";
        if (GUI.Button(new Rect(startX + 10, currentY, width - 20, 18), netTitle))
        {
            isShowNetworkDetails = !isShowNetworkDetails;
        }
        currentY += 20f;

        if (isShowNetworkDetails)
        {
            P2PNetworkManager p2pManager = Object.FindFirstObjectByType<P2PNetworkManager>();
            bool isConnected = p2pManager != null && p2pManager.GetIsConnected();

            GUI.color = isConnected ? Color.green : Color.red;
            GUI.Label(new Rect(startX + 20, currentY, width - 30, 20), $"Connection: {(isConnected ? "Connected" : "Disconnected")}");
            currentY += 15f;

            bool isHardStalling = gameLoopManager.GetIsHardStalling();
            bool isSoftStalling = gameLoopManager.GetIsSoftStalling();

            string lockstepStatus = "Running";
            GUI.color = Color.green;

            if (isHardStalling)
            {
                lockstepStatus = "HARD STALLED";
                GUI.color = Color.red;
            }
            else if (isSoftStalling)
            {
                lockstepStatus = "SOFT STALLED";
                GUI.color = Color.yellow;
            }

            GUI.Label(new Rect(startX + 20, currentY, width - 30, 20), $"Lockstep: {lockstepStatus}");
            currentY += 15f;

            bool isDesync = gameLoopManager.GetIsDesyncDetected();
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

    /*
     * 화면 좌측 하단에 플레이어 1의 입력 로그 및 액션 디버그 패널을 렌더링합니다.
     */
    private void DrawP1DebugPanel(float startX, float bottomY, float width)
    {
        bool hasP1Controller = p1Controller != null;
        if (!hasP1Controller) return;

        string p1Title = isShowP1 ? "▼ P1 Input & Action Debug" : "▲ P1 Input & Action Debug";
        if (GUI.Button(new Rect(startX, bottomY, width, 20), p1Title))
        {
            isShowP1 = !isShowP1;
        }

        if (isShowP1)
        {
            float panelHeight = 350f;
            DrawInputDebugContent(p1Controller, p1InputLogQueue, startX, bottomY - panelHeight, width, panelHeight);
        }
    }

    /*
     * 화면 우측 하단에 플레이어 2의 입력 로그 및 액션 디버그 패널을 렌더링합니다.
     */
    private void DrawP2DebugPanel(float startX, float bottomY, float width)
    {
        bool hasP2Controller = p2Controller != null;
        if (!hasP2Controller) return;

        string p2Title = isShowP2 ? "▼ P2 Input & Action Debug" : "▲ P2 Input & Action Debug";
        if (GUI.Button(new Rect(startX, bottomY, width, 20), p2Title))
        {
            isShowP2 = !isShowP2;
        }

        if (isShowP2)
        {
            float panelHeight = 350f;
            DrawInputDebugContent(p2Controller, p2InputLogQueue, startX, bottomY - panelHeight, width, panelHeight);
        }
    }

    /*
     * 컨트롤러의 현재 입력, 액션, 콤보 시퀀스를 문자열로 변환하여 GUI에 출력합니다.
     */
    private void DrawInputDebugContent(PlayerController controller, Queue<string> logQueue, float startX, float startY, float width, float panelHeight)
    {
        GUI.Box(new Rect(startX, startY, width, panelHeight), "");

        InputFlags currentHold = controller.currentInput.flags;
        GUI.Label(new Rect(startX + 10, startY + 10, width - 20, 20), $"[Current Hold]: {InputFlagsToString(currentHold)}");

        ActionDataSO currentAction = controller.GetStateMachine().GetCurrentActionData();
        bool hasCurrentAction = currentAction != null;
        string actionName = hasCurrentAction ? currentAction.animationStateName : "None";
        GUI.Label(new Rect(startX + 10, startY + 35, width - 20, 20), $"[Current Action]: {actionName}");

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

        GUI.Label(new Rect(startX + 10, startY + 65, width - 20, 20), "--- Input Key Log ---");

        float yOffset = startY + 85;
        foreach (string log in logQueue)
        {
            GUI.Label(new Rect(startX + 10, yOffset, width - 20, 20), log);
            yOffset += 15;
        }

        yOffset = startY + 245;
        GUI.Label(new Rect(startX + 10, yOffset, width - 20, 20), "--- Active Combo Sequence ---");
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
        GUI.Label(new Rect(startX + 10, yOffset, width - 20, 60), comboString, multilineStyle);
    }

    /*
     * 비트 플래그 형태의 키 입력을 읽기 쉬운 기호 및 문자열로 포맷팅합니다.
     */
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