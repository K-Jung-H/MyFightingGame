using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public class PlayerSelectContext
{
    public Color cursorColor = Color.white;
    public Transform displayTransform;
    public Image illustrationImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statusText;
    public GameObject lockIconObject;
    public bool isMirrored;

    [HideInInspector] public int currentIndex;
    [HideInInspector] public bool isLocked;
    [HideInInspector] public InputBinding inputBinding;
    [HideInInspector] public GameObject currentModel;
    [HideInInspector] public int lastIdleIndex = -1;
    [HideInInspector] public Coroutine loadCoroutine;
}

public class CharacterSelectManager : MonoBehaviour
{
    public Transform characterGridPanel;
    public CharacterSelectDataSO[] characterRoster;
    public RuntimeAnimatorController sharedSelectAnimator;
    public int maxRandomIdles = 3;
    public float modelLoadDelay = 0.2f;

    public PlayerSelectContext leftContext;
    public PlayerSelectContext rightContext;

    public TextMeshProUGUI countdownText;
    public GameObject startButtonObject;
    public TextMeshProUGUI startButtonText;
    public Color normalStartColor = Color.white;
    public Color pressedStartColor = Color.yellow;

    public Button changeSideButton;

    private CharacterSelectTile[] gridTiles;
    private int gridColumns;
    private int character3DLayer;

    public bool isLobbyReady { get; private set; }
    private bool isLocalCountdownActive;
    private float localCountdownTimer;
    private int lastDisplayedCountdown = -1;

    public bool isStartButtonReady { get; private set; }
    public bool isStartRequestSent { get; private set; }

    private ICharacterSelectLogic currentLogic;

    private void Start()
    {
        leftContext.isLocked = false;
        rightContext.isLocked = false;

        leftContext.currentIndex = Mathf.Clamp(leftContext.currentIndex, 0, characterRoster.Length - 1);
        rightContext.currentIndex = Mathf.Clamp(rightContext.currentIndex, 0, characterRoster.Length - 1);

        if (countdownText != null) countdownText.text = "";
        if (startButtonObject != null) startButtonObject.SetActive(false);
        if (changeSideButton != null) changeSideButton.gameObject.SetActive(false);

        character3DLayer = LayerMask.NameToLayer("Character3D");
        gridTiles = characterGridPanel.GetComponentsInChildren<CharacterSelectTile>();

        GridLayoutGroup gridLayout = characterGridPanel.GetComponent<GridLayoutGroup>();
        gridColumns = (gridLayout != null && gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount) 
            ? gridLayout.constraintCount : 7;

        for (int i = 0; i < gridTiles.Length; i++)
        {
            if (i < characterRoster.Length)
            {
                gridTiles[i].SetupTile(characterRoster[i].portraitSprite);
            }
            UpdateTileVisual(i);
        }

        leftContext.inputBinding = InputBinding.GetDefaultP1();
        rightContext.inputBinding = InputBinding.GetDefaultP2();

        UpdateSpecificTiles(leftContext.currentIndex, leftContext.currentIndex);
        UpdateSpecificTiles(rightContext.currentIndex, rightContext.currentIndex);
        UpdateCharacterDisplay(leftContext);
        UpdateCharacterDisplay(rightContext);
        UpdateLockUI(leftContext);
        UpdateLockUI(rightContext);

        BattleType currentBattle = GameFlowManager.Instance.currentBattleType;
        
        if (currentBattle == BattleType.Training) currentLogic = new TrainingSelectLogic();
        else if (currentBattle == BattleType.OnlineBattle) currentLogic = new OnlineSelectLogic();
        else currentLogic = new OfflineSelectLogic();

        currentLogic.Initialize(this);

        SubscribeToStateEvents();
        SyncInitialRoomState();
        isLobbyReady = true;
    }

    private void Update()
    {
        if (!isLobbyReady) return;

        UpdateCountdownUI();
        currentLogic.ProcessInput();
    }

    private void OnDestroy()
    {
        UnsubscribeFromStateEvents();
    }

    private void SubscribeToStateEvents()
    {
        if (RoomStateManager.Instance != null)
        {
            RoomStateManager.Instance.OnCharacterSelectUpdated += HandleCharacterSelectUpdated;
            RoomStateManager.Instance.OnCountdownUpdated += HandleCountdownUpdated;
            RoomStateManager.Instance.OnStartButtonActivated += HandleStartButtonActivated;
        }
    }

