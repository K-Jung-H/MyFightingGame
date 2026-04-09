using UnityEngine;
using System.Collections.Generic;

public class PlayerStateMachine : ISnapshotSync
{
    private PlayerController controller;
    private PlayerConfigSO config;
    private Dictionary<PlayerState_Type, PlayerStateBase> states;
    private PlayerStateBase currentStateObject;
    private PlayerState_Type cachedCurrentState;
    private PlayerState_Type previousStateType;
    private int stateFrameCounter;
    private ActionDataSO currentActionData;
    private bool isCommandActionTriggered;
    private Dictionary<string, int> animationHashCache = new Dictionary<string, int>();

    public void ExportState(ref PlayerSnapshot snapshot)
    {
        snapshot.cachedCurrentState = cachedCurrentState;
        snapshot.previousStateType = previousStateType;
        snapshot.stateFrameCounter = stateFrameCounter;
        snapshot.isCommandActionTriggered = isCommandActionTriggered;
        
        snapshot.currentActionID = controller.GetActionRegistry().GetActionID(currentActionData);
        
        WakeUpState wakeUpState = GetStateObject(PlayerState_Type.WakeUp) as WakeUpState;
        bool isWakeUpStateValid = wakeUpState != null;
        if (isWakeUpStateValid)
        {
            snapshot.scheduledWakeUpType = wakeUpState.GetScheduledWakeUpType();
        }

        LayingDownState layState = GetStateObject(PlayerState_Type.LayingDown) as LayingDownState;
        bool isLayStateValid = layState != null;
        if (isLayStateValid)
        {
            snapshot.isFromRoll = layState.IsFromRoll();
        }

        SideStepState sideStep = GetStateObject(PlayerState_Type.SideStep) as SideStepState;
        bool isSideStepValid = sideStep != null;
        if (isSideStepValid)
        {
            snapshot.sideStepDirection = sideStep.GetStepDirection();
        }

        HurtStateBase currentHurt = currentStateObject as HurtStateBase;
        bool isHurtValid = currentHurt != null;
        if (isHurtValid)
        {
            snapshot.currentStunFrames = currentHurt.GetCurrentStunFrames();
        }

        GroundSmashState smashState = GetStateObject(PlayerState_Type.GroundSmash) as GroundSmashState;
        bool isSmashStateValid = smashState != null;
        if (isSmashStateValid)
        {
            snapshot.isGroundBouncing = smashState.GetIsBouncing();
        }
    }

    public void ImportState(PlayerSnapshot snapshot)
    {
        cachedCurrentState = snapshot.cachedCurrentState;
        previousStateType = snapshot.previousStateType;
        stateFrameCounter = snapshot.stateFrameCounter;
        isCommandActionTriggered = snapshot.isCommandActionTriggered;

        currentActionData = controller.GetActionRegistry().GetAction(snapshot.currentActionID);
        
        currentStateObject = GetStateObject(cachedCurrentState);

        WakeUpState wakeUpState = GetStateObject(PlayerState_Type.WakeUp) as WakeUpState;
        bool isWakeUpStateValid = wakeUpState != null;
        if (isWakeUpStateValid)
        {
            wakeUpState.SetWakeUpType(snapshot.scheduledWakeUpType);
        }

        LayingDownState layState = GetStateObject(PlayerState_Type.LayingDown) as LayingDownState;
        bool isLayStateValid = layState != null;
        if (isLayStateValid)
        {
            layState.SetFromRoll(snapshot.isFromRoll);
        }

        SideStepState sideStep = GetStateObject(PlayerState_Type.SideStep) as SideStepState;
        bool isSideStepValid = sideStep != null;
        if (isSideStepValid)
        {
            sideStep.SetStepDirection(snapshot.sideStepDirection);
        }

        HurtStateBase currentHurt = currentStateObject as HurtStateBase;
        bool isHurtValid = currentHurt != null;
        if (isHurtValid)
        {
            currentHurt.SetCurrentStunFrames(snapshot.currentStunFrames);
        }

        GroundSmashState smashState = GetStateObject(PlayerState_Type.GroundSmash) as GroundSmashState;
        bool isSmashStateValid = smashState != null;
        if (isSmashStateValid)
        {
            smashState.SetIsBouncing(snapshot.isGroundBouncing);
        }
    }

    public void Initialize(PlayerController playerController, PlayerConfigSO playerConfig)
    {
        controller = playerController;
        config = playerConfig;

        InitializeStates();
        cachedCurrentState = (PlayerState_Type)(-1);
        previousStateType = (PlayerState_Type)(-1);
        TransitionTo(PlayerState_Type.Idle);
    }

