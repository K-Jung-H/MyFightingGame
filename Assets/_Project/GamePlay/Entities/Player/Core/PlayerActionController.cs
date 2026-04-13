using UnityEngine;
using System.Collections.Generic;

public struct ActionRequest
{
    public ActionDataSO actionData;
    public PlayerState_Type targetState;
    public ComboNode comboNode;
    public bool isCommandAction;
}

public struct BufferedAction
{
    public ActionRequest request;
    public int enqueueFrame;
}

public class ActionBuffer
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
        
        bool hasAttackInput = newlyPressedAttack != InputFlags.None;
        if (!hasAttackInput) return null;

        ComboNode matchedCombo = EvaluateCombo(currentInput, currentComboSequence);
        
        bool isComboMatched = matchedCombo != null;
        if (isComboMatched)
        {
            inputBuffer.Clear();
            return new ActionRequest 
            { 
                actionData = matchedCombo.actionData, 
                targetState = PlayerState_Type.Attacking,
                comboNode = matchedCombo,
                isCommandAction = false 
            };
        }

        return null;
    }

    private ComboNode EvaluateCombo(InputFlags currentInput, List<InputFlags> currentComboSequence)
    {
        bool isTreeInvalid = comboTree == null;
        if (isTreeInvalid) return null;

        List<ComboNode> currentNodes = comboTree.startingAttacks;

        foreach (var pastInput in currentComboSequence)
        {
            ComboNode match = comboTree.FindBestMatchNode(currentNodes, pastInput);
            bool isMatchValid = match != null;
            
            if (isMatchValid)
            {
                currentNodes = match.nextAttacks;
            }
            else
            {
                return null;
            }
        }

        return comboTree.FindBestMatchNode(currentNodes, currentInput);
    }
}

public class PlayerActionController : ISnapshotSync
{
    private PlayerController controller;
    private DeterministicInputBuffer inputBuffer;
    private ActionResolver actionResolver;
    private ActionBuffer actionBuffer;
    private List<InputFlags> comboSequence;
    private int commandBufferWindow;

    public void Initialize(PlayerController playerController, CommandListSO commandList, ComboTreeSO comboTree, int bufferWindow)
    {
        controller = playerController;
        commandBufferWindow = bufferWindow;
        
        inputBuffer = new DeterministicInputBuffer();
        inputBuffer.Initialize();

        actionResolver = new ActionResolver();
        actionResolver.Initialize(controller, commandList, comboTree);

        actionBuffer = new ActionBuffer();
        actionBuffer.InitializeBuffer();
        
        comboSequence = new List<InputFlags>();
    }

    public unsafe void ExportState(ref PlayerSnapshot snapshot)
    {
        snapshot.actionControllerState.deterministicInputBuffer = this.inputBuffer;

        int count = Mathf.Min(comboSequence.Count, 10);
        snapshot.actionControllerState.comboCount = count;
        
        for (int i = 0; i < count; i++)
        {
            snapshot.actionControllerState.comboSequence[i] = (int)comboSequence[i];
        }

        snapshot.actionControllerState.pendingAction = actionBuffer.GetPendingAction();
    }

    public unsafe void ImportState(PlayerSnapshot snapshot)
    {
        this.inputBuffer = snapshot.actionControllerState.deterministicInputBuffer;

        comboSequence.Clear();
        int count = Mathf.Min(snapshot.actionControllerState.comboCount, 10);
        
        for (int i = 0; i < count; i++)
        {
            comboSequence.Add((InputFlags)snapshot.actionControllerState.comboSequence[i]);
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

    public void AddToComboSequence(InputFlags input)
    {
        comboSequence.Add(input);
    }
}