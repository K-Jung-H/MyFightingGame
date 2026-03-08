using UnityEngine;

public class PlayerController : ITargetable
{
    private PlayerConfigSO config;
    private PlayerActionController actionController;
    private PlayerPhysics physics;
    private PlayerCombat combat;
    private PlayerStateMachine stateMachine;
    private ITargetable targetEntity;

    private InputStateTracker inputTracker;

    public PlayerInput currentInput { get; private set; }
    public InputFlags currentKeyDownFlags { get; private set; }
    private int currentFrame;

    private InputFlags previousRawFlags = InputFlags.None;

    public void Initialize(Vector3 startPosition, PlayerConfigSO playerConfig, CommandListSO cmdList, ComboTreeSO comboTreeData)
    {
        config = playerConfig;
        currentFrame = 0;

        inputTracker = new InputStateTracker();
        inputTracker.Initialize();

        physics = new PlayerPhysics();
        physics.Initialize(startPosition, config);

        combat = new PlayerCombat();

        actionController = new PlayerActionController();
        actionController.Initialize(this, cmdList, comboTreeData, config.commandBufferWindow);

        stateMachine = new PlayerStateMachine();
        stateMachine.Initialize(this, config);
    }

    public void UpdateTick(PlayerInput input)
    {
        bool isHitstopActive = combat.ProcessHitstopTick();
        if (isHitstopActive)
        {
            return;
        }

        PlayerInput sanitizedInput = ApplyReleaseDebounce(input);

        inputTracker.UpdateTick(sanitizedInput.flags);
        
        currentInput = sanitizedInput;
        currentKeyDownFlags = inputTracker.currentFlags & ~inputTracker.previousFlags;
        currentFrame++;

        physics.UpdateLookDirection(targetEntity, stateMachine.GetCurrentState());

        actionController.ProcessInput(sanitizedInput, currentKeyDownFlags, currentFrame, stateMachine.GetCurrentState());
        ProcessActionBuffer();

        physics.ResetRootMotionFlag();
        stateMachine.UpdateTick(sanitizedInput);
        physics.ProcessPhysicsTick();
    }

    private PlayerInput ApplyReleaseDebounce(PlayerInput rawInput)
    {
        InputFlags currentRawFlags = rawInput.flags;
        InputFlags justReleasedFlags = previousRawFlags & ~currentRawFlags;

        PlayerInput sanitizedInput = rawInput;
        sanitizedInput.flags = currentRawFlags | justReleasedFlags;

        previousRawFlags = currentRawFlags;

        return sanitizedInput;
    }

    private void ProcessActionBuffer()
    {
        bool isTransitionable = stateMachine.CanTransitionToAttack();
        if (isTransitionable)
        {
            ActionRequest? nextAction = actionController.GetExecutableAction(currentFrame);
            bool hasNextAction = nextAction.HasValue;
            if (hasNextAction)
            {
                stateMachine.ExecuteActionRequest(nextAction.Value, actionController);
            }
        }
    }

    public Vector3 GetPosition() => physics.GetPosition();
    public void SetTarget(ITargetable target) => targetEntity = target;
    public PlayerPhysics GetPhysics() => physics;
    public PlayerCombat GetCombat() => combat;
    public PlayerStateMachine GetStateMachine() => stateMachine;
    public PlayerActionController GetActionController() => actionController;
    public PlayerConfigSO GetConfig() => config;
    public int GetCurrentFrame() => currentFrame;
    public InputStateTracker GetTracker() => inputTracker;
}