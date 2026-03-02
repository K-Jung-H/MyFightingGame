using System.Collections.Generic;

public class InputBuffer
{
    private LinkedList<PlayerInput> buffer = new LinkedList<PlayerInput>();
    private int bufferSize;

    public InputBuffer(int size = 60)
    {
        bufferSize = size;
    }

    public void AddInput(PlayerInput input)
    {
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

        var latestInput = buffer.First.Value;
        bool isLatestMatched = CheckStepMatch(command.sequence[stepIndex], latestInput.flags);
        if (!isLatestMatched) return false;
        
        bool hasMultipleInputs = buffer.Count > 1;
        if (hasMultipleInputs)
        {
            var prevInput = buffer.First.Next.Value;
            bool isPrevMatched = CheckStepMatch(command.sequence[stepIndex], prevInput.flags);
            if (isPrevMatched) return false;
        }

        stepIndex--; 

        foreach (var buffered in buffer)
        {
            bool isTooOld = buffered.frame < frameLimit;
            if (isTooOld) break;

            bool isAllStepsMatched = stepIndex < 0;
            if (isAllStepsMatched) return true;

            CommandStep currentStep = command.sequence[stepIndex];
            bool isCurrentStepMatched = CheckStepMatch(currentStep, buffered.flags);
            
            if (isCurrentStepMatched)
            {
                stepIndex--;
            }
            else
            {
                bool hasInputFlags = buffered.flags != InputFlags.None;
                if (hasInputFlags)
                {
                    CommandStep nextStep = command.sequence[stepIndex + 1];
                    bool isNextStepMatched = CheckStepMatch(nextStep, buffered.flags);
                    if (!isNextStepMatched)
                    {
                        return false; 
                    }
                }
            }
        }

        return stepIndex < 0;
    }

    private bool CheckStepMatch(CommandStep step, InputFlags flags)
    {
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