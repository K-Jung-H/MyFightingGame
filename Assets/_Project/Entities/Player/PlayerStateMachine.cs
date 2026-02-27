using UnityEngine;
using System.Collections.Generic;

public class PlayerStateMachine : ITargetable
{
    private Dictionary<PlayerState_Type, PlayerStateBase> states;
    private PlayerStateBase currentStateObject;
    public PlayerInput currentInput { get; private set; }
    protected PlayerState_Type currentState;
    protected Vector3 position;
    protected Vector3 currentDirection;
    protected Vector3 lookDirection;
    protected ITargetable targetEntity;
    
    protected PlayerInput previousInput;
    protected InputFlags currentKeyDownFlags;
    protected InputFlags lastTappedDirection;
    protected int lastTapFrame;
    protected int consecutiveTaps;
    protected int runningForwardFrames;
    protected int stateFrameCounter;
    protected int currentFrame;

    private PlayerConfigSO config;
    private CommandListSO commandList;
    private InputBuffer inputBuffer;
    
    private ComboTreeSO comboTree;
    private ComboNode currentComboNode;
    protected List<InputFlags> comboSequence = new List<InputFlags>();

    private bool isCommandActionTriggered;
    private CommandDefinition currentCommand;
    private CommandDefinition bufferedCommand;
    private int bufferedCommandFrame;

    private Dictionary<string, int> animationHashCache = new Dictionary<string, int>();

    private HurtInfo currentHurtInfo;
    private HashSet<int> registeredHitGroupIds = new HashSet<int>();

    public HurtInfo GetCurrentHurtInfo() => currentHurtInfo;

    public virtual void Initialize(Vector3 startPosition, PlayerConfigSO playerConfig, CommandListSO cmdList, ComboTreeSO comboTreeData)
    {
        position = startPosition;
        config = playerConfig;
        commandList = cmdList;
        comboTree = comboTreeData;
        
        if (commandList != null)
        {
            commandList.SortCommands();
        }
        
        inputBuffer = new InputBuffer(60);
        currentDirection = Vector3.forward;
        lookDirection = Vector3.forward;
        currentFrame = 0;

        InitializeStates();
        currentState = (PlayerState_Type)(-1); 
        TransitionTo(PlayerState_Type.Idle);
    }

    private void InitializeStates()
    {
        states = new Dictionary<PlayerState_Type, PlayerStateBase>();
        states.Add(PlayerState_Type.Idle, new IdleState(this, config));
        states.Add(PlayerState_Type.Walking, new WalkingState(this, config));
        states.Add(PlayerState_Type.Running, new RunningState(this, config));
        states.Add(PlayerState_Type.Sprinting, new SprintingState(this, config));
        states.Add(PlayerState_Type.Attacking, new AttackingState(this, config));
        states.Add(PlayerState_Type.Stun, new StunState(this, config));
        states.Add(PlayerState_Type.Hit, new HitState(this, config));    
    }

    public void ApplyHit(HurtInfo hurtData)
    {
        currentHurtInfo = hurtData;
        TransitionTo(PlayerState_Type.Hit, true);
    }

    public void TransitionTo(PlayerState_Type newState, bool forceTransition = false)
    {
        if (states == null || !states.ContainsKey(newState)) return;
        if (!forceTransition && currentState == newState) return;

        if (currentStateObject != null) currentStateObject.Exit();

        currentState = newState;
        currentStateObject = states[newState];
        stateFrameCounter = 0;

        if (newState != PlayerState_Type.Attacking)
        {
            registeredHitGroupIds.Clear();
        }
        
        currentStateObject.Enter();
    }

    public void AddToComboSequence(InputFlags attackInput) => comboSequence.Add(attackInput);

    public void ClearComboSequence()
    {
        comboSequence.Clear();
        currentComboNode = null;
    }

