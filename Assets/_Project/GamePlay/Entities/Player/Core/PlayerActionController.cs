using UnityEngine;
using System.Collections.Generic;

public struct ActionRequest
{
    public ActionDataSO actionData;
    public PlayerState_Type targetState;
    public ComboNode comboNode;
    public bool isCommandAction;
}

public class ActionResolver
{
    private PlayerController controller;
    private CommandListSO commandList;
    private ComboTreeSO comboTree;

    public void Initialize(PlayerController playerController, CommandListSO cmds, ComboTreeSO combos)
    {
        controller = playerController;
        commandList = cmds;
        comboTree = combos;
    }

    public ActionRequest? EvaluateInput(ref DeterministicInputBuffer inputBuffer, InputFlags currentInput, InputFlags newlyPressedFlags, int currentFrame, PlayerState_Type currentState, List<InputFlags> currentComboSequence)
    {
        InputStateTracker tracker = controller.GetTracker();
        CommandDefinition matchedCommand = inputBuffer.CheckCommands(commandList, currentFrame, currentState, ref tracker);
        
        bool isCommandMatched = matchedCommand != null;
        if (isCommandMatched)
        {
            inputBuffer.Clear();
            return new ActionRequest 
            { 
                actionData = matchedCommand.actionData, 
                targetState = matchedCommand.targetState,
                comboNode = null,
                isCommandAction = true 
            };
        }

        InputFlags attackMask = InputFlags.LP | InputFlags.RP | InputFlags.LK | InputFlags.RK;
        InputFlags newlyPressedAttack = newlyPressedFlags & attackMask;

        bool isAttackPressed = newlyPressedAttack != InputFlags.None;
        if (isAttackPressed)
        {
            InputFlags directionMask = InputFlags.Up | InputFlags.Down | InputFlags.Forward | InputFlags.Back;
            InputFlags combinedInput = newlyPressedAttack | (currentInput & directionMask);

            bool isAttacking = currentState == PlayerState_Type.Attacking;
            bool isComboActive = currentComboSequence != null && currentComboSequence.Count > 0;

            if (isAttacking && isComboActive)
            {
                List<InputFlags> testSequence = new List<InputFlags>(currentComboSequence);
                testSequence.Add(combinedInput);
                
                ComboNode nextCombo = comboTree != null ? comboTree.GetNodeFromSequence(testSequence) : null;
                bool isNextComboValid = nextCombo != null;
                if (isNextComboValid)
                {
                    return new ActionRequest 
                    { 
                        actionData = nextCombo.actionData, 
                        targetState = PlayerState_Type.Attacking,
                        comboNode = nextCombo,
                        isCommandAction = false 
                    };
                }
                
                return null;
            }
            
            ComboNode firstCombo = comboTree != null ? comboTree.FindBestMatchNode(comboTree.startingAttacks, combinedInput) : null;
            bool isFirstComboValid = firstCombo != null;
            if (isFirstComboValid)
            {
                return new ActionRequest 
                { 
                    actionData = firstCombo.actionData, 
                    targetState = PlayerState_Type.Attacking,
                    comboNode = firstCombo,
                    isCommandAction = false 
                };
            }
        }

        return null;
    }
}

public struct BufferedAction
{
    public ActionRequest request;
    public int enqueueFrame;
}

public class ActionBufferManager
{
    private BufferedAction? pendingAction;

    public void InitializeBuffer()
    {
        pendingAction = null;
    }

    public void BufferAction(ActionRequest request, int currentFrame)
    {
        pendingAction = new BufferedAction { request = request, enqueueFrame = currentFrame };
    }

    public void ClearExpiredActions(int currentFrame, int bufferWindowFrames)
    {
        bool hasPendingAction = pendingAction.HasValue;
        if (hasPendingAction)
        {
            bool isExpired = currentFrame - pendingAction.Value.enqueueFrame > bufferWindowFrames;
            if (isExpired)
            {
                pendingAction = null;
            }
        }
    }

