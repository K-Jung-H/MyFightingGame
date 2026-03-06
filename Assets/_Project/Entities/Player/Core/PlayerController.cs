using UnityEngine;

public class PlayerController : ITargetable
{
    private PlayerConfigSO config;
    private PlayerActionController actionController;
    private PlayerPhysics physics;
    private PlayerCombat combat;
    private PlayerStateMachine stateMachine;
    private ITargetable targetEntity;

    public PlayerInput currentInput { get; private set; }
    public InputFlags currentKeyDownFlags { get; private set; }
    private PlayerInput previousInput;
    private int currentFrame;

    public void Initialize(Vector3 startPosition, PlayerConfigSO playerConfig, CommandListSO cmdList, ComboTreeSO comboTreeData)
    {
        config = playerConfig;
        currentFrame = 0;

        physics = new PlayerPhysics();
        physics.Initialize(startPosition, config);

        combat = new PlayerCombat();

        actionController = new PlayerActionController();
        actionController.Initialize(cmdList, comboTreeData, config.commandBufferWindow);

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

        currentInput = input;
        currentKeyDownFlags = input.flags & ~previousInput.flags;
        currentFrame++;

        physics.UpdateLookDirection(targetEntity, stateMachine.GetCurrentState());

        actionController.ProcessInput(input, currentKeyDownFlags, currentFrame, stateMachine.GetCurrentState());
        ProcessActionBuffer();

        physics.ResetRootMotionFlag();
        stateMachine.UpdateTick(input);
        physics.ProcessPhysicsTick();

        previousInput = input;
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

    public Vector3 GetPosition()
    {
        return physics.GetPosition();
    }

    public void SetTarget(ITargetable target)
    {
        targetEntity = target;
    }

    public PlayerPhysics GetPhysics() => physics;
    public PlayerCombat GetCombat() => combat;
    public PlayerStateMachine GetStateMachine() => stateMachine;
    public PlayerActionController GetActionController() => actionController;
    public PlayerConfigSO GetConfig() => config;
    public int GetCurrentFrame() => currentFrame;
}