    public virtual void UpdateTick(PlayerInput input)
    {
        currentInput = input;
        currentKeyDownFlags = input.flags & ~previousInput.flags;
        currentFrame++;
        stateFrameCounter++;
        
        inputBuffer.AddInput(input);
        UpdateLookDirection();
        UpdateTapContext(input.flags);
        
        CommandDefinition matchedCommand = inputBuffer.CheckCommands(commandList, currentFrame, currentState);
        if (matchedCommand != null)
        {
            bufferedCommand = matchedCommand;
            bufferedCommandFrame = currentFrame;
            
            inputBuffer.Clear(); 
            ClearComboSequence();
            
            currentKeyDownFlags = InputFlags.None;
        }

        bool isCancelable = true;
        if (currentState == PlayerState_Type.Attacking)
        {
            int cancelFrame = currentStateObject is AttackingState atkState ? atkState.GetCancelWindow() : 999;
            isCancelable = stateFrameCounter >= cancelFrame;
        }

        if (isCancelable && bufferedCommand != null)
        {
            if (currentFrame - bufferedCommandFrame <= config.commandBufferWindow)
            {
                ExecuteBufferedCommand();
                return;
            }
        }

        if (currentStateObject != null)
        {
            currentStateObject.UpdateTick(input);
        }

        previousInput = input;
    }
    
    private void UpdateTapContext(InputFlags currentFlags)
    {
        InputFlags dirMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;
        InputFlags currentDir = currentFlags & dirMask;
        InputFlags prevDir = previousInput.flags & dirMask;

        if (currentDir != InputFlags.None && currentDir != prevDir)
        {
            if (currentDir == lastTappedDirection && (currentFrame - lastTapFrame) <= config.tapWindowFrames)
            {
                consecutiveTaps++;
            }
            else
            {
                consecutiveTaps = 1;
            }
            lastTappedDirection = currentDir;
            lastTapFrame = currentFrame;
        }
    }

    public void ProcessMovementLogic(PlayerInput input)
    {
        Vector3 rawInput = GetRawInputVector(input.flags);
        if (rawInput != Vector3.zero)
        {
            UpdateCurrentDirection(rawInput);
            ApplyMovement();
        }
        else
        {
            TransitionTo(PlayerState_Type.Idle);
            runningForwardFrames = 0;
            consecutiveTaps = 0;
        }
    }

    public Vector3 GetRawInputVector(InputFlags flags)
    {
        float x = ((flags & InputFlags.Right) != 0 ? 1 : 0) - ((flags & InputFlags.Left) != 0 ? 1 : 0);
        float z = ((flags & InputFlags.Up) != 0 ? 1 : 0) - ((flags & InputFlags.Down) != 0 ? 1 : 0);
        return new Vector3(x, 0, z).normalized;
    }

    private void UpdateCurrentDirection(Vector3 rawInput)
    {
        if (Vector3.Dot(currentDirection, rawInput) < -0.9f)
        {
            currentDirection = rawInput;
        }
        else
        {
            currentDirection = Vector3.Lerp(currentDirection, rawInput, config.turnLerpSpeed).normalized;
        }
    }

    private void ApplyMovement()
    {
        float speed = config.walkSpeed;
        if (currentState == PlayerState_Type.Running) speed = config.runSpeed;
        else if (currentState == PlayerState_Type.Sprinting) speed = config.sprintSpeed;

        Vector3 worldMoveDir = Quaternion.LookRotation(lookDirection) * currentDirection;
        position += worldMoveDir * speed;
    }

    protected virtual void UpdateLookDirection()
    {
        if (targetEntity == null || currentState == PlayerState_Type.Attacking || currentState == PlayerState_Type.Stun) return;
        Vector3 diff = targetEntity.GetPosition() - position;
        diff.y = 0;
        if (diff.sqrMagnitude > 0.0001f) lookDirection = diff.normalized;
    }

    public void ApplyPushback(Vector3 pushVector) => position += pushVector;
    public void IncrementRunningForwardFrames() => runningForwardFrames++;
    public int GetRunningForwardFrames() => runningForwardFrames;
    public int GetStateFrameCounter() => stateFrameCounter;
    public void SetTarget(ITargetable target) => targetEntity = target;
    public void SetPosition(Vector3 newPos) => position = newPos;
    public Vector3 GetPosition() => position;
    public Vector3 GetDirection() => currentDirection;
    public Vector3 GetLookDirection() => lookDirection;
    public PlayerState_Type GetCurrentState() => currentState;
    public int GetConsecutiveTaps() => consecutiveTaps;

