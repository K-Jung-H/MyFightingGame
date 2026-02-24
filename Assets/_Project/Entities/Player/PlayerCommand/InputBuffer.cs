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
        if (buffer.Count > bufferSize)
        {
            buffer.RemoveLast();
        }
    }

    public void Clear()
    {
        buffer.Clear();
    }

    public CommandDefinition CheckCommands(CommandListSO commandList, int currentFrame, PlayerState currentState)
    {
        if (commandList == null || commandList.commands == null) return null;

        foreach (var command in commandList.commands)
        {
            if (command.validStates != null && command.validStates.Count > 0 && !command.validStates.Contains(currentState))
            {
                continue;
            }

            if (CheckSequence(command, currentFrame))
            {
                return command;
            }
        }
        return null;
    }

    private bool CheckSequence(CommandDefinition command, int currentFrame)
    {
        if (command.sequence == null || command.sequence.Count == 0) return false;

        int stepIndex = command.sequence.Count - 1;
        int frameLimit = currentFrame - command.timeWindowFrames;

        foreach (var buffered in buffer)
        {
            if (buffered.frame < frameLimit) return false;

            CommandStep currentStep = command.sequence[stepIndex];
            bool isMatch = CheckStepMatch(currentStep, buffered.flags);

            if (isMatch)
            {
                stepIndex--;
                if (stepIndex < 0) return true;
            }
            else
            {
                if (stepIndex < command.sequence.Count - 1)
                {

                    CommandStep previousMatchedStep = command.sequence[stepIndex + 1];
                    bool isHoldingPrevious = CheckStepMatch(previousMatchedStep, buffered.flags);
                    

                    if (!isHoldingPrevious)
                    {
                        return false;
                    }
                }

            }
        }

        return false;
    }


    private bool CheckStepMatch(CommandStep step, InputFlags flags)
    {
        if (step.requireExactMatch)
        {
            return flags == step.requiredFlags;
        }
        else
        {
            if (step.requiredFlags == InputFlags.None)
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