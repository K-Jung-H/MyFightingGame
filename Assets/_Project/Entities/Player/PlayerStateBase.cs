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

    public abstract PlayerState_Type GetStateType();
    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void UpdateTick(PlayerInput input);
}

public class IdleState : PlayerStateBase
{
    public IdleState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Idle;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState_Type.Attacking);
            return;
        }

        if (stateMachine.GetRawInputVector(input.flags) != Vector3.zero)
        {
            stateMachine.TransitionTo(PlayerState_Type.Walking);
        }
    }
}

public class StunState : PlayerStateBase
{
    public StunState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Stun;

    public override void UpdateTick(PlayerInput input) { }
}

public class WalkingState : PlayerStateBase
{
    public WalkingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Walking;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState_Type.Attacking);
            return;
        }

        stateMachine.ProcessMovementLogic(input);
        
    }
}

public class RunningState : PlayerStateBase
{
    public RunningState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Running;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState_Type.Attacking);
            return;
        }

        stateMachine.ProcessMovementLogic(input);

        bool isForward = (input.flags & InputFlags.Up) != 0;
        
        if (isForward)
        {
            stateMachine.IncrementRunningForwardFrames();
            if (stateMachine.GetRunningForwardFrames() >= config.autoSprintFrames)
            {
                stateMachine.TransitionTo(PlayerState_Type.Sprinting);
            }
        }
    }
}

public class SprintingState : PlayerStateBase
{
    public SprintingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Sprinting;

    public override void UpdateTick(PlayerInput input)
    {
        if ((input.flags & (InputFlags.LightAttack | InputFlags.HeavyAttack)) != 0)
        {
            stateMachine.TransitionTo(PlayerState_Type.Attacking);
            return;
        }

        stateMachine.ProcessMovementLogic(input);

        bool isForward = (input.flags & InputFlags.Up) != 0;
        
        if (!isForward)
        {
            stateMachine.TransitionTo(PlayerState_Type.Running);
        }
    }
}

public class AttackingState : PlayerStateBase
{
    private InputFlags bufferedAttackInput;
    private AnimationFrameData currentFrameData;

    public AttackingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Attacking;
    
    public int GetCancelWindow() => currentFrameData.logicData.cancelWindowStartFrame;

    public override void Enter()
    {
        bufferedAttackInput = InputFlags.None;

        if (stateMachine.GetCurrentCommand() != null)
        {
            UpdateCurrentFrameData();
            return;
        }

        if (stateMachine.GetComboSequence().Count == 0)
        {
            InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
            InputFlags dirMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;
            
            InputFlags initialAttack = stateMachine.currentInput.flags & (attackMask | dirMask);
            stateMachine.AddToComboSequence(initialAttack);
            
            bool isComboValid = EvaluateNextComboAttack();
            if (!isComboValid)
            {
                stateMachine.ClearComboSequence();
                stateMachine.TransitionTo(PlayerState_Type.Idle);
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
        if (currentCommand != null && currentCommand.actionData != null)
        {
            currentFrameData = currentCommand.actionData.frameData;
            return;
        }

        ComboNode currentNode = stateMachine.GetCurrentComboNode();
        if (currentNode != null && currentNode.actionData != null)
        {
            currentFrameData = currentNode.actionData.frameData;
        }
        else
        {
            currentFrameData = new AnimationFrameData();
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        int currentFrame = stateMachine.GetStateFrameCounter();
        int totalFrames = currentFrameData.logicData.totalFrames;

        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        InputFlags dirMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;
        
        InputFlags newlyPressedAttack = stateMachine.GetKeyDownFlags() & attackMask;

        if (newlyPressedAttack != InputFlags.None && bufferedAttackInput == InputFlags.None)
        {
            InputFlags currentDirection = input.flags & dirMask;
            bufferedAttackInput = newlyPressedAttack | currentDirection;
        }

        if (currentFrame >= currentFrameData.logicData.cancelWindowStartFrame && bufferedAttackInput != InputFlags.None)
        {
            stateMachine.AddToComboSequence(bufferedAttackInput);
            bool isComboValid = EvaluateNextComboAttack();
            
            if (isComboValid)
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
            stateMachine.TransitionTo(PlayerState_Type.Idle);
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