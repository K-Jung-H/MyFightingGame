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

    private CharacterSelectTile[] gridTiles;
    private int gridColumns;
    private int character3DLayer;
    private bool isMatchStarted;
    private bool isLobbyReady = false;
    private bool isEventsSubscribed = false;

    private void Start()
    {
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

    private void OnDestroy()
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnSelectBroadcastReceived -= HandleNetworkSelectBroadcast;
            NetworkSessionManager.Instance.OnSceneChangeReceived -= HandleSceneChangeCommand;
        }
    }

    private void Update()
    {
        ConnectionMode currentMode = GameFlowManager.Instance.currentMode;
        
        if (currentMode == ConnectionMode.OnlineHost || currentMode == ConnectionMode.OnlineClient)
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.UpdateNetwork();
            }
        }

        if (!isEventsSubscribed)
        {
            if (currentMode == ConnectionMode.None) return;

            SubscribeToFlowEvents(currentMode);
            isEventsSubscribed = true;
        }

        if (isMatchStarted || !isLobbyReady) return;

        if (currentMode == ConnectionMode.Offline)
        {
            HandleLocalInput(1, p1Context);
            HandleLocalInput(2, p2Context);
        }
        else
        {
            int localId = (currentMode == ConnectionMode.OnlineHost) ? 1 : 2;
            PlayerSelectContext localCtx = (localId == 1) ? p1Context : p2Context;
            HandleLocalInput(localId, localCtx);
        }
    }

    private void SubscribeToFlowEvents(ConnectionMode mode)
    {
        if (mode == ConnectionMode.Offline)
        {
            isLobbyReady = true;
        }
        else if (mode == ConnectionMode.OnlineHost || mode == ConnectionMode.OnlineClient)
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.OnConnectionEstablished += () => isLobbyReady = true;
                NetworkSessionManager.Instance.OnSelectBroadcastReceived += HandleNetworkSelectBroadcast;
                NetworkSessionManager.Instance.OnSceneChangeReceived += HandleSceneChangeCommand;
            }
        }
    }

    private void HandleLocalInput(int playerId, PlayerSelectContext context)
    {
        if (Keyboard.current == null) return;

        if (GetLockInput(context))
        {
            context.isLocked = !context.isLocked;
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

    private void HandleNetworkSelectBroadcast(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock)
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

    private void HandleSceneChangeCommand()
    {
        if (isMatchStarted) return;
        isMatchStarted = true;
        GameFlowManager.Instance.OnReceiveSceneChangeCommand("MainScene");
    }

    private bool GetLockInput(PlayerSelectContext context)
    {
        return Keyboard.current[context.inputBinding.lpKey].wasPressedThisFrame;
    }

    private int GetMovementInput(PlayerSelectContext context)
    {
        if (Keyboard.current[context.inputBinding.leftKey].wasPressedThisFrame) return -1;
        if (Keyboard.current[context.inputBinding.rightKey].wasPressedThisFrame) return 1;
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
            Vector3 scale = context.illustrationImage.rectTransform.localScale;
            scale.x = context.isMirrored ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            context.illustrationImage.rectTransform.localScale = scale;
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
                context.lastIdleIndex = (maxRandomIdles > 1) ? Random.Range(0, maxRandomIdles) : 0;
                anim.Play("Selecting_Idle_" + context.lastIdleIndex, 0, 0f);
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, layer);
    }
}