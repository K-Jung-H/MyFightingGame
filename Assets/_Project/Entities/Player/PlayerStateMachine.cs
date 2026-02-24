using UnityEngine;
using System.Collections.Generic;

public class PlayerStateMachine : ITargetable
{
    private Dictionary<PlayerState, PlayerStateBase> states;
    private PlayerStateBase currentStateObject;
    
    protected PlayerState currentState;
    protected Vector3 position;
    protected Vector3 currentDirection;
    protected Vector3 lookDirection;
    protected ITargetable targetEntity;
    
    protected PlayerInput previousInput;
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

        currentState = (PlayerState)(-1); 
        
        TransitionTo(PlayerState.Idle);
    }

    private void InitializeStates()
    {
        states = new Dictionary<PlayerState, PlayerStateBase>();
        states.Add(PlayerState.Idle, new IdleState(this, config));
        states.Add(PlayerState.Walking, new WalkingState(this, config));
        states.Add(PlayerState.Running, new RunningState(this, config));
        states.Add(PlayerState.Sprinting, new SprintingState(this, config));
        states.Add(PlayerState.Attacking, new AttackingState(this, config));
        states.Add(PlayerState.Stun, new StunState(this, config));
    }

    public void TransitionTo(PlayerState newState)
    {
        if (states == null || !states.ContainsKey(newState)) return;
        if (currentState == newState) return;

        if (currentStateObject != null) currentStateObject.Exit();

        currentState = newState;
        currentStateObject = states[newState];
        stateFrameCounter = 0;
        
        currentStateObject.Enter();
    }

    public void AddToComboSequence(InputFlags attackInput)
    {
        comboSequence.Add(attackInput);
    }

    public void ClearComboSequence()
    {
        comboSequence.Clear();
        currentComboNode = null;
    }

    public virtual void UpdateTick(PlayerInput input)
    {
        currentFrame++;
        stateFrameCounter++;
        
        inputBuffer.AddInput(input);
        UpdateTapContext(input.flags);
        UpdateLookDirection();

        if (commandList != null)
        {
            CommandDefinition matchedCommand = inputBuffer.CheckCommands(commandList, currentFrame, currentState);
            if (matchedCommand != null)
            {
                Debug.Log($"[Frame {currentFrame}] Command Triggered: {matchedCommand.commandName} -> State: {matchedCommand.targetState}");
                inputBuffer.Clear();

                TransitionTo(matchedCommand.targetState);
                previousInput = input;
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
            TransitionTo(PlayerState.Idle);
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
        if (currentState == PlayerState.Running) speed = config.runSpeed;
        else if (currentState == PlayerState.Sprinting) speed = config.sprintSpeed;

        Vector3 worldMoveDir = Quaternion.LookRotation(lookDirection) * currentDirection;
        position += worldMoveDir * speed;
    }

    protected virtual void UpdateLookDirection()
    {
        if (targetEntity == null || currentState == PlayerState.Attacking || currentState == PlayerState.Stun) return;
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
    public PlayerState GetCurrentState() => currentState;
    public int GetConsecutiveTaps() => consecutiveTaps;


    public float GetCurrentSpeed()
    {
        if (currentState == PlayerState.Walking) return 1.0f;
        if (currentState == PlayerState.Running) return 2.0f;
        if (currentState == PlayerState.Sprinting) return 3.0f;
        return 0.0f;
    }

    public List<InputFlags> GetComboSequence() => comboSequence;
    public void ResetStateFrameCounter() => stateFrameCounter = 0;
    
    public void SetComboTree(ComboTreeSO tree) => comboTree = tree;
    public ComboTreeSO GetComboTree() => comboTree;
    
    public void SetCurrentComboNode(ComboNode node) => currentComboNode = node;
    public ComboNode GetCurrentComboNode() => currentComboNode;

    public PlayerInput GetPreviousInput() => previousInput;
}