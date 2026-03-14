using System;

public unsafe struct DeterministicInputBuffer
{
    public const int bufferSize = 60;

    public fixed int frames[bufferSize];
    public fixed int rawFlags[bufferSize];

    public int head;
    public int count;

    public void Initialize()
    {
        head = 0;
        count = 0;
    }

    public void AddInput(PlayerInput input)
    {
        bool hasPreviousInput = count > 0;
        if (hasPreviousInput)
        {
            int lastFlags = rawFlags[head];
            bool isSameAsPrevious = lastFlags == (int)input.flags;

            if (isSameAsPrevious)
            {
                frames[head] = input.frame;
                return;
            }
        }

        head = (head - 1 + bufferSize) % bufferSize;
        frames[head] = input.frame;
        rawFlags[head] = (int)input.flags;

        bool isBufferNotFull = count < bufferSize;
        if (isBufferNotFull)
        {
            count++;
        }
    }

    public void Clear()
    {
        head = 0;
        count = 0;
    }

    public PlayerInput GetInputAt(int index)
    {
        PlayerInput input = new PlayerInput();
        input.frame = frames[index];
        input.flags = (InputFlags)rawFlags[index];
        return input;
    }

    public CommandDefinition CheckCommands(CommandListSO commandList, int currentFrame, PlayerState_Type currentState, ref InputStateTracker tracker)
    {
        bool isListInvalid = commandList == null || commandList.commands == null;
        if (isListInvalid) return null;

        foreach (var command in commandList.commands)
        {
            bool hasStateRestriction = command.validStates != 0;
            bool isStateValid = (command.validStates & currentState) != 0;

            if (hasStateRestriction && !isStateValid) continue;

            bool isSequenceMatched = CheckSequence(command, currentFrame, ref tracker);
            if (isSequenceMatched) return command;
        }
        return null;
    }

    private bool CheckSequence(CommandDefinition command, int currentFrame, ref InputStateTracker tracker)
    {
        bool isSequenceInvalid = command.sequence == null || command.sequence.Count == 0 || count == 0;
        if (isSequenceInvalid) return false;

        int stepIndex = command.sequence.Count - 1;
        int frameLimit = currentFrame - command.timeWindowFrames;

        PlayerInput latestInput = GetInputAt(head);
        bool isLatestMatched = CheckStepMatch(command.sequence[stepIndex], ref tracker, latestInput.flags);
        if (!isLatestMatched) return false;

        stepIndex--;

        int currentIndex = (head + 1) % bufferSize;
        int checkedCount = 1;

        while (checkedCount < count)
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

            PlayerInput buffered = GetInputAt(currentIndex);
            bool isTooOld = buffered.frame < frameLimit;
            if (isTooOld) break;

            bool isCurrentStepMatched = CheckStepMatch(currentStep, ref tracker, buffered.flags);
            if (isCurrentStepMatched)
            {
                stepIndex--;
            }

            currentIndex = (currentIndex + 1) % bufferSize;
            checkedCount++;
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

    private bool CheckStepMatch(CommandStep step, ref InputStateTracker tracker, InputFlags flags)
    {
        bool isHoldType = step.executeType == InputExecuteType.Hold;
        if (isHoldType) return tracker.GetHoldDuration(step.requiredFlags) >= step.requiredHoldFrames;

        if (step.isExactMatchRequired) return flags == step.requiredFlags;

        bool isNoneRequired = step.requiredFlags == InputFlags.None;
        if (isNoneRequired) return flags == InputFlags.None;

        return (flags & step.requiredFlags) == step.requiredFlags;
    }
}