    public ActionRequest? TryGetNextAction()
    {
        bool hasPendingAction = pendingAction.HasValue;
        if (hasPendingAction)
        {
            ActionRequest action = pendingAction.Value.request;
            pendingAction = null;
            return action;
        }
        return null;
    }

    public void ClearBuffer()
    {
        pendingAction = null;
    }

    public bool IsBufferEmpty() => !pendingAction.HasValue;

    public BufferedAction? GetPendingAction() => pendingAction;
    public void SetPendingAction(BufferedAction? action) => pendingAction = action;
}

public class PlayerActionController
{
    private PlayerController controller;
    private DeterministicInputBuffer inputBuffer;
    private ActionResolver actionResolver;
    private ActionBufferManager actionBuffer;
    private List<InputFlags> comboSequence;
    private int commandBufferWindow;

    public void Initialize(PlayerController playerController, CommandListSO cmdList, ComboTreeSO comboTreeData, int bufferWindowFrames)
    {
        controller = playerController;
        
        inputBuffer = new DeterministicInputBuffer();
        inputBuffer.Initialize();

        comboSequence = new List<InputFlags>();
        commandBufferWindow = bufferWindowFrames;

        actionResolver = new ActionResolver();
        actionResolver.Initialize(controller, cmdList, comboTreeData);

        actionBuffer = new ActionBufferManager();
        actionBuffer.InitializeBuffer();
    }

    public void ExportState(ref PlayerSnapshot snapshot)
    {
        snapshot.actionControllerState.deterministicInputBuffer = inputBuffer;

        bool isComboArrayMissing = snapshot.actionControllerState.comboSequence == null || snapshot.actionControllerState.comboSequence.Length != 10;
        if (isComboArrayMissing)
        {
            snapshot.actionControllerState.comboSequence = new InputFlags[10];
        }

        int currentComboCount = comboSequence.Count;
        for (int i = 0; i < currentComboCount; i++)
        {
            snapshot.actionControllerState.comboSequence[i] = comboSequence[i];
        }

        snapshot.actionControllerState.comboCount = currentComboCount;
        snapshot.actionControllerState.pendingAction = actionBuffer.GetPendingAction();
    }

    public void ImportState(PlayerSnapshot snapshot)
    {
        inputBuffer = snapshot.actionControllerState.deterministicInputBuffer;

        comboSequence.Clear();
        int savedComboCount = snapshot.actionControllerState.comboCount;
        bool hasValidComboArray = snapshot.actionControllerState.comboSequence != null;
        
        if (hasValidComboArray)
        {
            for (int i = 0; i < savedComboCount; i++)
            {
                comboSequence.Add(snapshot.actionControllerState.comboSequence[i]);
            }
        }

        actionBuffer.SetPendingAction(snapshot.actionControllerState.pendingAction);
    }

    public void ProcessInput(PlayerInput currentInput, InputFlags currentKeyDownFlags, int currentFrame, PlayerState_Type currentState)
    {
        inputBuffer.AddInput(currentInput);
        
        ActionRequest? evaluatedAction = actionResolver.EvaluateInput(ref inputBuffer, currentInput.flags, currentKeyDownFlags, currentFrame, currentState, comboSequence);

        bool isActionEvaluated = evaluatedAction.HasValue;
        if (isActionEvaluated)
        {
            actionBuffer.BufferAction(evaluatedAction.Value, currentFrame);
        }
    }

    public ActionRequest? GetExecutableAction(int currentFrame)
    {
        actionBuffer.ClearExpiredActions(currentFrame, commandBufferWindow);
        return actionBuffer.TryGetNextAction();
    }

    public void ClearAllBuffers()
    {
        inputBuffer.Clear();
        actionBuffer.ClearBuffer();
    }

    public List<InputFlags> GetComboSequence()
    {
        return comboSequence;
    }

    public void ClearComboSequence()
    {
        comboSequence.Clear();
    }

    public void AddToComboSequence(InputFlags requiredInput)
    {
        comboSequence.Add(requiredInput);
    }
}