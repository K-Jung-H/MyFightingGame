using System;

public class InputBuffer : ISnapshotSync
{
    private PlayerController controller;
    private PlayerInput[] buffer;
    private int head;
    private int count;
    private int bufferSize;

    public InputBuffer(int size = 60)
    {
        bufferSize = size;
        buffer = new PlayerInput[bufferSize];
        head = 0;
        count = 0;
    }

    public void Initialize(PlayerController playerController)
    {
        controller = playerController;
    }

    public void ExportState(ref PlayerSnapshot snapshot)
    {
        bool isArrayMissing = snapshot.inputBufferState.inputs == null || snapshot.inputBufferState.inputs.Length != bufferSize;
        if (isArrayMissing)
        {
            snapshot.inputBufferState.inputs = new PlayerInput[bufferSize];
        }

        Array.Copy(buffer, snapshot.inputBufferState.inputs, bufferSize);
        snapshot.inputBufferState.head = head;
        snapshot.inputBufferState.count = count;
    }

    public void ImportState(PlayerSnapshot snapshot)
    {
        bool isStateValid = snapshot.inputBufferState.inputs != null;
        if (isStateValid)
        {
            Array.Copy(snapshot.inputBufferState.inputs, buffer, bufferSize);
        }
        head = snapshot.inputBufferState.head;
        count = snapshot.inputBufferState.count;
    }

    public void AddInput(PlayerInput input)
    {
        bool hasPreviousInput = count > 0;
        if (hasPreviousInput)
        {
            PlayerInput lastInput = buffer[head];
            bool isSameAsPrevious = lastInput.flags == input.flags;

            if (isSameAsPrevious)
            {
                lastInput.frame = input.frame;
                buffer[head] = lastInput;
                return;
            }
        }

        head = (head - 1 + bufferSize) % bufferSize;
        buffer[head] = input;
        
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

    public CommandDefinition CheckCommands(CommandListSO commandList, int currentFrame, PlayerState_Type currentState)
    {
        bool isListInvalid = commandList == null || commandList.commands == null;
        if (isListInvalid) return null;

        foreach (var command in commandList.commands)
        {
            bool hasStateRestriction = command.validStates != 0;
            bool isStateValid = (command.validStates & currentState) != 0;

            if (hasStateRestriction && !isStateValid) continue;

            bool isSequenceMatched = CheckSequence(command, currentFrame);
            if (isSequenceMatched) return command;
        }
        return null;
    }

    private bool CheckSequence(CommandDefinition command, int currentFrame)
    {
        bool isSequenceInvalid = command.sequence == null || command.sequence.Count == 0 || count == 0;
        if (isSequenceInvalid) return false;

        int stepIndex = command.sequence.Count - 1;
        int frameLimit = currentFrame - command.timeWindowFrames;
        InputStateTracker tracker = controller.GetTracker();

        PlayerInput latestInput = buffer[head];
        bool isLatestMatched = CheckStepMatch(command.sequence[stepIndex], tracker, latestInput.flags);
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

            PlayerInput buffered = buffer[currentIndex];
            bool isTooOld = buffered.frame < frameLimit;
            if (isTooOld) break;

            bool isCurrentStepMatched = CheckStepMatch(currentStep, tracker, buffered.flags);
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

    private bool CheckStepMatch(CommandStep step, InputStateTracker tracker, InputFlags flags)
    {
        bool isHoldType = step.executeType == InputExecuteType.Hold;
        if (isHoldType) return tracker.GetHoldDuration(step.requiredFlags) >= step.requiredHoldFrames;

        if (step.isExactMatchRequired) return flags == step.requiredFlags;
        
        bool isNoneRequired = step.requiredFlags == InputFlags.None;
        if (isNoneRequired) return flags == InputFlags.None;
        
        return (flags & step.requiredFlags) == step.requiredFlags;
    }
}