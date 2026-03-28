using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public static class MatchDataManager
{
    public static CharacterDataSO P1CharacterData { get; set; }
    public static CharacterDataSO P2CharacterData { get; set; }
}

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

    public PlayerSelectContext p1Context;
    public PlayerSelectContext p2Context;

    [Header("Start Sequence UI")]
    public TextMeshProUGUI countdownText;
    public GameObject startButtonObject;
    public TextMeshProUGUI startButtonText;
    public Color normalStartColor = Color.white;
    public Color pressedStartColor = Color.yellow;

    private CharacterSelectTile[] gridTiles;
    private int gridColumns;
    private int character3DLayer;
    private bool isMatchStarted;
    private bool isLobbyReady = false;
    private bool isEventsSubscribed = false;
    private bool isLocalCountdownActive;
    private float localCountdownTimer;
    private bool isStartButtonReady;
    private int lastDisplayedCountdown = -1;

    private Action OnConnectionEstablishedAction;
    private Action<int, bool, int, int, bool, int> OnSelectBroadcastAction;

    private void Start()
    {
        if (countdownText != null)
        {
            countdownText.text = "";
        }

        if (startButtonObject != null)
        {
            startButtonObject.SetActive(false);
        }
        
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

        p1Context.inputBinding = InputBinding.GetDefaultP1();
        p2Context.inputBinding = InputBinding.GetDefaultP2();

        UpdateSpecificTiles(p1Context.currentIndex, p1Context.currentIndex);
        UpdateSpecificTiles(p2Context.currentIndex, p2Context.currentIndex);
        UpdateCharacterDisplay(p1Context);
        UpdateCharacterDisplay(p2Context);
        UpdateLockUI(p1Context);
        UpdateLockUI(p2Context);
    }

    private void Update()
    {
        ConnectionMode currentMode = GameFlowManager.Instance.currentMode;
        
        UpdateNetworkState(currentMode);

        if (!isEventsSubscribed)
        {
            if (currentMode == ConnectionMode.None) return;

            SubscribeToFlowEvents(currentMode);
            isEventsSubscribed = true;
        }

        IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
        if (session != null)
        {
            session.UpdateSession(Time.deltaTime);
        }

        UpdateCountdownUI();

        if (isMatchStarted || !isLobbyReady) return;

        if (currentMode == ConnectionMode.Offline)
        {
            ProcessOfflineModeInput();
        }
        else if (currentMode == ConnectionMode.OnlineClient)
        {
            ProcessOnlineModeInput(session);
        }
    }

    private void OnDestroy()
    {
        IMatchSession session = GameFlowManager.Instance?.GetCurrentSession();
        if (session != null)
        {
            session.OnCountdownUpdate -= HandleCountdownUpdate;
            session.OnStartButtonActive -= HandleStartButtonActive;
            session.OnSceneChange -= HandleSceneChangeCommand;
        }

        if (NetworkSessionManager.Instance != null)
        {
            if (OnConnectionEstablishedAction != null)
                NetworkSessionManager.Instance.OnConnectionEstablished -= OnConnectionEstablishedAction;
            if (OnSelectBroadcastAction != null)
                NetworkSessionManager.Instance.OnSelectBroadcastReceived -= OnSelectBroadcastAction;
        }
    }

    private void UpdateNetworkState(ConnectionMode mode)
    {
        if (mode == ConnectionMode.OnlineClient)
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.UpdateNetwork();
            }
        }
    }

    private void SubscribeToFlowEvents(ConnectionMode mode)
    {
        IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
        if (session != null)
        {
            session.OnCountdownUpdate += HandleCountdownUpdate;
            session.OnStartButtonActive += HandleStartButtonActive;
            session.OnSceneChange += HandleSceneChangeCommand;
        }

        if (mode == ConnectionMode.Offline || mode == ConnectionMode.OnlineClient)
        {
            isLobbyReady = true;

            if (mode == ConnectionMode.OnlineClient && NetworkSessionManager.Instance != null)
            {
                OnSelectBroadcastAction = HandleNetworkSelectBroadcast;
                NetworkSessionManager.Instance.OnSelectBroadcastReceived += OnSelectBroadcastAction;
            }
        }
    }

    private void UpdateCountdownUI()
    {
        if (isLocalCountdownActive)
        {
            localCountdownTimer -= Time.deltaTime;
            if (countdownText != null)
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

    private void ProcessOfflineModeInput()
    {
        HandleLocalInput(1, p1Context);
        HandleLocalInput(2, p2Context);
    }

    private void ProcessOnlineModeInput(IMatchSession session)
    {
        if (session == null) return;

        int localSlot = session.GetLocalPlayerSlot();
        if (localSlot == 0)
        {
            HandleLocalInput(1, p1Context);
        }
        else if (localSlot == 1)
        {
            HandleLocalInput(2, p2Context);
        }
    }

    private void HandleLocalInput(int playerId, PlayerSelectContext context)
    {
        if (Keyboard.current == null) return;

        ConnectionMode currentMode = GameFlowManager.Instance.currentMode;
        bool isOfflineMode = currentMode == ConnectionMode.Offline;

        bool isSelectPressed = GetSelectInput(context);
        bool isLockInput = isOfflineMode ? GetOfflineLockInput(context) : isSelectPressed;
        bool isUnlockInput = isOfflineMode ? GetOfflineUnlockInput(context) : isSelectPressed;

        if (isStartButtonReady)
        {
            if (isSelectPressed)
            {
                if (startButtonText != null)
                {
                    startButtonText.color = pressedStartColor;
                }
                
                IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
                if (session != null)
                {
                    session.SendStartRequest(playerId);
                }
            }
            return;
        }

        if (!context.isLocked && isLockInput)
        {
            context.isLocked = true;
            UpdateLockUI(context);
            NotifyStateToManager(playerId, context);
        }
        else if (context.isLocked && isUnlockInput)
        {
            context.isLocked = false;
            UpdateLockUI(context);
            NotifyStateToManager(playerId, context);
        }

        if (context.isLocked) return;

        int move = GetMovementInput(context);
        if (move != 0)
        {
            int oldIndex = context.currentIndex;
            context.currentIndex = (context.currentIndex + move + characterRoster.Length) % characterRoster.Length;
            
            UpdateCharacterDisplay(context);
            UpdateSpecificTiles(oldIndex, context.currentIndex);
            NotifyStateToManager(playerId, context);
        }
    }

    private void NotifyStateToManager(int playerId, PlayerSelectContext context)
    {
        if (context.isLocked)
        {
            SaveCharacterData(playerId, context.currentIndex);
        }

        IMatchSession session = GameFlowManager.Instance.GetCurrentSession();
        if (session != null)
        {
            session.UpdateCharacterSelect(playerId, context.currentIndex, context.isLocked);
        }
    }

    private void HandleNetworkSelectBroadcast(int p1Idx, bool p1Lock, int p1Side, int p2Idx, bool p2Lock, int p2Side)
    {
        UpdateRemoteState(p1Context, p1Idx, p1Lock, 1);
        UpdateRemoteState(p2Context, p2Idx, p2Lock, 2);
    }

    private void UpdateRemoteState(PlayerSelectContext context, int newIdx, bool newLock, int playerId)
    {
        if (context.currentIndex != newIdx)
        {
            int oldIdx = context.currentIndex;
            context.currentIndex = newIdx;
            UpdateCharacterDisplay(context);
            UpdateSpecificTiles(oldIdx, newIdx);
        }

        if (context.isLocked != newLock)
        {
            context.isLocked = newLock;
            UpdateLockUI(context);
            
            if (newLock)
            {
                SaveCharacterData(playerId, newIdx);
            }
        }
    }

    private void SaveCharacterData(int playerId, int index)
    {
        if (playerId == 1) MatchDataManager.P1CharacterData = characterRoster[index].inGameData;
        else if (playerId == 2) MatchDataManager.P2CharacterData = characterRoster[index].inGameData;
    }

    private void HandleCountdownUpdate(bool isStarted)
    {
        isLocalCountdownActive = isStarted;
        localCountdownTimer = 3f;
        isStartButtonReady = false;
        lastDisplayedCountdown = -1;

        if (countdownText != null && !isStarted) 
        {
            countdownText.text = "";
        }
        
        if (startButtonObject != null)
        {
            startButtonObject.SetActive(false);
        }
    }

    private void HandleStartButtonActive()
    {
        isLocalCountdownActive = false;
        isStartButtonReady = true;

        if (countdownText != null) 
        {
            countdownText.text = "";
        }
        
        if (startButtonObject != null)
        {
            startButtonObject.SetActive(true);
        }
        
        if (startButtonText != null)
        {
            startButtonText.color = normalStartColor;
        }
    }

    private void HandleSceneChangeCommand()
    {
        if (isMatchStarted) return;
        isMatchStarted = true;
        GameFlowManager.Instance.OnReceiveSceneChangeCommand("GamePlayScene");
    }

    private bool GetSelectInput(PlayerSelectContext context)
    {
        if (context.inputBinding.selectKey == Key.None) return false;
        return Keyboard.current[context.inputBinding.selectKey].wasPressedThisFrame;
    }

    private bool GetOfflineLockInput(PlayerSelectContext context)
    {
        bool lpPressed = context.inputBinding.lpKey != Key.None && Keyboard.current[context.inputBinding.lpKey].wasPressedThisFrame;
        bool rpPressed = context.inputBinding.rpKey != Key.None && Keyboard.current[context.inputBinding.rpKey].wasPressedThisFrame;
        return lpPressed || rpPressed;
    }

    private bool GetOfflineUnlockInput(PlayerSelectContext context)
    {
        bool lkPressed = context.inputBinding.lkKey != Key.None && Keyboard.current[context.inputBinding.lkKey].wasPressedThisFrame;
        bool rkPressed = context.inputBinding.rkKey != Key.None && Keyboard.current[context.inputBinding.rkKey].wasPressedThisFrame;
        return lkPressed || rkPressed;
    }

    private int GetMovementInput(PlayerSelectContext context)
    {
        if (context.inputBinding.leftKey != Key.None && Keyboard.current[context.inputBinding.leftKey].wasPressedThisFrame) return -1;
        if (context.inputBinding.rightKey != Key.None && Keyboard.current[context.inputBinding.rightKey].wasPressedThisFrame) return 1;
        return 0;
    }

    private void UpdateLockUI(PlayerSelectContext context)
    {
        if (context.statusText != null) context.statusText.text = context.isLocked ? "Ready" : "Selecting";
        if (context.lockIconObject != null) context.lockIconObject.SetActive(context.isLocked);
    }

    private void UpdateSpecificTiles(int oldIndex, int newIndex)
    {
        UpdateTileVisual(oldIndex);
        UpdateTileVisual(newIndex);
    }

    private void UpdateTileVisual(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= gridTiles.Length) return;
        bool isP1 = (targetIndex == p1Context.currentIndex);
        bool isP2 = (targetIndex == p2Context.currentIndex);
        gridTiles[targetIndex].UpdateVisuals(isP1, isP2, p1Context.cursorColor, p2Context.cursorColor);
    }

    private void UpdateCharacterDisplay(PlayerSelectContext context)
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