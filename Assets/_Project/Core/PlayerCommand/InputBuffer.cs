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

    public CommandDefinition CheckCommands(CommandListSO commandList, int currentFrame, PlayerState_Type currentState)
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

    var latestInput = buffer.First.Value;
    if (!CheckStepMatch(command.sequence[stepIndex], latestInput.flags)) return false;
    
    if (buffer.Count > 1)
    {
        var prevInput = buffer.First.Next.Value;
        if (CheckStepMatch(command.sequence[stepIndex], prevInput.flags)) return false;
    }

    stepIndex--; 

    foreach (var buffered in buffer)
    {
        if (buffered.frame < frameLimit) break;
        if (stepIndex < 0) return true;

        CommandStep currentStep = command.sequence[stepIndex];
        
        if (CheckStepMatch(currentStep, buffered.flags))
        {
            stepIndex--;
        }
        else
        {
           
            if (buffered.flags != InputFlags.None)
            {
                CommandStep nextStep = command.sequence[stepIndex + 1];
                if (!CheckStepMatch(nextStep, buffered.flags))
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