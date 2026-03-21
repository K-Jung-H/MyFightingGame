using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;

public static class MatchDataManager
{
    public static CharacterDataSO P1CharacterData { get; set; }
    public static CharacterDataSO P2CharacterData { get; set; }
}

public enum ConnectionMode
{
    Offline,
    Online
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
    public ConnectionMode currentConnectionMode = ConnectionMode.Offline;
    public int localPlayerId = 1;

    public Transform characterGridPanel;
    public CharacterSelectDataSO[] characterRoster;

    public RuntimeAnimatorController sharedSelectAnimator;
    public int maxRandomIdles = 3;
    public float modelLoadDelay = 0.2f;

    public string nextSceneName = "MainScene";
    public float matchStartDelay = 1.0f;

    private bool isMatchStarting;
    private CharacterSelectTile[] gridTiles;
    private int gridColumns;
    private int character3DLayer;

    public PlayerSelectContext p1Context;
    public PlayerSelectContext p2Context;

    private void Start()
    {
        character3DLayer = LayerMask.NameToLayer("Character3D");
        gridTiles = characterGridPanel.GetComponentsInChildren<CharacterSelectTile>();

        GridLayoutGroup gridLayout = characterGridPanel.GetComponent<GridLayoutGroup>();
        bool isGridLayoutValid = gridLayout != null && gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount;

        if (isGridLayoutValid)
        {
            gridColumns = gridLayout.constraintCount;
        }
        else
        {
            gridColumns = 7;
        }

        for (int i = 0; i < gridTiles.Length; i++)
        {
            bool isIndexValid = i < characterRoster.Length;
            if (isIndexValid)
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

    private void Update()
    {
        if (isMatchStarting) return;

        bool isOnlineMode = currentConnectionMode == ConnectionMode.Online;

        if (isOnlineMode)
        {
            PlayerSelectContext localContext = localPlayerId == 1 ? p1Context : p2Context;
            HandleLocalInput(localContext, true);
        }
        else
        {
            HandleLocalInput(p1Context, false);
            HandleLocalInput(p2Context, false);
        }
    }

    public void OnReceiveRemotePlayerState(int remoteIndex, bool isRemoteLocked)
    {
        PlayerSelectContext remoteContext = localPlayerId == 1 ? p2Context : p1Context;

        bool isIndexChanged = remoteContext.currentIndex != remoteIndex;
        if (isIndexChanged)
        {
            int oldIndex = remoteContext.currentIndex;
            remoteContext.currentIndex = remoteIndex;

            UpdateCharacterDisplay(remoteContext);
            UpdateSpecificTiles(oldIndex, remoteIndex);
        }

        bool isLockChanged = remoteContext.isLocked != isRemoteLocked;
        if (isLockChanged)
        {
            remoteContext.isLocked = isRemoteLocked;
            UpdateLockUI(remoteContext);
            CheckMatchReady();
        }
    }

    private void HandleLocalInput(PlayerSelectContext context, bool isOnlineMode)
    {
        bool isKeyboardNull = Keyboard.current == null;
        if (isKeyboardNull) return;

        ProcessLockState(context, isOnlineMode);

        if (context.isLocked) return;

        ProcessGridMovement(context);
    }

    private void ProcessLockState(PlayerSelectContext context, bool isOnlineMode)
    {
        if (isOnlineMode)
        {
            bool isSelectPressed = Keyboard.current[context.inputBinding.selectKey].wasPressedThisFrame;
            bool isPausePressed = Keyboard.current[context.inputBinding.pauseKey].wasPressedThisFrame;

            if (isSelectPressed)
            {
                context.isLocked = !context.isLocked;
                UpdateLockUI(context);
                SendLocalStateToServer();
                CheckMatchReady();
            }
            else if (context.isLocked && isPausePressed)
            {
                context.isLocked = false;
                UpdateLockUI(context);
                SendLocalStateToServer();
            }
        }
        else
        {
            bool isLpPressed = Keyboard.current[context.inputBinding.lpKey].wasPressedThisFrame;
            bool isRpPressed = Keyboard.current[context.inputBinding.rpKey].wasPressedThisFrame;
            bool isLkPressed = Keyboard.current[context.inputBinding.lkKey].wasPressedThisFrame;
            bool isRkPressed = Keyboard.current[context.inputBinding.rkKey].wasPressedThisFrame;

            bool isLockAttempted = isLpPressed || isRpPressed;
            bool isUnlockAttempted = isLkPressed || isRkPressed;

            if (!context.isLocked && isLockAttempted)
            {
                context.isLocked = true;
                UpdateLockUI(context);
                CheckMatchReady();
            }
            else if (context.isLocked && isUnlockAttempted)
            {
                context.isLocked = false;
                UpdateLockUI(context);
            }
        }
    }

    private void ProcessGridMovement(PlayerSelectContext context)
    {
        bool isLeftPressed = Keyboard.current[context.inputBinding.leftKey].wasPressedThisFrame;
        bool isRightPressed = Keyboard.current[context.inputBinding.rightKey].wasPressedThisFrame;
        bool isUpPressed = Keyboard.current[context.inputBinding.upKey].wasPressedThisFrame;
        bool isDownPressed = Keyboard.current[context.inputBinding.downKey].wasPressedThisFrame;

        int newIndex = context.currentIndex;
        int totalCount = characterRoster.Length;

        if (isLeftPressed)
        {
            newIndex--;
            if (newIndex < 0) newIndex = totalCount - 1;
        }
        else if (isRightPressed)
        {
            newIndex++;
            if (newIndex >= totalCount) newIndex = 0;
        }
        else if (isUpPressed)
        {
            newIndex -= gridColumns;
            if (newIndex < 0)
            {
                int currentColumn = context.currentIndex % gridColumns;
                int maxRow = (totalCount - 1) / gridColumns;
                int bottomIndex = currentColumn + (maxRow * gridColumns);

                newIndex = bottomIndex < totalCount ? bottomIndex : bottomIndex - gridColumns;
            }
        }
        else if (isDownPressed)
        {
            newIndex += gridColumns;
            if (newIndex >= totalCount)
            {
                newIndex = context.currentIndex % gridColumns;
            }
        }

        bool isIndexChanged = newIndex != context.currentIndex;

        if (isIndexChanged)
        {
            int oldIndex = context.currentIndex;
            context.currentIndex = newIndex;

            UpdateCharacterDisplay(context);
            UpdateSpecificTiles(oldIndex, newIndex);

            bool isOnlineMode = currentConnectionMode == ConnectionMode.Online;
            if (isOnlineMode)
            {
                SendLocalStateToServer();
            }
        }
    }

    private void CheckMatchReady()
    {
        bool areBothLocked = p1Context.isLocked && p2Context.isLocked;
        if (areBothLocked && !isMatchStarting)
        {
            StartCoroutine(StartMatchRoutine());
        }
    }

    private IEnumerator StartMatchRoutine()
    {
        isMatchStarting = true;

        MatchDataManager.P1CharacterData = characterRoster[p1Context.currentIndex].inGameData;
        MatchDataManager.P2CharacterData = characterRoster[p2Context.currentIndex].inGameData;

        yield return new WaitForSeconds(matchStartDelay);

        SceneManager.LoadScene(nextSceneName);
    }

    private void SendLocalStateToServer()
    {
        PlayerSelectContext localContext = localPlayerId == 1 ? p1Context : p2Context;
    }

    private void UpdateLockUI(PlayerSelectContext context)
    {
        bool isStatusTextValid = context.statusText != null;
        if (isStatusTextValid)
        {
            context.statusText.text = context.isLocked ? "Ready" : "Selecting";
        }

        bool isLockIconObjectValid = context.lockIconObject != null;
        if (isLockIconObjectValid)
        {
            context.lockIconObject.SetActive(context.isLocked);
        }
    }

    private void UpdateSpecificTiles(int oldIndex, int newIndex)
    {
        bool isTilesNull = gridTiles == null;
        if (isTilesNull) return;

        UpdateTileVisual(oldIndex);
        UpdateTileVisual(newIndex);
    }

    private void UpdateTileVisual(int targetIndex)
    {
        bool isIndexValid = targetIndex >= 0 && targetIndex < gridTiles.Length;
        if (!isIndexValid) return;

        bool isP1 = (targetIndex == p1Context.currentIndex);
        bool isP2 = (targetIndex == p2Context.currentIndex);

        gridTiles[targetIndex].UpdateVisuals(isP1, isP2, p1Context.cursorColor, p2Context.cursorColor);
    }

    private void UpdateCharacterDisplay(PlayerSelectContext context)
    {
        CharacterSelectDataSO selectedData = characterRoster[context.currentIndex];

        bool isIllustrationImageValid = context.illustrationImage != null;
        if (isIllustrationImageValid)
        {
            context.illustrationImage.sprite = selectedData.fullBodySprite;
            context.illustrationImage.preserveAspect = true;

            Vector3 imageScale = context.illustrationImage.rectTransform.localScale;
            imageScale.x = context.isMirrored ? -Mathf.Abs(imageScale.x) : Mathf.Abs(imageScale.x);
            context.illustrationImage.rectTransform.localScale = imageScale;
        }

        bool isNameTextValid = context.nameText != null;
        if (isNameTextValid)
        {
            context.nameText.text = selectedData.characterName;
        }

        bool isCoroutineActive = context.loadCoroutine != null;
        if (isCoroutineActive)
        {
            StopCoroutine(context.loadCoroutine);
        }

        context.loadCoroutine = StartCoroutine(SpawnModelRoutine(context, context.currentIndex));
    }

    private IEnumerator SpawnModelRoutine(PlayerSelectContext context, int targetIndex)
    {
        yield return new WaitForSeconds(modelLoadDelay);

        bool isIndexMatched = context.currentIndex == targetIndex;
        if (!isIndexMatched) yield break;

        bool isCurrentModelValid = context.currentModel != null;
        if (isCurrentModelValid)
        {
            Destroy(context.currentModel);
        }

        CharacterSelectDataSO selectedData = characterRoster[targetIndex];
        bool isPrefabValid = selectedData.modelPrefab != null;

        if (isPrefabValid)
        {
            context.currentModel = Instantiate(
                selectedData.modelPrefab,
                context.displayTransform.position,
                context.displayTransform.rotation,
                context.displayTransform
            );

            SetLayerRecursively(context.currentModel, character3DLayer);

            Animator modelAnimator = context.currentModel.GetComponentInChildren<Animator>();
            bool isAnimatorAndSharedControllerValid = modelAnimator != null && sharedSelectAnimator != null;

            if (isAnimatorAndSharedControllerValid)
            {
                modelAnimator.runtimeAnimatorController = sharedSelectAnimator;
                modelAnimator.applyRootMotion = false;

                modelAnimator.Rebind();
                modelAnimator.SetBool("IsMirrored", context.isMirrored);

                int randomIdleIndex;
                if (maxRandomIdles > 1)
                {
                    do
                    {
                        randomIdleIndex = Random.Range(0, maxRandomIdles);
                    } while (randomIdleIndex == context.lastIdleIndex);
                }
                else
                {
                    randomIdleIndex = 0;
                }

                context.lastIdleIndex = randomIdleIndex;
                string targetStateName = "Selecting_Idle_" + randomIdleIndex;

                modelAnimator.Play(targetStateName, 0, 0f);
                modelAnimator.Update(0f);
            }
        }
    }

    private void SetLayerRecursively(GameObject targetObject, int targetLayer)
    {
        bool isTargetNull = targetObject == null;
        if (isTargetNull) return;

        targetObject.layer = targetLayer;

        foreach (Transform childTransform in targetObject.transform)
        {
            SetLayerRecursively(childTransform.gameObject, targetLayer);
        }
    }
}