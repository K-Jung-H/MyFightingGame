using UnityEngine;
using System.Collections.Generic;

public abstract class PlayerStateBase
{
    protected PlayerStateMachine stateMachine;
    protected PlayerConfigSO config;

    public PlayerStateBase(PlayerStateMachine sm, PlayerConfigSO cfg)
    {
        stateMachine = sm;
        config = cfg;
    }

    public abstract PlayerState GetStateType();
    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void UpdateTick(PlayerInput input);
}

public class IdleState : PlayerStateBase
{
    public IdleState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState GetStateType() => PlayerState.Idle;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState.Attacking);
            return;
        }

        if (stateMachine.GetRawInputVector(input.flags) != Vector3.zero)
        {
            stateMachine.TransitionTo(PlayerState.Walking);
        }
    }
}

public class StunState : PlayerStateBase
{
    public StunState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState GetStateType() => PlayerState.Stun;

    public override void UpdateTick(PlayerInput input) { }
}

public class WalkingState : PlayerStateBase
{
    public WalkingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState GetStateType() => PlayerState.Walking;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState.Attacking);
            return;
        }

        stateMachine.ProcessMovementLogic(input);
        
    }
}

public class RunningState : PlayerStateBase
{
    public RunningState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState GetStateType() => PlayerState.Running;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState.Attacking);
            return;
        }

        stateMachine.ProcessMovementLogic(input);

        bool isForward = (input.flags & InputFlags.Up) != 0;
        
        if (isForward)
        {
            stateMachine.IncrementRunningForwardFrames();
            if (stateMachine.GetRunningForwardFrames() >= config.autoSprintFrames)
            {
                stateMachine.TransitionTo(PlayerState.Sprinting);
            }
        }
    }
}

public class SprintingState : PlayerStateBase
{
    public SprintingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState GetStateType() => PlayerState.Sprinting;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState.Attacking);
            return;
        }

        stateMachine.ProcessMovementLogic(input);

        bool isForward = (input.flags & InputFlags.Up) != 0;
        
        if (!isForward)
        {
            stateMachine.TransitionTo(PlayerState.Running);
        }
    }
}

public class AttackingState : PlayerStateBase
{
    private InputFlags bufferedAttackInput;
    private AnimationFrameData currentFrameData;

    public AttackingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState GetStateType() => PlayerState.Attacking;
    public int GetCancelWindow() => currentFrameData.cancelWindowStartFrame;
    public override void Enter()
    {
        bufferedAttackInput = InputFlags.None;

        if (stateMachine.GetComboSequence().Count == 0)
        {
            InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
            InputFlags dirMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;
            
            InputFlags initialAttack = stateMachine.currentInput.flags & (attackMask | dirMask);
            stateMachine.AddToComboSequence(initialAttack);
            
            bool isValidCombo = EvaluateNextComboAttack();
            if (!isValidCombo)
            {
                stateMachine.ClearComboSequence();
                stateMachine.TransitionTo(PlayerState.Idle);
            }
        }
        else
        {
            UpdateCurrentFrameData();
        }
    }
    
    private void UpdateCurrentFrameData()
    {
        CommandDefinition currentCommand = stateMachine.GetCurrentCommand();
        if (currentCommand != null)
        {
            currentFrameData = currentCommand.frameData;
            return;
        }

        ComboNode currentNode = stateMachine.GetCurrentComboNode();
        if (currentNode != null)
        {
            currentFrameData = currentNode.frameData;
        }
        else
        {
            currentFrameData = new AnimationFrameData();
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        int totalFrames = currentFrameData.totalFrames;

        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        InputFlags dirMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;
        
        InputFlags newlyPressedAttack = stateMachine.GetKeyDownFlags() & attackMask;

        if (newlyPressedAttack != InputFlags.None && bufferedAttackInput == InputFlags.None)
        {
            InputFlags currentDirection = input.flags & dirMask;
            bufferedAttackInput = newlyPressedAttack | currentDirection;
        }

        if (currentFrame >= currentFrameData.cancelWindowStartFrame && bufferedAttackInput != InputFlags.None)
        {
            stateMachine.AddToComboSequence(bufferedAttackInput);
            bool isValidCombo = EvaluateNextComboAttack();
            
            if (isValidCombo)
            {
                stateMachine.ClearCurrentCommand();
                stateMachine.ResetStateFrameCounter();
                bufferedAttackInput = InputFlags.None;
                return;
            }
            else
            {
                stateMachine.ClearComboSequence();
                bufferedAttackInput = InputFlags.None;
            }
        }

        if (currentFrame >= totalFrames)
        {
            stateMachine.ClearComboSequence();
            stateMachine.ClearCurrentCommand();
            stateMachine.TransitionTo(PlayerState.Idle);
        }
    }

    private bool EvaluateNextComboAttack()
    {
        ComboTreeSO tree = stateMachine.GetComboTree();
        if (tree == null) return false;

        List<InputFlags> currentSequence = stateMachine.GetComboSequence();
        ComboNode matchedNode = tree.GetNodeFromSequence(currentSequence);

        if (matchedNode != null)
        {
            stateMachine.SetCurrentComboNode(matchedNode);
            UpdateCurrentFrameData();
            return true;
        }

        return false;
    }
}