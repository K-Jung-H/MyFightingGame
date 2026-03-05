using UnityEngine;
using System.Collections.Generic;

public class PlayerStateMachine : ITargetable
{
    private PlayerConfigSO config;
    private InputBuffer inputBuffer;
    private ActionResolver actionResolver;
    protected ITargetable targetEntity;

    private Dictionary<PlayerState_Type, PlayerStateBase> states;
    private PlayerStateBase currentStateObject;
    protected PlayerState_Type cachedCurrentState;

    public bool isGrounded { get; private set; }
    public Vector3 lastImpactVelocity { get; private set; }
    protected Vector3 position;
    protected Vector3 velocity;
    protected Vector3 currentDirection;
    protected Vector3 lookDirection;
    protected float globalGravity;
    protected bool isRootMotionActiveThisFrame;

    public PlayerInput currentInput { get; private set; }
    protected PlayerInput previousInput;
    protected InputFlags currentKeyDownFlags;

    private List<InputFlags> comboSequence = new List<InputFlags>();
    private ActionRequest? bufferedActionRequest;
    private int bufferedActionFrame;

    protected int currentFrame;
    protected int stateFrameCounter;

    private ActionDataSO currentActionData;
    private bool isCommandActionTriggered;
    private Dictionary<string, int> animationHashCache = new Dictionary<string, int>();

    private HurtInfo currentHurtInfo;
    private HashSet<int> registeredHitGroupIds = new HashSet<int>();
    protected int hitstopCounter;

    public virtual void Initialize(Vector3 startPosition, PlayerConfigSO playerConfig, CommandListSO cmdList, ComboTreeSO comboTreeData)
    {
        position = startPosition;
        velocity = Vector3.zero;
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
        cachedCurrentState = (PlayerState_Type)(-1); 
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
        states.Add(PlayerState_Type.StandHit, new StandHitState(this, config));
        states.Add(PlayerState_Type.AirHit, new AirHitState(this, config));
        states.Add(PlayerState_Type.Stunning, new StunningState(this, config));
        states.Add(PlayerState_Type.WakeUp, new WakeUpState(this, config));
        states.Add(PlayerState_Type.GroundSmash, new GroundSmashState(this, config));
        states.Add(PlayerState_Type.LayingDown, new LayingDownState(this, config));
    }

    public virtual void UpdateTick(PlayerInput input)
    {
        bool isHitstopActive = hitstopCounter > 0;
        if (isHitstopActive)
        {
            hitstopCounter--;
            return;
        }
        
        currentInput = input;
        currentKeyDownFlags = input.flags & ~previousInput.flags;
        currentFrame++;
        stateFrameCounter++;
        
        inputBuffer.AddInput(input);
        UpdateLookDirection();
        
        ActionRequest? evaluatedAction = actionResolver.EvaluateInput(inputBuffer, input.flags, currentKeyDownFlags, currentFrame, GetCurrentState(), comboSequence);

        if (evaluatedAction.HasValue)
        {
            bufferedActionRequest = evaluatedAction.Value;
            bufferedActionFrame = currentFrame;
        }

        bool isCancelable = true;
        bool isAttacking = GetCurrentState() == PlayerState_Type.Attacking;
        
        if (isAttacking)
        {
            int cancelFrame = currentStateObject is AttackingState atkState ? atkState.GetCancelWindow() : 999;
            isCancelable = stateFrameCounter >= cancelFrame;
        }

        bool isActionBuffered = bufferedActionRequest.HasValue;
        if (isCancelable && isActionBuffered)
        {
            bool isWithinBufferWindow = (currentFrame - bufferedActionFrame) <= config.commandBufferWindow;
            
            if (isWithinBufferWindow)
            {
                ActionRequest request = bufferedActionRequest.Value;
                bool isTargetingAttack = request.targetState == PlayerState_Type.Attacking;
                
                if (isTargetingAttack)
                {
                    if (CanTransitionToAttack())
                    {
                        ExecuteActionRequest(request);
                    }
                    else
                    {
                        bufferedActionRequest = null;
                    }
                }
                else
                {
                    ExecuteActionRequest(request);
                }
            }
            else
            {
                bufferedActionRequest = null;
            }
        }
        
        isRootMotionActiveThisFrame = false;

        bool isStateObjectValid = currentStateObject != null;
        if (isStateObjectValid)
        {
            currentStateObject.UpdateTick(input);
        }

        ProcessPhysics();

        previousInput = input;
    }