    private void InitializeStates()
    {
        states = new Dictionary<PlayerState_Type, PlayerStateBase>
        {
            { PlayerState_Type.Idle, new IdleState(this, config) },
            { PlayerState_Type.Walking, new WalkingState(this, config) },
            { PlayerState_Type.Running, new RunningState(this, config) },
            { PlayerState_Type.Sprinting, new SprintingState(this, config) },
            { PlayerState_Type.Attacking, new AttackingState(this, config) },
            { PlayerState_Type.Crouching, new CrouchingState(this, config) },
            { PlayerState_Type.SideStep, new SideStepState(this, config) },
            { PlayerState_Type.SideWalk, new SideWalkState(this, config) },

            { PlayerState_Type.StandHit, new StandHitState(this, config) },
            { PlayerState_Type.CrouchHit, new CrouchHitState(this, config) },
            { PlayerState_Type.StandBlock, new StandBlockState(this, config) },
            { PlayerState_Type.CrouchBlock, new CrouchBlockState(this, config) },

            { PlayerState_Type.AirHit, new AirHitState(this, config) },
            { PlayerState_Type.Stunning, new StunningState(this, config) },
            { PlayerState_Type.GroundSmash, new GroundSmashState(this, config) },
            { PlayerState_Type.LayingDown, new LayingDownState(this, config) },
            { PlayerState_Type.WakeUp, new WakeUpState(this, config) },

            { PlayerState_Type.Dead, new DeadState(this, config) },
            { PlayerState_Type.Defeat, new DefeatState(this, config) },
            { PlayerState_Type.Win, new WinState(this, config) }
        };
    }

    public void UpdateTick(PlayerInput input)
    {
        stateFrameCounter++;

        bool isStateObjectValid = currentStateObject != null;
        if (isStateObjectValid)
        {
            currentStateObject.UpdateTick(input);
        }
    }

    public void TransitionTo(PlayerState_Type newState, bool forceTransition = false)
    {
        bool isStateInvalid = states == null || !states.ContainsKey(newState);
        if (isStateInvalid) return;

        bool isSameState = !forceTransition && cachedCurrentState == newState;
        if (isSameState) return;

        bool isCurrentStateValid = currentStateObject != null;
        if (isCurrentStateValid)
        {
            currentStateObject.Exit();
            previousStateType = cachedCurrentState;
        }

        cachedCurrentState = newState;
        currentStateObject = states[newState];
        stateFrameCounter = 0;

        bool isNotAttacking = newState != PlayerState_Type.Attacking;
        if (isNotAttacking)
        {
            controller.GetCombat().ClearRegisteredHitGroupIds();
        }

        currentStateObject.Enter();
    }

    public void ExecuteActionRequest(ActionRequest request, PlayerActionController actionController)
    {
        currentActionData = request.actionData;
        isCommandActionTriggered = request.isCommandAction;

        controller.GetCombat().ClearRegisteredHitGroupIds();

        if (request.isCommandAction)
        {
            actionController.ClearComboSequence();
        }
        else if (request.comboNode != null)
        {
            actionController.AddToComboSequence(request.comboNode.requiredInput);
        }

        actionController.ClearAllBuffers();
        TransitionTo(request.targetState, true);
    }

    public bool CanTransitionToAttack()
    {
        bool isHit = cachedCurrentState == PlayerState_Type.StandHit || cachedCurrentState == PlayerState_Type.AirHit;
        bool isDown = cachedCurrentState == PlayerState_Type.LayingDown || cachedCurrentState == PlayerState_Type.WakeUp || cachedCurrentState == PlayerState_Type.GroundSmash;
        bool isStunned = cachedCurrentState == PlayerState_Type.Stunning;
        bool isMatchEnd = cachedCurrentState == PlayerState_Type.Dead || cachedCurrentState == PlayerState_Type.Defeat || cachedCurrentState == PlayerState_Type.Win;

        bool isAttacking = cachedCurrentState == PlayerState_Type.Attacking;
        bool isCancelable = true;

        if (isAttacking)
        {
            isCancelable = stateFrameCounter >= currentStateObject.GetCancelWindow();
        }

        return !(isHit || isDown || isStunned || isMatchEnd) && isCancelable;
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
        bool hasValidActionData = currentActionData != null && currentActionData.frameData.hurtboxEvents != null;
        
        if (hasValidActionData)
        {
            foreach (var evt in currentActionData.frameData.hurtboxEvents)
            {
                bool isFrameMatch = stateFrameCounter >= evt.startFrame && stateFrameCounter <= evt.endFrame;
                if (isFrameMatch)
                {
                    return evt.hurtboxType;
                }
            }
        }

        switch (cachedCurrentState)
        {
            case PlayerState_Type.Crouching:
            case PlayerState_Type.CrouchBlock:
            case PlayerState_Type.CrouchHit:
                return Hurtbox_Type.Crouching;


            case PlayerState_Type.LayingDown:
            case PlayerState_Type.GroundSmash:
            case PlayerState_Type.Dead:
                return Hurtbox_Type.Laying;

            case PlayerState_Type.AirHit:
            case PlayerState_Type.WakeUp:
                return Hurtbox_Type.Airborne;

            case PlayerState_Type.Defeat:
            case PlayerState_Type.Win:
                return Hurtbox_Type.Invincible;

            default:
                return Hurtbox_Type.Standing;
        }
    }

    public PlayerState_Type GetCurrentState() => cachedCurrentState;
    public PlayerState_Type GetPreviousStateType() => previousStateType;
    public int GetStateFrameCounter() => stateFrameCounter;
    public ActionDataSO GetCurrentActionData() => currentActionData;
    public void ClearCurrentAction() => currentActionData = null;
    public PlayerStateBase GetStateObject(PlayerState_Type stateType)
    {
        states.TryGetValue(stateType, out PlayerStateBase state);
        return state;
    }
    public PlayerController GetController() => controller;
}