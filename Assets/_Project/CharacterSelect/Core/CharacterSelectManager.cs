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
    public bool isMirrored;
    
    [HideInInspector] 
    public int currentIndex;
    [HideInInspector] 
    public bool isLocked;
    [HideInInspector] 
    public InputBinding inputBinding;
    [HideInInspector] 
    public GameObject currentModel;
    [HideInInspector]
    public int lastIdleIndex = -1;
}

public class CharacterSelectManager : MonoBehaviour
{
    public Transform characterGridPanel;
    public CharacterSelectDataSO[] characterRoster;
    
    public RuntimeAnimatorController sharedSelectAnimator;
    public int maxRandomIdles = 3;

    public PlayerSelectContext p1Context;
    public PlayerSelectContext p2Context;

    private CharacterSelectTile[] gridTiles;
    private int gridColumns;
    private int character3DLayer;

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

        RefreshAllTiles();
        UpdateCharacterDisplay(p1Context);
        UpdateCharacterDisplay(p2Context);
    }

    private void Update()
    {
        HandleInput(p1Context);
        HandleInput(p2Context);
    }

    private void HandleInput(PlayerSelectContext context)
    {
        bool isKeyboardNull = Keyboard.current == null;
        if (isKeyboardNull) return;

        bool isConfirmPressed = Keyboard.current[context.inputBinding.lpKey].wasPressedThisFrame;
        
        if (isConfirmPressed)
        {
            context.isLocked = !context.isLocked;
            return;
        }

        if (context.isLocked) return;

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
            context.currentIndex = newIndex;
            UpdateCharacterDisplay(context);
            RefreshAllTiles();
        }
    }

    private void RefreshAllTiles()
    {
        bool isTilesNull = gridTiles == null;
        if (isTilesNull) return;

        for (int i = 0; i < gridTiles.Length; i++)
        {
            bool isP1 = (i == p1Context.currentIndex);
            bool isP2 = (i == p2Context.currentIndex);
            
            gridTiles[i].UpdateVisuals(isP1, isP2, p1Context.cursorColor, p2Context.cursorColor);
        }
    }

    private void UpdateCharacterDisplay(PlayerSelectContext context)
    {
        bool hasCurrentModel = context.currentModel != null;
        if (hasCurrentModel)
        {
            Destroy(context.currentModel);
        }

        CharacterSelectDataSO selectedData = characterRoster[context.currentIndex];

        bool hasIllustrationImage = context.illustrationImage != null;
        if (hasIllustrationImage)
        {
            context.illustrationImage.sprite = selectedData.fullBodySprite;

            Vector3 imageScale = context.illustrationImage.rectTransform.localScale;
            imageScale.x = context.isMirrored ? -Mathf.Abs(imageScale.x) : Mathf.Abs(imageScale.x);
            context.illustrationImage.rectTransform.localScale = imageScale;
        }

        bool hasNameText = context.nameText != null;
        if (hasNameText)
        {
            context.nameText.text = selectedData.characterName;
        }

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
            bool hasAnimatorAndSharedController = modelAnimator != null && sharedSelectAnimator != null;
            
            if (hasAnimatorAndSharedController)
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