    public bool CanTransitionToAttack()
    {
        PlayerState_Type currentState = GetCurrentState();

        bool isHit = currentState == PlayerState_Type.StandHit || currentState == PlayerState_Type.AirHit;
        bool isDown = currentState == PlayerState_Type.LayingDown || currentState == PlayerState_Type.WakeUp || currentState == PlayerState_Type.GroundSmash;
        bool isStunned = currentState == PlayerState_Type.Stunning;
        bool isGrounded = this.isGrounded; 

        return !(isHit || isDown || isStunned);
    }

    private void ProcessPhysics()
    {
        float deceleration = globalGravity * config.gravityScale;
        
        velocity.x = Mathf.MoveTowards(velocity.x, 0f, deceleration);
        velocity.z = Mathf.MoveTowards(velocity.z, 0f, deceleration);

        if (!isRootMotionActiveThisFrame)
        {
            velocity.y -= deceleration;
        }
        
        position += velocity;

        isGrounded = position.y <= 0f;

        if (isGrounded)
        {
            bool isFalling = velocity.y < 0f;
            if (isFalling)
            {
                lastImpactVelocity = velocity;
                velocity.y = 0f;
            }
            position.y = 0f;
        }
        else
        {
            lastImpactVelocity = Vector3.zero;
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
        }
    }

    private void ApplyMovement()
    {
        float speed = config.walkSpeed;
        PlayerState_Type stateType = GetCurrentState();
        
        bool isRunning = stateType == PlayerState_Type.Running;
        bool isSprinting = stateType == PlayerState_Type.Sprinting;
        
        if (isRunning) speed = config.runSpeed;
        else if (isSprinting) speed = config.sprintSpeed;

        Vector3 worldMoveDir = Quaternion.LookRotation(lookDirection) * currentDirection;
        position += worldMoveDir * speed;
    }

    protected virtual void UpdateLookDirection()
    {
        bool isLookUpdateDisabled = targetEntity == null || GetCurrentState() == PlayerState_Type.Attacking;
        if (isLookUpdateDisabled) return;

        Vector3 diff = targetEntity.GetPosition() - position;
        diff.y = 0;
        
        bool isTargetValid = diff.sqrMagnitude > 0.0001f;
        if (isTargetValid)
        {
            lookDirection = diff.normalized;
        }
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

    public void TransitionTo(PlayerState_Type newState, bool forceTransition = false)
    {
        bool isStateInvalid = states == null || !states.ContainsKey(newState);
        if (isStateInvalid) return;
        
        bool isSameState = !forceTransition && cachedCurrentState == newState;
        if (isSameState) return;

        bool isCurrentStateValid = currentStateObject != null;
        if (isCurrentStateValid) currentStateObject.Exit();

        cachedCurrentState = newState;
        currentStateObject = states[newState];
        stateFrameCounter = 0;

        bool isNotAttacking = newState != PlayerState_Type.Attacking;
        if (isNotAttacking)
        {
            registeredHitGroupIds.Clear();
        }
        
        currentStateObject.Enter();
    }

    private void ExecuteActionRequest(ActionRequest request)
    {
        currentActionData = request.actionData;
        isCommandActionTriggered = request.isCommandAction;

        bool hasActionData = currentActionData != null;
        if (hasActionData)
        {
            Debug.Log($"[Attack Triggered] Action: {currentActionData.name}, AnimState: {currentActionData.animationStateName}, IsCommand: {isCommandActionTriggered}");
        }

        registeredHitGroupIds.Clear();

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

    public void ApplyHit(HurtInfo hurtData)
    {
        PlayerState_Type currentStateType = GetCurrentState();
     
        currentHurtInfo = hurtData;
        Vector3 finalPushback = hurtData.pushbackVector;

        bool isAlreadyInAirHit = currentStateType == PlayerState_Type.AirHit || 
                                 currentStateType == PlayerState_Type.GroundSmash ||
                                 currentStateType == PlayerState_Type.LayingDown ||
                                 currentStateType == PlayerState_Type.WakeUp;

        bool isJuggleBumpNeeded = (!isGrounded || isAlreadyInAirHit) && finalPushback.y < 0.25f;
        if (isJuggleBumpNeeded)
        {
            finalPushback.y = 0.25f; 
        }

        SetVelocity(finalPushback);

        PlayerState_Type nextState = PlayerState_Type.StandHit;

        if (isAlreadyInAirHit)
        {
            nextState = PlayerState_Type.AirHit;
        }
        else
        {
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
                    nextState = PlayerState_Type.Stunning;
                    break;
            }
        }

        ClearComboSequence();

        TransitionTo(nextState, true);
    }

    public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        Vector3 worldDeltaPos = Quaternion.LookRotation(currentDirection) * deltaPosition;
        position += worldDeltaPos;

        currentDirection = deltaRotation * currentDirection;
        lookDirection = deltaRotation * lookDirection;

        isRootMotionActiveThisFrame = true;
        velocity.y = 0f;
    }

