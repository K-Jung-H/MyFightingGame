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

    public AttackingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState GetStateType() => PlayerState.Attacking;

    public override void Enter()
    {
        bufferedAttackInput = InputFlags.None;

        if (stateMachine.GetComboSequence().Count == 0)
        {
            InputFlags initialAttack = stateMachine.GetPreviousInput().flags & (InputFlags.LightAttack | InputFlags.HeavyAttack);
            stateMachine.AddToComboSequence(initialAttack);
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        InputFlags dirMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;

        if (stateMachine.GetStateFrameCounter() < config.attackFrameLimit)
        {
            if ((input.flags & attackMask) != 0 && bufferedAttackInput == InputFlags.None)
            {
                bufferedAttackInput = input.flags & (attackMask | dirMask);
            }
        }

        if (stateMachine.GetStateFrameCounter() >= config.attackFrameLimit)
        {
            if (bufferedAttackInput != InputFlags.None)
            {
                stateMachine.AddToComboSequence(bufferedAttackInput);
                bool isValidCombo = EvaluateNextComboAttack();
                
                if (isValidCombo)
                {
                    stateMachine.ResetStateFrameCounter();
                    bufferedAttackInput = InputFlags.None;
                }
                else
                {
                    stateMachine.ClearComboSequence();
                    stateMachine.TransitionTo(PlayerState.Idle);
                }
            }
            else
            {
                stateMachine.ClearComboSequence();
                stateMachine.TransitionTo(PlayerState.Idle);
            }
        }
    }

    private bool EvaluateNextComboAttack()
    {
        List<InputFlags> currentCombo = stateMachine.GetComboSequence();
        
        return true; 
    }
}