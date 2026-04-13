using UnityEngine;

public class PlayerController : ITargetable, ISnapshotSync
{
    private PlayerConfigSO config;
    private PlayerActionController actionController;
    private PlayerPhysics physics;
    private PlayerCombat combat;
    private PlayerStateMachine stateMachine;
    private ITargetable targetEntity;
    private ActionRegistry actionRegistry;

    private InputStateTracker inputTracker;

    public PlayerInput currentInput { get; private set; }
    public InputFlags currentKeyDownFlags { get; private set; }
    private int currentFrame;

    private InputFlags previousRawFlags = InputFlags.None;
    private InputFlags accumulatedHitstopFlags = InputFlags.None;
    private InputFlags accumulatedLogicFlags = InputFlags.None;

    public bool invertDepthAxis { get; private set; }

    public void Initialize(Vector3 startPosition, CharacterDataSO characterData, bool invertDepth = false)
    {
        invertDepthAxis = invertDepth;
        config = characterData.config;
        currentFrame = 0;
        accumulatedHitstopFlags = InputFlags.None;
        accumulatedLogicFlags = InputFlags.None;

        inputTracker = new InputStateTracker();
        inputTracker.Initialize();

        actionRegistry = new ActionRegistry();
        actionRegistry.Initialize(characterData.GetAllRegisteredActions());

        physics = new PlayerPhysics();
        physics.Initialize(startPosition, config);

        combat = new PlayerCombat(config);

        actionController = new PlayerActionController();
        actionController.Initialize(this, characterData.animationMap.commandList, characterData.animationMap.comboTree, config.commandBufferWindow);

        stateMachine = new PlayerStateMachine();
        stateMachine.Initialize(this, config);
    }

    public void ExportState(ref PlayerSnapshot snapshot)
    {
        physics.ExportState(ref snapshot);
        combat.ExportState(ref snapshot);
        stateMachine.ExportState(ref snapshot);
        actionController.ExportState(ref snapshot);
        
        snapshot.inputTrackerState = inputTracker;
        snapshot.controllerFrame = currentFrame;
        snapshot.previousRawFlags = previousRawFlags;
        snapshot.accumulatedHitstopFlags = accumulatedHitstopFlags;
        snapshot.accumulatedLogicFlags = accumulatedLogicFlags;
    }

    public void ImportState(PlayerSnapshot snapshot)
    {
        physics.ImportState(snapshot);
        combat.ImportState(snapshot);
        stateMachine.ImportState(snapshot);
        actionController.ImportState(snapshot);
        
        inputTracker = snapshot.inputTrackerState;
        currentFrame = snapshot.controllerFrame;
        previousRawFlags = snapshot.previousRawFlags;
        accumulatedHitstopFlags = snapshot.accumulatedHitstopFlags;
        accumulatedLogicFlags = snapshot.accumulatedLogicFlags;
    }

public void UpdateTick(PlayerInput input, bool isLogicStep)
    {
        if (!isLogicStep) return;

        PlayerInput sanitizedInput = ApplyReleaseDebounce(input);
        
        inputTracker.UpdateTick(sanitizedInput.flags);
        
        sanitizedInput.flags = inputTracker.currentFlags;
        
        currentInput = sanitizedInput;
        currentKeyDownFlags = inputTracker.currentFlags & ~inputTracker.previousFlags;

        bool isHitstopActive = combat.GetHitstopCounter() > 0;
        isHitstopActive = combat.ProcessHitstopTick();

        if (isHitstopActive)
        {
            accumulatedHitstopFlags |= currentKeyDownFlags;
            return;
        }

        currentFrame++;
        sanitizedInput.frame = currentFrame; 
        
        InputFlags mergedKeyDown = currentKeyDownFlags | accumulatedHitstopFlags;
        accumulatedHitstopFlags = InputFlags.None;
        
        sanitizedInput.flags |= mergedKeyDown;

        PlayerState_Type currentState = stateMachine.GetCurrentState();
        actionController.ProcessInput(sanitizedInput, mergedKeyDown, currentFrame, currentState);

        bool isHoming = false;
        if (currentState == PlayerState_Type.Attacking)
        {
            ActionDataSO currentAction = stateMachine.GetCurrentActionData();
            bool hasValidFrameData = currentAction != null && currentAction.frameData != null;
            if (hasValidFrameData)
            {
                isHoming = currentAction.frameData.logicData.isHoming;
            }
        }

        physics.UpdateLookDirection(targetEntity, currentState, isHoming);
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
    public ActionRegistry GetActionRegistry() => actionRegistry;
    public PlayerConfigSO GetConfig() => config;
    public int GetCurrentFrame() => currentFrame;
    public InputStateTracker GetTracker() => inputTracker;

    public FPVector3 GetFPPosition() => physics.GetFPPosition(); 
    public FPVector3 GetFPLookDirection() => physics.GetFPLookDirection(); 
}