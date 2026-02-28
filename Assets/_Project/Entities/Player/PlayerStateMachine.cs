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
    protected float yVelocity;
    protected ITargetable targetEntity;
    
    protected PlayerInput previousInput;
    protected InputFlags currentKeyDownFlags;
    protected InputFlags lastTappedDirection;
    protected int lastTapFrame;
    protected int consecutiveTaps;
    protected int runningForwardFrames;
    protected int stateFrameCounter;
    protected int currentFrame;
    protected float globalGravity;

    private PlayerConfigSO config;
    private InputBuffer inputBuffer;
    private ActionResolver actionResolver;
    
    private List<InputFlags> comboSequence = new List<InputFlags>();
    private ActionRequest? bufferedActionRequest;
    private int bufferedActionFrame;
    private ActionDataSO currentActionData;
    private bool isCommandActionTriggered;

    private Dictionary<string, int> animationHashCache = new Dictionary<string, int>();

    private HurtInfo currentHurtInfo;
    private HashSet<int> registeredHitGroupIds = new HashSet<int>();

    public virtual void Initialize(Vector3 startPosition, PlayerConfigSO playerConfig, CommandListSO cmdList, ComboTreeSO comboTreeData)
    {
        position = startPosition;
        yVelocity = 0f;
        config = playerConfig;
        
        if (cmdList != null)
        {
            cmdList.SortCommands();
        }
        
        inputBuffer = new InputBuffer(60);
        currentDirection = Vector3.forward;
        lookDirection = Vector3.forward;
        currentFrame = 0;

        actionResolver = new ActionResolver();
        actionResolver.Initialize(cmdList, comboTreeData);

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
        states.Add(PlayerState_Type.StandHit, new StandHitState(this, config));
        states.Add(PlayerState_Type.AirHit, new AirHitState(this, config));
        states.Add(PlayerState_Type.Knockdown, new KnockdownState(this, config));
        states.Add(PlayerState_Type.WakeUp, new WakeUpState(this, config));
    }

    public void ApplyHit(HurtInfo hurtData)
    {
        currentHurtInfo = hurtData;
        
        bool isAirborneAttack = hurtData.targetHurtState == HurtState_Type.AirHit;
        if (isAirborneAttack)
        {
            SetYVelocity(hurtData.pushbackVector.y);
        }

        PlayerState_Type nextState = PlayerState_Type.StandHit;
        switch (hurtData.targetHurtState)
        {
            case HurtState_Type.StandHit:
            case HurtState_Type.GuardHit:
                nextState = PlayerState_Type.StandHit;
                break;
            case HurtState_Type.AirHit:
                nextState = PlayerState_Type.AirHit;
                break;
            case HurtState_Type.KnockDown:
            case HurtState_Type.GroundHit:
                nextState = PlayerState_Type.Knockdown;
                break;
        }

        TransitionTo(nextState, true);
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

    public virtual void UpdateTick(PlayerInput input)
    {
        currentInput = input;
        currentKeyDownFlags = input.flags & ~previousInput.flags;
        currentFrame++;
        stateFrameCounter++;
        
        inputBuffer.AddInput(input);
        UpdateLookDirection();
        UpdateTapContext(input.flags);
        
        ActionRequest? evaluatedAction = actionResolver.EvaluateInput(inputBuffer, input.flags, currentKeyDownFlags, currentFrame, currentState, comboSequence);

        if (evaluatedAction.HasValue)
        {
            bufferedActionRequest = evaluatedAction.Value;
            bufferedActionFrame = currentFrame;
        }

        bool isCancelable = true;
        if (currentState == PlayerState_Type.Attacking)
        {
            int cancelFrame = currentStateObject is AttackingState atkState ? atkState.GetCancelWindow() : 999;
            isCancelable = stateFrameCounter >= cancelFrame;
        }

        if (isCancelable && bufferedActionRequest.HasValue)
        {
            bool isWithinBufferWindow = (currentFrame - bufferedActionFrame) <= config.commandBufferWindow;
            if (isWithinBufferWindow)
            {
                ExecuteActionRequest(bufferedActionRequest.Value);
            }
            else
            {
                bufferedActionRequest = null;
            }
        }

        if (currentStateObject != null)
        {
            currentStateObject.UpdateTick(input);
        }

        ProcessPhysics();

        previousInput = input;
    }

    private void ProcessPhysics()
    {
        bool isAlreadyGrounded = position.y <= 0f && yVelocity <= 0f;
        if (isAlreadyGrounded)
        {
            position.y = 0f;
            yVelocity = 0f;
            return;
        }

        yVelocity -= globalGravity * config.gravityScale;
        position.y += yVelocity;

        bool isGrounded = position.y <= 0f;
        if (isGrounded)
        {
            position.y = 0f;
            yVelocity = 0f;
        }
    }
    
    private void ExecuteActionRequest(ActionRequest request)
    {
        currentActionData = request.actionData;
        isCommandActionTriggered = request.isCommandAction;

        if (request.isCommandAction)
        {
            ClearComboSequence();
        }
        else if (request.comboNode != null)
        {
            comboSequence.Add(request.comboNode.requiredInput);
        }

        inputBuffer.Clear();
        bufferedActionRequest = null;
        TransitionTo(request.targetState, true);
    }

    private void UpdateTapContext(InputFlags currentFlags)
    {
        InputFlags dirMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;
        InputFlags currentDir = currentFlags & dirMask;
        InputFlags prevDir = previousInput.flags & dirMask;

        bool isDirectionChanged = currentDir != InputFlags.None && currentDir != prevDir;
        if (isDirectionChanged)
        {
            bool isConsecutiveTap = currentDir == lastTappedDirection && (currentFrame - lastTapFrame) <= config.tapWindowFrames;
            if (isConsecutiveTap)
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
        bool hasMovementInput = rawInput != Vector3.zero;

        if (hasMovementInput)
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
        bool isOppositeDirection = Vector3.Dot(currentDirection, rawInput) < -0.9f;
        if (isOppositeDirection)
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
        bool isLookUpdateDisabled = targetEntity == null || currentState == PlayerState_Type.Attacking || currentState == PlayerState_Type.Stun;
        if (isLookUpdateDisabled) return;

        Vector3 diff = targetEntity.GetPosition() - position;
        diff.y = 0;
        
        bool isTargetValid = diff.sqrMagnitude > 0.0001f;
        if (isTargetValid)
        {
            lookDirection = diff.normalized;
        }
    }

    public void ApplyPushback(Vector3 pushVector) => position += pushVector;
    public void SetYVelocity(float newYVelocity) => yVelocity = newYVelocity;
    public float GetYVelocity() => yVelocity;
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
    public void SetGlobalGravity(float gravity) => globalGravity = gravity;

    public float GetCurrentSpeed()
    {
        if (currentState == PlayerState_Type.Walking) return 1.0f;
        if (currentState == PlayerState_Type.Running) return 2.0f;
        if (currentState == PlayerState_Type.Sprinting) return 3.0f;
        return 0.0f;
    }

    public void ClearComboSequence() => comboSequence.Clear();
    public PlayerInput GetPreviousInput() => previousInput;
    public InputFlags GetKeyDownFlags() => currentKeyDownFlags;
    public void ClearInputBuffer() => inputBuffer.Clear();

    public int GetCurrentAttackTriggerHash()
    {
        bool hasValidActionName = currentActionData != null && !string.IsNullOrEmpty(currentActionData.animationStateName);
        if (hasValidActionName)
        {
            return GetAnimationHash(currentActionData.animationStateName);        
        }
        return 0;
    }

    public bool CheckAndConsumeCommandAction(out int actionHash)
    {
        actionHash = 0;
        if (currentActionData != null && isCommandActionTriggered)
        {
            actionHash = GetAnimationHash(currentActionData.animationStateName);
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
    public ActionDataSO GetCurrentActionData() => currentActionData;
    public List<InputFlags> GetComboSequence() => comboSequence;
    public void ClearCurrentAction() => currentActionData = null;
    public HurtInfo GetCurrentHurtInfo() => currentHurtInfo;
    public bool HasAlreadyHit(int hitGroupID) => registeredHitGroupIds.Contains(hitGroupID);
    public void RegisterHitGroup(int hitGroupID) => registeredHitGroupIds.Add(hitGroupID);

    public Hurtbox_Type GetCurrentHurtboxType()
    {
        bool isAttackingWithHurtboxes = currentState == PlayerState_Type.Attacking && currentActionData != null && currentActionData.frameData.hurtboxEvents != null;
        
        if (isAttackingWithHurtboxes)
        {
            foreach (var evt in currentActionData.frameData.hurtboxEvents)
            {
                bool isWithinHurtboxFrame = stateFrameCounter >= evt.startFrame && stateFrameCounter <= evt.endFrame;
                if (isWithinHurtboxFrame)
                {
                    return evt.hurtboxType;
                }
            }
        }
        return Hurtbox_Type.Standing;
    }
}