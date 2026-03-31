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
    private bool isLobbyReady;
    private bool isLocalCountdownActive;
    private float localCountdownTimer;
    private bool isStartButtonReady;
    private int lastDisplayedCountdown = -1;
    private bool isStartRequestSent;

    private void Start()
    {
        if (countdownText != null) countdownText.text = "";
        if (startButtonObject != null) startButtonObject.SetActive(false);
        
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

        SubscribeToStateEvents();
        SyncInitialRoomState();
        isLobbyReady = true;
    }

    private void Update()
    {
        if (!isLobbyReady) return;

        UpdateCountdownUI();

        ConnectionMode currentMode = GameFlowManager.Instance.currentMode;
        
        if (currentMode == ConnectionMode.Offline)
        {
            ProcessOfflineModeInput();
            EvaluateOfflineState();
        }
        else if (currentMode == ConnectionMode.OnlineClient)
        {
            ProcessOnlineModeInput();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromStateEvents();
    }

    /*
     * 룸 매니저의 상태 동기화 및 서버 주도 흐름 제어 이벤트를 구독합니다.
     */
    private void SubscribeToStateEvents()
    {
        if (RoomStateManager.Instance != null)
        {
            RoomStateManager.Instance.OnCharacterSelectUpdated += HandleCharacterSelectUpdated;
            RoomStateManager.Instance.OnCountdownUpdated += HandleCountdownUpdated;
            RoomStateManager.Instance.OnStartButtonActivated += HandleStartButtonActivated;
        }
    }

    /*
     * 씬 파괴 시 이벤트 구독을 해제하여 메모리 누수를 방지합니다.
     */
    private void UnsubscribeFromStateEvents()
    {
        if (RoomStateManager.Instance != null)
        {
            RoomStateManager.Instance.OnCharacterSelectUpdated -= HandleCharacterSelectUpdated;
            RoomStateManager.Instance.OnCountdownUpdated -= HandleCountdownUpdated;
            RoomStateManager.Instance.OnStartButtonActivated -= HandleStartButtonActivated;
        }
    }

    /*
     * 씬 진입 직후 룸 매니저에 캐싱된 현재 캐릭터 선택 상태를 UI에 즉시 동기화합니다.
     */
    private void SyncInitialRoomState()
    {
        if (RoomStateManager.Instance != null && GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient)
        {
            RoomStateModel model = RoomStateManager.Instance.roomModel;
            HandleCharacterSelectUpdated(
                model.p1CharacterIndex, model.isP1CharacterLocked, model.p1PreferredSide,
                model.p2CharacterIndex, model.isP2CharacterLocked, model.p2PreferredSide
            );
        }
    }

    /*
     * 서버의 명령 또는 오프라인 자체 판정에 의해 활성화된 카운트다운 타이머의 UI를 갱신합니다.
     */
    private void UpdateCountdownUI()
    {
        if (isLocalCountdownActive)
        {
            localCountdownTimer -= Time.deltaTime;
            
            if (localCountdownTimer <= 0f)
            {
                isLocalCountdownActive = false;
                
                if (GameFlowManager.Instance.currentMode == ConnectionMode.Offline)
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

    /*
     * 오프라인 모드일 때 자체적으로 양측의 준비 상태를 확인하여 카운트다운을 시작하거나 취소합니다.
     */
    private void EvaluateOfflineState()
    {
        if (p1Context.isLocked && p2Context.isLocked)
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

    /*
     * 오프라인 모드일 때 양측 플레이어의 입력을 동시에 처리합니다.
     */
    private void ProcessOfflineModeInput()
    {
        HandleLocalInput(1, p1Context);
        HandleLocalInput(2, p2Context);
    }

    /*
     * 온라인 모드일 때 룸 매니저로부터 할당받은 권한(슬롯)에 맞는 입력만 처리합니다.
     */
    private void ProcessOnlineModeInput()
    {
        if (RoomStateManager.Instance == null) return;

        int localSlot = RoomStateManager.Instance.GetLocalPlayerSlot(); 
        
        if (localSlot == 0)
        {
            HandleLocalInput(1, p1Context);
        }
        else if (localSlot == 1)
        {
            HandleLocalInput(2, p2Context);
        }
    }

    /*
     * 로컬 키보드 입력을 처리하여 커서를 움직이거나 락인/락인해제/게임시작 상태를 전송합니다.
     */
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
            if (isSelectPressed && !isStartRequestSent)
            {
                isStartRequestSent = true;
                if (startButtonText != null) startButtonText.color = pressedStartColor;
                
                if (isOfflineMode)
                {
                    GameFlowManager.Instance.ChangeScene(GameSceneType.GamePlay);
                }
                else if (ServerNetworkManager.Instance != null)
                {
                    ServerNetworkManager.Instance.SendStartRequest();
                }
            }
            return;
        }

        if (!context.isLocked && isLockInput)
        {
            context.isLocked = true;
            UpdateLockUI(context);
            NotifyStateToServer(playerId, context);
        }
        else if (context.isLocked && isUnlockInput)
        {
            context.isLocked = false;
            UpdateLockUI(context);
            NotifyStateToServer(playerId, context);
        }

        if (context.isLocked) return;

        int move = GetMovementInput(context);
        if (move != 0)
        {
            int oldIndex = context.currentIndex;
            context.currentIndex = (context.currentIndex + move + characterRoster.Length) % characterRoster.Length;
            
            UpdateCharacterDisplay(context);
            UpdateSpecificTiles(oldIndex, context.currentIndex);
            NotifyStateToServer(playerId, context);
        }
    }

    /*
     * 변경된 캐릭터 픽 상태를 로컬 매니저에 저장하고, 온라인일 경우 서버로 전송합니다.
     */
    private void NotifyStateToServer(int playerId, PlayerSelectContext context)
    {
        if (context.isLocked)
        {
            SaveCharacterData(playerId, context.currentIndex);
        }

        if (GameFlowManager.Instance.currentMode == ConnectionMode.OnlineClient && ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendSelectUpdate(playerId, context.currentIndex, context.isLocked);
        }
    }

    /*
     * 룸 매니저로부터 양측의 캐릭터 선택 동기화 이벤트를 수신하여 적용합니다.
     */
    private void HandleCharacterSelectUpdated(int p1Idx, bool p1Lock, int p1Side, int p2Idx, bool p2Lock, int p2Side)
    {
        UpdateRemoteState(p1Context, p1Idx, p1Lock, 1);
        UpdateRemoteState(p2Context, p2Idx, p2Lock, 2);
    }

    /*
     * 원격에서 전달된 커서 위치와 락인 상태를 로컬 UI에 강제로 덮어씌웁니다.
     */
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

    /*
     * 서버(또는 로컬 판정)의 지시에 따라 카운트다운 타이머의 작동 여부를 설정합니다.
     */
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

    /*
     * 서버의 지시에 따라 게임 시작 버튼을 활성화하고 입력을 대기합니다.
     */
    private void HandleStartButtonActivated()
    {
        isLocalCountdownActive = false;
        isStartButtonReady = true;

        if (countdownText != null) countdownText.text = "";
        if (startButtonObject != null) startButtonObject.SetActive(true);
        if (startButtonText != null) startButtonText.color = normalStartColor;
    }

    /*
     * 픽이 완료된 캐릭터의 게임용 데이터를 전역 데이터베이스에 캐싱합니다.
     */
    private void SaveCharacterData(int playerId, int index)
    {
        if (playerId == 1) MatchDataManager.P1CharacterData = characterRoster[index].inGameData;
        else if (playerId == 2) MatchDataManager.P2CharacterData = characterRoster[index].inGameData;
    }

    /*
     * 확인(Select) 키 입력 여부를 반환합니다.
     */
    private bool GetSelectInput(PlayerSelectContext context)
    {
        if (context.inputBinding.selectKey == Key.None) return false;
        return Keyboard.current[context.inputBinding.selectKey].wasPressedThisFrame;
    }

    /*
     * 오프라인 픽 잠금(LP, RP) 키 입력 여부를 반환합니다.
     */
    private bool GetOfflineLockInput(PlayerSelectContext context)
    {
        bool isLpPressed = context.inputBinding.lpKey != Key.None && Keyboard.current[context.inputBinding.lpKey].wasPressedThisFrame;
        bool isRpPressed = context.inputBinding.rpKey != Key.None && Keyboard.current[context.inputBinding.rpKey].wasPressedThisFrame;
        return isLpPressed || isRpPressed;
    }

    /*
     * 오프라인 픽 잠금 해제(LK, RK) 키 입력 여부를 반환합니다.
     */
    private bool GetOfflineUnlockInput(PlayerSelectContext context)
    {
        bool isLkPressed = context.inputBinding.lkKey != Key.None && Keyboard.current[context.inputBinding.lkKey].wasPressedThisFrame;
        bool isRkPressed = context.inputBinding.rkKey != Key.None && Keyboard.current[context.inputBinding.rkKey].wasPressedThisFrame;
        return isLkPressed || isRkPressed;
    }

    /*
     * 좌우 방향키 입력에 따른 이동 값을 반환합니다.
     */
    private int GetMovementInput(PlayerSelectContext context)
    {
        if (context.inputBinding.leftKey != Key.None && Keyboard.current[context.inputBinding.leftKey].wasPressedThisFrame) return -1;
        if (context.inputBinding.rightKey != Key.None && Keyboard.current[context.inputBinding.rightKey].wasPressedThisFrame) return 1;
        return 0;
    }

    /*
     * 락인 여부에 따라 텍스트 문구와 자물쇠 아이콘을 갱신합니다.
     */
    private void UpdateLockUI(PlayerSelectContext context)
    {
        if (context.statusText != null) context.statusText.text = context.isLocked ? "Ready" : "Selecting";
        if (context.lockIconObject != null) context.lockIconObject.SetActive(context.isLocked);
    }

    /*
     * 이전 타일과 이동 후 타일의 선택 시각 효과를 갱신합니다.
     */
    private void UpdateSpecificTiles(int oldIndex, int newIndex)
    {
        UpdateTileVisual(oldIndex);
        UpdateTileVisual(newIndex);
    }

    /*
     * 특정 인덱스의 타일이 P1 혹은 P2에 의해 선택되었는지 판별하여 색을 칠합니다.
     */
    private void UpdateTileVisual(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= gridTiles.Length) return;
        bool isP1 = (targetIndex == p1Context.currentIndex);
        bool isP2 = (targetIndex == p2Context.currentIndex);
        gridTiles[targetIndex].UpdateVisuals(isP1, isP2, p1Context.cursorColor, p2Context.cursorColor);
    }

    /*
     * 변경된 캐릭터 인덱스에 맞춰 2D 일러스트, 텍스트, 3D 모델을 교체합니다.
     */
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

    /*
     * 지연 시간 후 이전 모델을 파괴하고 새 3D 모델을 생성 및 애니메이션 재생합니다.
     */
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

    /*
     * 2D 일러스트의 피벗과 크기를 원본 이미지 비율에 맞추어 조정합니다.
     */
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

    /*
     * 대상 게임 오브젝트와 하위 모든 자식들의 레이어를 재귀적으로 변경합니다.
     */
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, layer);
    }
}