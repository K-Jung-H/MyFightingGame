using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class StagePlayerContext
{
    public SelectStatusUI statusUI;
    [HideInInspector] public int currentIndex;
    [HideInInspector] public bool isLocked;
    [HideInInspector] public InputBinding inputBinding;
}

public class StageSelectManager : MonoBehaviour
{
    [Header("UI References")]
    public Image canvasBackgroundImage;
    public Button buttonReturn;
    public TextMeshProUGUI countdownText;

    [Header("Stage Grid Config")]
    public Transform stageGridPanel;
    public GameObject stagePortraitPrefab;
    public GameStageDataSO[] stageRoster;

    [Header("Player Contexts")]
    public StagePlayerContext p1Context;
    public StagePlayerContext p2Context;

    public bool isLobbyReady { get; private set; }
    public bool isLocalCountdownActive { get; private set; }
    
    private float localCountdownTimer;
    private int lastDisplayedCountdown = -1;
    private const int TotalPortraitCount = 10;
    private StagePortraitUI[] stageTiles;
    private IStageSelectLogic currentLogic;

    private void Start()
    {
        InitializeManager();
    }

    private void Update()
    {
        if (!isLobbyReady) return;

        UpdateCountdownUI();
        currentLogic.ProcessInput();
    }

    private void InitializeManager()
    {
        p1Context.inputBinding = InputBinding.GetDefaultP1();
        p2Context.inputBinding = InputBinding.GetDefaultP2();

        if (buttonReturn != null) buttonReturn.onClick.AddListener(HandleReturnToCharacterSelect);

        GenerateStageGrid();

        BattleType currentBattle = GameFlowManager.Instance.currentBattleType;
        if (currentBattle == BattleType.Training) currentLogic = new TrainingStageSelectLogic();
        else if (currentBattle == BattleType.OnlineBattle) currentLogic = new OnlineStageSelectLogic();
        else currentLogic = new OfflineStageSelectLogic();

        currentLogic.Initialize(this);
        UpdateAllVisuals();

        isLobbyReady = true;
    }

    public int GetMovementInput(StagePlayerContext context)
    {
        if (context.inputBinding.leftKey != Key.None && Keyboard.current[context.inputBinding.leftKey].wasPressedThisFrame) return -1;
        if (context.inputBinding.rightKey != Key.None && Keyboard.current[context.inputBinding.rightKey].wasPressedThisFrame) return 1;
        return 0;
    }

    public bool GetOfflineLockInput(StagePlayerContext context)
    {
        bool isLpPressed = context.inputBinding.lpKey != Key.None && Keyboard.current[context.inputBinding.lpKey].wasPressedThisFrame;
        bool isRpPressed = context.inputBinding.rpKey != Key.None && Keyboard.current[context.inputBinding.rpKey].wasPressedThisFrame;
        return isLpPressed || isRpPressed;
    }

    public bool GetOfflineUnlockInput(StagePlayerContext context)
    {
        bool isLkPressed = context.inputBinding.lkKey != Key.None && Keyboard.current[context.inputBinding.lkKey].wasPressedThisFrame;
        bool isRkPressed = context.inputBinding.rkKey != Key.None && Keyboard.current[context.inputBinding.rkKey].wasPressedThisFrame;
        return isLkPressed || isRkPressed;
    }

    public void MoveCursor(StagePlayerContext context, int direction)
    {
        if (stageRoster == null || stageRoster.Length == 0) return;

        context.currentIndex += direction;

        if (context.currentIndex < 0)
        {
            context.currentIndex = stageRoster.Length - 1;
        }
        else if (context.currentIndex >= stageRoster.Length)
        {
            context.currentIndex = 0;
        }

        UpdateAllVisuals();
    }

    public void LockSelection(StagePlayerContext context)
    {
        context.isLocked = true;
        UpdateAllVisuals();
    }

    public void UnlockSelection(StagePlayerContext context)
    {
        context.isLocked = false;
        UpdateAllVisuals();
    }

    public void StartCountdown()
    {
        isLocalCountdownActive = true;
        localCountdownTimer = 3f;
    }

    public void StopCountdown()
    {
        isLocalCountdownActive = false;
        if (countdownText != null) countdownText.text = "";
    }

    private void UpdateCountdownUI()
    {
        if (!isLocalCountdownActive) return;

        localCountdownTimer -= Time.deltaTime;

        if (localCountdownTimer <= 0f)
        {
            isLocalCountdownActive = false;
            int selectedIndex = p1Context.isLocked ? p1Context.currentIndex : p2Context.currentIndex;
            MatchDataManager.SelectedStageData = stageRoster[selectedIndex];
            GameFlowManager.Instance.ChangeScene(GameSceneType.GamePlay);
        }
        else if (countdownText != null)
        {
            int currentSeconds = Mathf.CeilToInt(localCountdownTimer);
            if (currentSeconds != lastDisplayedCountdown)
            {
                lastDisplayedCountdown = currentSeconds;
                countdownText.text = currentSeconds.ToString();
            }
        }
    }

    private void GenerateStageGrid()
    {
        if (stageGridPanel == null || stagePortraitPrefab == null) return;

        stageTiles = new StagePortraitUI[TotalPortraitCount];
        for (int i = 0; i < TotalPortraitCount; i++)
        {
            GameObject spawnObject = Instantiate(stagePortraitPrefab, stageGridPanel);
            StagePortraitUI portraitUI = spawnObject.GetComponent<StagePortraitUI>();
            
            if (portraitUI != null)
            {
                if (stageRoster != null && i < stageRoster.Length)
                {
                    portraitUI.SetupPortrait(stageRoster[i]);
                }
                else
                {
                    portraitUI.SetupEmptyPortrait();
                }
                stageTiles[i] = portraitUI;
            }
        }
    }

    public void UpdateAllVisuals()
    {
        if (stageTiles == null) return;

        bool isP1Active = currentLogic.IsPlayerActive(1);
        bool isP2Active = currentLogic.IsPlayerActive(2);

        if (p1Context.statusUI != null) p1Context.statusUI.gameObject.SetActive(isP1Active);
        if (p2Context.statusUI != null) p2Context.statusUI.gameObject.SetActive(isP2Active);

        for (int i = 0; i < stageTiles.Length; i++)
        {
            if (stageTiles[i] == null) continue;
            bool isP1Selected = isP1Active && (i == p1Context.currentIndex);
            bool isP2Selected = isP2Active && (i == p2Context.currentIndex);
            stageTiles[i].SetSelectionHighlight(isP1Selected, isP2Selected);
        }

        currentLogic.UpdateBackground();

        if (isP1Active && p1Context.statusUI != null && stageRoster.Length > 0)
            p1Context.statusUI.UpdateStatus(stageRoster[p1Context.currentIndex].thumbnail, p1Context.isLocked);
            
        if (isP2Active && p2Context.statusUI != null && stageRoster.Length > 0)
            p2Context.statusUI.UpdateStatus(stageRoster[p2Context.currentIndex].thumbnail, p2Context.isLocked);
    }

    private void HandleReturnToCharacterSelect()
    {
        MatchDataManager.P1CharacterData = null;
        MatchDataManager.P2CharacterData = null;
        GameFlowManager.Instance.ChangeScene(GameSceneType.CharacterSelect);
    }
}