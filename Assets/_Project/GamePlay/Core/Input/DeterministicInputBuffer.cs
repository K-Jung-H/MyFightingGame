using System;

public unsafe struct DeterministicInputBuffer
{
    public const int bufferSize = 60;

    public fixed int frames[bufferSize];
    public fixed int rawFlags[bufferSize];
    public fixed int holdFrames[bufferSize];

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
                holdFrames[head]++;
                return;
            }
        }

        head = (head - 1 + bufferSize) % bufferSize;
        frames[head] = input.frame;
        rawFlags[head] = (int)input.flags;
        holdFrames[head] = 1;

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

    public CommandDefinition CheckCommands(CommandListSO commandList, int currentFrame, PlayerState_Type currentState)
    {
        if (commandList == null || count == 0) return null;

        for (int i = 0; i < commandList.commands.Count; i++)
        {
            CommandDefinition command = commandList.commands[i];
            if (command == null || command.sequence == null || command.sequence.Count == 0) continue;

            bool canExecute = (command.validStates & currentState) != 0;
            if (!canExecute) continue;

            if (IsMatch(command, currentFrame))
            {
                return command;
            }
        }
        return null;
    }

    private bool IsMatch(CommandDefinition command, int currentFrame)
    {
        int stepIndex = command.sequence.Count - 1;
        int currentIndex = head;
        int checkedCount = 0;
        int frameLimit = currentFrame - command.timeWindowFrames;

        while (stepIndex >= 0 && checkedCount < count)
        {
            CommandStep currentStep = command.sequence[stepIndex];
            
            PlayerInput buffered = GetInputAt(currentIndex);
            bool isTooOld = buffered.frame < frameLimit;
            if (isTooOld) break;

            bool isCurrentStepMatched = CheckStepMatch(currentStep, buffered.flags, currentIndex);
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
            bool isHoldMet = GetHoldDurationFromBuffer(remainingHoldStep.requiredFlags, head) >= remainingHoldStep.requiredHoldFrames;

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

    private bool CheckStepMatch(CommandStep step, InputFlags flags, int bufferIndex)
    {
        bool isHoldType = step.executeType == InputExecuteType.Hold;
        if (isHoldType) return GetHoldDurationFromBuffer(step.requiredFlags, bufferIndex) >= step.requiredHoldFrames;

        if (step.isExactMatchRequired) return flags == step.requiredFlags;

        bool isNoneRequired = step.requiredFlags == InputFlags.None;
        if (isNoneRequired) return flags == InputFlags.None;

        return (flags & step.requiredFlags) == step.requiredFlags;
    }

    private int GetHoldDurationFromBuffer(InputFlags requiredFlags, int startBufferIndex)
    {
        int totalHoldFrames = 0;
        int offsetFromHead = (startBufferIndex - head + bufferSize) % bufferSize;
        int remainingElements = count - offsetFromHead;

        for (int i = 0; i < remainingElements; i++)
        {
            int historyIndex = (startBufferIndex + i) % bufferSize;
            int flagsAtFrame = rawFlags[historyIndex];

            if ((flagsAtFrame & (int)requiredFlags) == (int)requiredFlags)
            {
                totalHoldFrames += holdFrames[historyIndex];
            }
            else
            {
                break;
            }
        }
        return totalHoldFrames;
    }
}