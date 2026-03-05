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
    private CommandListSO commandList;
    private ComboTreeSO comboTree;

    public void Initialize(CommandListSO cmds, ComboTreeSO combos)
    {
        commandList = cmds;
        comboTree = combos;
    }

    public ActionRequest? EvaluateInput(InputBuffer inputBuffer, InputFlags currentInput, InputFlags newlyPressedFlags, int currentFrame, PlayerState_Type currentState, List<InputFlags> currentComboSequence)
    {
        CommandDefinition matchedCommand = inputBuffer.CheckCommands(commandList, currentFrame, currentState);
        
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