    public float GetCurrentSpeed()
    {
        if (currentState == PlayerState_Type.Walking) return 1.0f;
        if (currentState == PlayerState_Type.Running) return 2.0f;
        if (currentState == PlayerState_Type.Sprinting) return 3.0f;
        return 0.0f;
    }

    public List<InputFlags> GetComboSequence() => comboSequence;
    public void ResetStateFrameCounter() => stateFrameCounter = 0;
    public void SetComboTree(ComboTreeSO tree) => comboTree = tree;
    public ComboTreeSO GetComboTree() => comboTree;
    public void SetCurrentComboNode(ComboNode node) => currentComboNode = node;
    public ComboNode GetCurrentComboNode() => currentComboNode;
    public PlayerInput GetPreviousInput() => previousInput;
    public InputFlags GetKeyDownFlags() => currentKeyDownFlags;
    public CommandDefinition GetCurrentCommand() => currentCommand;
    public void ClearCurrentCommand() => currentCommand = null;
    public void ClearInputBuffer()
    {
        inputBuffer.Clear();
    }
    public int GetCurrentAttackTriggerHash()
    {
        if (currentComboNode != null && currentComboNode.actionData != null && !string.IsNullOrEmpty(currentComboNode.actionData.animationStateName))
        {
            Debug.Log($"[AnimLog] 최종 결정된 콤보 애니메이션: {currentComboNode.actionData.animationStateName}");
            return GetAnimationHash(currentComboNode.actionData.animationStateName);        
        }
        return 0;
    }

    public bool CheckAndConsumeCommandAction(out int actionHash)
    {
        actionHash = 0;
        if (currentCommand != null && currentCommand.actionData != null)
        {
            actionHash = GetAnimationHash(currentCommand.actionData.animationStateName);

            if (isCommandActionTriggered)
            {
                Debug.Log($"[AnimLog] 최종 결정된 커맨드 스킬 애니메이션: {currentCommand.actionData.animationStateName}");
            }
        }
        bool triggered = isCommandActionTriggered;
        isCommandActionTriggered = false;
        return triggered;
    }

    public int GetAnimationHash(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return 0;
        if (!animationHashCache.TryGetValue(stateName, out int hash))
        {
            hash = Animator.StringToHash(stateName);
            animationHashCache.Add(stateName, hash);
        }
        return hash;
    }

    public PlayerConfigSO GetPlayerConfig() => config;

    public ActionDataSO GetCurrentActionData()
    {
        if (currentState != PlayerState_Type.Attacking) return null;
        if (currentCommand != null) return currentCommand.actionData;
        if (currentComboNode != null) return currentComboNode.actionData;
        return null;
    }

    public Hurtbox_Type GetCurrentHurtboxType()
    {
        if (currentState == PlayerState_Type.Attacking)
        {
            ActionDataSO currentAction = GetCurrentActionData();
            if (currentAction != null && currentAction.frameData.hurtboxEvents != null)
            {
                foreach (var evt in currentAction.frameData.hurtboxEvents)
                {
                    if (stateFrameCounter >= evt.startFrame && stateFrameCounter <= evt.endFrame)
                    {
                        return evt.hurtboxType;
                    }
                }
            }
        }
        return Hurtbox_Type.Standing;
    }

    public bool HasAlreadyHit(int hitGroupID) => registeredHitGroupIds.Contains(hitGroupID);
    public void RegisterHitGroup(int hitGroupID) => registeredHitGroupIds.Add(hitGroupID);

    public bool HasBufferedCommand() => bufferedCommand != null;
    public void SetBufferedCommand(CommandDefinition cmd) 
    {
        bufferedCommand = cmd;
        bufferedCommandFrame = currentFrame;
    }

    public void ExecuteBufferedCommand()
    {
        if (bufferedCommand == null) return;

        currentCommand = bufferedCommand;
        
        if (currentCommand.actionData != null && !string.IsNullOrEmpty(currentCommand.actionData.animationStateName))
        {
            isCommandActionTriggered = true;
        }

        inputBuffer.Clear();
        
        currentComboNode = null;
        ClearComboSequence();

        TransitionTo(currentCommand.targetState, true);
        
        bufferedCommand = null;
    }

    public CommandDefinition CheckBufferedCommand() => inputBuffer.CheckCommands(commandList, currentFrame, currentState);

}