    private void UnsubscribeFromStateEvents()
    {
        if (RoomStateManager.Instance != null)
        {
            RoomStateManager.Instance.OnCharacterSelectUpdated -= HandleCharacterSelectUpdated;
            RoomStateManager.Instance.OnCountdownUpdated -= HandleCountdownUpdated;
            RoomStateManager.Instance.OnStartButtonActivated -= HandleStartButtonActivated;
        }
    }

    private void SyncInitialRoomState()
    {
        if (RoomStateManager.Instance != null && GameFlowManager.Instance.currentConnectionMode == ConnectionMode.OnlineClient)
        {
            RoomStateModel model = RoomStateManager.Instance.roomModel;
            HandleCharacterSelectUpdated(
                model.p1CharacterIndex, model.isP1CharacterLocked, model.p1PreferredSide,
                model.p2CharacterIndex, model.isP2CharacterLocked, model.p2PreferredSide
            );
        }
    }

    private void UpdateCountdownUI()
    {
        if (isLocalCountdownActive)
        {
            localCountdownTimer -= Time.deltaTime;
            
            if (localCountdownTimer <= 0f)
            {
                isLocalCountdownActive = false;
                
                if (GameFlowManager.Instance.currentConnectionMode == ConnectionMode.Offline)
                {
                    HandleStartButtonActivated();
                }
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
    }

    public void SetStartRequestSent()
    {
        isStartRequestSent = true;
        if (startButtonText != null) startButtonText.color = pressedStartColor;
    }

    public void EvaluateOfflineState()
    {
        if (leftContext.isLocked && rightContext.isLocked)
        {
            if (!isLocalCountdownActive && !isStartButtonReady)
            {
                HandleCountdownUpdated(true);
            }
        }
        else
        {
            HandleCountdownUpdated(false);
        }
    }

    private void HandleCharacterSelectUpdated(int p1Idx, bool p1Lock, int p1Side, int p2Idx, bool p2Lock, int p2Side)
    {
        currentLogic.OnStateUpdatedFromServer(p1Idx, p1Lock, p1Side, p2Idx, p2Lock, p2Side);
    }

    private void HandleCountdownUpdated(bool isStarted)
    {
        isLocalCountdownActive = isStarted;
        isStartButtonReady = false;
        lastDisplayedCountdown = -1;

        if (isStarted)
        {
            localCountdownTimer = 3f;
        }

        if (countdownText != null && !isStarted) countdownText.text = "";
        if (startButtonObject != null) startButtonObject.SetActive(false);
    }

    private void HandleStartButtonActivated()
    {
        isLocalCountdownActive = false;
        isStartButtonReady = true;

        if (countdownText != null) countdownText.text = "";
        if (startButtonObject != null) startButtonObject.SetActive(true);
        if (startButtonText != null) startButtonText.color = normalStartColor;
    }

    public void SaveCharacterData(int playerId, int index)
    {
        if (playerId == 1) MatchDataManager.P1CharacterData = characterRoster[index].inGameData;
        else if (playerId == 2) MatchDataManager.P2CharacterData = characterRoster[index].inGameData;
    }

    public bool GetSelectInput(PlayerSelectContext context)
    {
        if (context.inputBinding.selectKey == Key.None) return false;
        return Keyboard.current[context.inputBinding.selectKey].wasPressedThisFrame;
    }

    public bool GetOfflineLockInput(PlayerSelectContext context)
    {
        bool isLpPressed = context.inputBinding.lpKey != Key.None && Keyboard.current[context.inputBinding.lpKey].wasPressedThisFrame;
        bool isRpPressed = context.inputBinding.rpKey != Key.None && Keyboard.current[context.inputBinding.rpKey].wasPressedThisFrame;
        return isLpPressed || isRpPressed;
    }

    public bool GetOfflineUnlockInput(PlayerSelectContext context)
    {
        bool isLkPressed = context.inputBinding.lkKey != Key.None && Keyboard.current[context.inputBinding.lkKey].wasPressedThisFrame;
        bool isRkPressed = context.inputBinding.rkKey != Key.None && Keyboard.current[context.inputBinding.rkKey].wasPressedThisFrame;
        return isLkPressed || isRkPressed;
    }

    public int GetMovementInput(PlayerSelectContext context)
    {
        if (context.inputBinding.leftKey != Key.None && Keyboard.current[context.inputBinding.leftKey].wasPressedThisFrame) return -1;
        if (context.inputBinding.rightKey != Key.None && Keyboard.current[context.inputBinding.rightKey].wasPressedThisFrame) return 1;
        return 0;
    }

    public void UpdateLockUI(PlayerSelectContext context)
    {
        if (context.statusText != null) context.statusText.text = context.isLocked ? "Ready" : "Selecting";
        if (context.lockIconObject != null) context.lockIconObject.SetActive(context.isLocked);
    }

    public void UpdateSpecificTiles(int oldIndex, int newIndex)
    {
        UpdateTileVisual(oldIndex);
        UpdateTileVisual(newIndex);
    }

    private void UpdateTileVisual(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= gridTiles.Length) return;
        
        bool isLeftVisible = leftContext.illustrationImage != null && leftContext.illustrationImage.gameObject.activeSelf;
        bool isRightVisible = rightContext.illustrationImage != null && rightContext.illustrationImage.gameObject.activeSelf;

        bool isLeft = isLeftVisible && (targetIndex == leftContext.currentIndex);
        bool isRight = isRightVisible && (targetIndex == rightContext.currentIndex);
        
        gridTiles[targetIndex].UpdateVisuals(isLeft, isRight, leftContext.cursorColor, rightContext.cursorColor);
    }

    public void UpdateCharacterDisplay(PlayerSelectContext context)
    {
        CharacterSelectDataSO data = characterRoster[context.currentIndex];
        
        if (context.illustrationImage != null)
        {
            context.illustrationImage.sprite = data.fullBodySprite;
            ApplyPivotAndSize(context.illustrationImage, data.fullBodySprite, context.isMirrored);
        }
        
        if (context.nameText != null) context.nameText.text = data.characterName;
        
        if (context.loadCoroutine != null) StopCoroutine(context.loadCoroutine);
        context.loadCoroutine = StartCoroutine(SpawnModelRoutine(context, context.currentIndex));
    }

    public void SetContextVisibility(PlayerSelectContext context, bool visible)
    {
        if (context.illustrationImage != null) context.illustrationImage.gameObject.SetActive(visible);
        if (context.nameText != null) context.nameText.gameObject.SetActive(visible);
        if (context.statusText != null) context.statusText.gameObject.SetActive(visible);
        if (context.lockIconObject != null) context.lockIconObject.SetActive(visible && context.isLocked);
        
        if (context.currentModel != null) context.currentModel.SetActive(visible);
        
        UpdateSpecificTiles(context.currentIndex, context.currentIndex);
    }

    private IEnumerator SpawnModelRoutine(PlayerSelectContext context, int targetIndex)
    {
        yield return new WaitForSeconds(modelLoadDelay);
        if (context.currentIndex != targetIndex) yield break;
        if (context.currentModel != null) Destroy(context.currentModel);

        CharacterSelectDataSO data = characterRoster[targetIndex];
        if (data.modelPrefab != null)
        {
            context.currentModel = Instantiate(data.modelPrefab, context.displayTransform.position, context.displayTransform.rotation, context.displayTransform);
            SetLayerRecursively(context.currentModel, character3DLayer);
            
            bool isVisible = (context.illustrationImage != null && context.illustrationImage.gameObject.activeSelf);
            context.currentModel.SetActive(isVisible);

            if (isVisible)
            {
                Animator anim = context.currentModel.GetComponentInChildren<Animator>();
                if (anim != null && sharedSelectAnimator != null)
                {
                    anim.runtimeAnimatorController = sharedSelectAnimator;
                    anim.SetBool("IsMirrored", context.isMirrored);
                    context.lastIdleIndex = (maxRandomIdles > 1) ? UnityEngine.Random.Range(0, maxRandomIdles) : 0;
                    anim.Play("Selecting_Idle_" + context.lastIdleIndex, 0, 0f);
                }
            }
        }
    }

    private void ApplyPivotAndSize(Image img, Sprite sprite, bool isMirrored)
    {
        if (img == null || sprite == null) return;

        RectTransform rt = img.rectTransform;
        RectTransform parentRt = rt.parent.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);

        float normalizedPivotX = sprite.pivot.x / sprite.rect.width;
        float normalizedPivotY = sprite.pivot.y / sprite.rect.height;
        rt.pivot = new Vector2(normalizedPivotX, normalizedPivotY);

        float parentHeight = parentRt.rect.height;
        float actualHeight = parentHeight - 30f;
        float targetWidth = actualHeight * (sprite.rect.width / sprite.rect.height);

        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

        rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
        rt.offsetMax = new Vector2(rt.offsetMax.x, -30f);

        rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);

        Vector3 scale = rt.localScale;
        scale.x = isMirrored ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        rt.localScale = scale;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, layer);
    }
}