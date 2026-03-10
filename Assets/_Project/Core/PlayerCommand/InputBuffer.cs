using System.Collections.Generic;

public class InputBuffer
{
    private PlayerController controller;
    private LinkedList<PlayerInput> buffer = new LinkedList<PlayerInput>();
    private int bufferSize;

    public InputBuffer(int size = 60)
    {
        bufferSize = size;
    }

    public void Initialize(PlayerController playerController)
    {
        controller = playerController;
    }

    public void AddInput(PlayerInput input)
    {
        bool hasPreviousInput = buffer.Count > 0;
        if (hasPreviousInput)
        {
            PlayerInput lastInput = buffer.First.Value;
            bool isSameAsPrevious = lastInput.flags == input.flags;

            if (isSameAsPrevious)
            {
                lastInput.frame = input.frame;
                buffer.First.Value = lastInput;
                return;
            }
        }

        buffer.AddFirst(input);
        
        bool isBufferOverflow = buffer.Count > bufferSize;
        if (isBufferOverflow)
        {
            buffer.RemoveLast();
        }
    }

    public void Clear()
    {
        buffer.Clear();
    }

    public CommandDefinition CheckCommands(CommandListSO commandList, int currentFrame, PlayerState_Type currentState)
    {
        bool isListInvalid = commandList == null || commandList.commands == null;
        if (isListInvalid) return null;

        foreach (var command in commandList.commands)
        {
            bool hasStateRestriction = command.validStates != 0;
            bool isStateValid = (command.validStates & currentState) != 0;

            if (hasStateRestriction && !isStateValid)
            {
                continue;
            }

            bool isSequenceMatched = CheckSequence(command, currentFrame);
            if (isSequenceMatched)
            {
                return command;
            }
        }
        return null;
    }

    private bool CheckSequence(CommandDefinition command, int currentFrame)
    {
        bool isSequenceInvalid = command.sequence == null || command.sequence.Count == 0;
        if (isSequenceInvalid) return false;

        int stepIndex = command.sequence.Count - 1;
        int frameLimit = currentFrame - command.timeWindowFrames;

        InputStateTracker tracker = controller.GetTracker();

        var latestInput = buffer.First.Value;
        bool isLatestMatched = CheckStepMatch(command.sequence[stepIndex], tracker, latestInput.flags);
        if (!isLatestMatched) return false;

        stepIndex--; 

        var currentNode = buffer.First.Next;
        
        while (currentNode != null)
        {
            if (stepIndex < 0) break;

            CommandStep currentStep = command.sequence[stepIndex];
            
            bool isHoldStep = currentStep.executeType == InputExecuteType.Hold;
            if (isHoldStep)
            {
                bool isHoldConditionMet = tracker.GetHoldDuration(currentStep.requiredFlags) >= currentStep.requiredHoldFrames;
                if (isHoldConditionMet)
                {
                    stepIndex--;
                }
                else
                {
                    return false;
                }
                continue; 
            }

            var buffered = currentNode.Value;
            bool isTooOld = buffered.frame < frameLimit;
            if (isTooOld) break;

            bool isCurrentStepMatched = CheckStepMatch(currentStep, tracker, buffered.flags);
            if (isCurrentStepMatched)
            {
                stepIndex--;
            }

            currentNode = currentNode.Next;
        }

        while (stepIndex >= 0 && command.sequence[stepIndex].executeType == InputExecuteType.Hold)
        {
            CommandStep remainingHoldStep = command.sequence[stepIndex];
            bool isHoldMet = tracker.GetHoldDuration(remainingHoldStep.requiredFlags) >= remainingHoldStep.requiredHoldFrames;
            
            if (isHoldMet)
            {
                stepIndex--;
            }
            else
            {
                break;
            }
        }

        return stepIndex < 0;
    }

    private bool CheckStepMatch(CommandStep step, InputStateTracker tracker, InputFlags flags)
    {
        bool isHoldType = step.executeType == InputExecuteType.Hold;
        if (isHoldType)
        {
            return tracker.GetHoldDuration(step.requiredFlags) >= step.requiredHoldFrames;
        }

        if (step.isExactMatchRequired)
        {
            return flags == step.requiredFlags;
        }
        else
        {
            bool isNoneRequired = step.requiredFlags == InputFlags.None;
            if (isNoneRequired)
            {
                return flags == InputFlags.None;
            }
            else
            {
                return (flags & step.requiredFlags) == step.requiredFlags;
            }
        }
    }
}