    public Vector3 GetVelocity() => velocity;
    public void SetVelocity(Vector3 targetVelocity) => velocity = targetVelocity;

    public void ApplyHitstop(int frames) => hitstopCounter = frames;
    public int GetHitstopCounter() => hitstopCounter;

    public void ApplyPushback(Vector3 pushVector) => position += pushVector;
    
    public void SetPosition(Vector3 newPos) => position = newPos;
    public Vector3 GetPosition() => position;

    public void SetGlobalGravity(float gravity) => globalGravity = gravity;
    public void SetTarget(ITargetable target) => targetEntity = target;

    public Vector3 GetDirection() => currentDirection;
    public Vector3 GetLookDirection() => lookDirection;
    
    public PlayerState_Type GetCurrentState() => cachedCurrentState;
    public int GetStateFrameCounter() => stateFrameCounter;
    
    public PlayerStateBase GetStateObject(PlayerState_Type stateType)
    {
        states.TryGetValue(stateType, out PlayerStateBase state);
        return state;
    }

    public PlayerInput GetPreviousInput() => previousInput;
    public InputFlags GetKeyDownFlags() => currentKeyDownFlags;
    public void ClearInputBuffer() => inputBuffer.Clear();

    public float GetCurrentSpeed()
    {
        PlayerState_Type stateType = GetCurrentState();
        bool isWalking = stateType == PlayerState_Type.Walking;
        bool isRunning = stateType == PlayerState_Type.Running;
        bool isSprinting = stateType == PlayerState_Type.Sprinting;

        if (isWalking) return 1.0f;
        if (isRunning) return 2.0f;
        if (isSprinting) return 3.0f;
        return 0.0f;
    }

    public Vector3 GetRawInputVector(InputFlags flags)
    {
        float x = ((flags & InputFlags.Right) != 0 ? 1 : 0) - ((flags & InputFlags.Left) != 0 ? 1 : 0);
        float z = ((flags & InputFlags.Up) != 0 ? 1 : 0) - ((flags & InputFlags.Down) != 0 ? 1 : 0);
        return new Vector3(x, 0, z).normalized;
    }

    public void ClearComboSequence() => comboSequence.Clear();
    public List<InputFlags> GetComboSequence() => comboSequence;

    public ActionDataSO GetCurrentActionData() => currentActionData;
    public void ClearCurrentAction() => currentActionData = null;

    public HurtInfo GetCurrentHurtInfo() => currentHurtInfo;
    public bool HasAlreadyHit(int hitGroupID) => registeredHitGroupIds.Contains(hitGroupID);
    public void RegisterHitGroup(int hitGroupID) => registeredHitGroupIds.Add(hitGroupID);

    public PlayerConfigSO GetPlayerConfig() => config;

    public int GetCurrentAttackTriggerHash()
    {
        bool hasValidActionData = currentActionData != null;
        if (hasValidActionData)
        {
            bool hasValidActionName = !string.IsNullOrEmpty(currentActionData.animationStateName);
            if (hasValidActionName)
            {
                return GetAnimationHash(currentActionData.animationStateName);        
            }
        }
        return 0;
    }

    public bool CheckAndConsumeCommandAction(out int actionHash)
    {
        actionHash = 0;
        bool hasValidActionData = currentActionData != null;
        
        if (hasValidActionData && isCommandActionTriggered)
        {
            actionHash = GetAnimationHash(currentActionData.animationStateName);
        }
        
        bool isTriggered = isCommandActionTriggered;
        isCommandActionTriggered = false;
        return isTriggered;
    }

    public int GetAnimationHash(string stateName)
    {
        bool isStateNameEmpty = string.IsNullOrEmpty(stateName);
        if (isStateNameEmpty)
        {
            Debug.LogWarning("[Animation Error] Attempted to get hash for an empty or null state name.");
            return 0;
        }
        
        bool hasHash = animationHashCache.TryGetValue(stateName, out int hash);
        if (!hasHash)
        {
            hash = Animator.StringToHash(stateName);
            animationHashCache.Add(stateName, hash);
        }
        return hash;
    }

    public Hurtbox_Type GetCurrentHurtboxType()
    {
        bool isAttacking = GetCurrentState() == PlayerState_Type.Attacking;
        bool hasValidActionData = currentActionData != null;
        
        if (isAttacking && hasValidActionData)
        {
            bool hasHurtboxEvents = currentActionData.frameData.hurtboxEvents != null;
            if (hasHurtboxEvents)
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
        }
        return Hurtbox_Type.Standing;
    }
}