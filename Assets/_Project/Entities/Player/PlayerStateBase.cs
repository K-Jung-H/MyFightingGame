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
        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        if ((stateMachine.GetKeyDownFlags() & attackMask) != InputFlags.None)
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
        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        if ((stateMachine.GetKeyDownFlags() & attackMask) != InputFlags.None)
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
        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        if ((stateMachine.GetKeyDownFlags() & attackMask) != InputFlags.None)
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
        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        if ((stateMachine.GetKeyDownFlags() & attackMask) != InputFlags.None)
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
    private bool isComboEvaluated;
    private ComboNode bufferedNextNode;
    private ActionDataSO currentActionData;

    public AttackingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Attacking;

    public override void Enter()
    {
        isComboEvaluated = false;
        bufferedNextNode = null;

        if (stateMachine.GetCurrentCommand() != null)
        {
            stateMachine.ClearComboSequence();
            
            if (UpdateCurrentFrameData())
            {
                Debug.Log($"[AttackingState] 커맨드 진입 및 초기화 완료: {stateMachine.GetCurrentCommand().actionData.animationStateName}");
            }
            return;
        }

        if (stateMachine.GetComboSequence().Count == 0)
        {
            InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
            InputFlags directionMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;

            InputFlags initialAttack = stateMachine.currentInput.flags & (attackMask | directionMask);

            if ((initialAttack & attackMask) == InputFlags.None)
            {
                initialAttack = InputFlags.LightAttack; 
            }

            ComboTreeSO tree = stateMachine.GetComboTree();
            ComboNode firstNode = tree != null ? tree.FindBestMatchNode(tree.startingAttacks, initialAttack) : null;

            if (firstNode != null)
            {
                stateMachine.AddToComboSequence(firstNode.requiredInput);
                stateMachine.SetCurrentComboNode(firstNode);
                
                if (UpdateCurrentFrameData())
                {
                    Debug.Log($"[AttackingState] 콤보 첫 진입 성공: {firstNode.actionData.animationStateName}");
                }
            }
            else
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

    private bool UpdateCurrentFrameData()
    {
        currentActionData = stateMachine.GetCurrentActionData();
        
        if (currentActionData == null || currentActionData.frameData == null || currentActionData.frameData.logicData.totalFrames <= 0)
        {
            Debug.LogError("[AttackingState] ActionDataSO 또는 FrameData가 누락되어 강제 해제됩니다.");
            stateMachine.ClearComboSequence();
            stateMachine.ClearCurrentCommand();
            stateMachine.TransitionTo(PlayerState_Type.Idle);
            return false;
        }
        return true;
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (stateMachine.GetCurrentState() != PlayerState_Type.Attacking) return;
        
        int currentFrame = stateMachine.GetStateFrameCounter();
        int totalFrames = currentActionData.frameData.logicData.totalFrames;
        int cancelWindowStartFrame = currentActionData.frameData.logicData.cancelWindowStartFrame;

        if (currentFrame == 0) return;
        if (stateMachine.HasBufferedCommand()) return;

        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        InputFlags newlyPressedAttack = stateMachine.GetKeyDownFlags() & attackMask;

        if (stateMachine.GetCurrentCommand() == null)
        {
            if (newlyPressedAttack != InputFlags.None && !isComboEvaluated)
            {
                isComboEvaluated = true;
                
                InputFlags directionMask = InputFlags.Up | InputFlags.Down | InputFlags.Left | InputFlags.Right;
                InputFlags combinedInput = newlyPressedAttack | (input.flags & directionMask);

                ComboTreeSO tree = stateMachine.GetComboTree();
                
                List<InputFlags> testSequence = new List<InputFlags>(stateMachine.GetComboSequence());
                testSequence.Add(combinedInput);
                bufferedNextNode = tree != null ? tree.GetNodeFromSequence(testSequence) : null;

                if (bufferedNextNode == null)
                {
                    bufferedNextNode = tree != null ? tree.FindBestMatchNode(tree.startingAttacks, combinedInput) : null;
                    if (bufferedNextNode != null)
                    {
                        stateMachine.ClearComboSequence();
                    }
                }

                if (bufferedNextNode != null)
                {
                    stateMachine.AddToComboSequence(bufferedNextNode.requiredInput);
                }
            }
        }

        if (currentFrame >= cancelWindowStartFrame && bufferedNextNode != null)
        {
            stateMachine.SetCurrentComboNode(bufferedNextNode);
            stateMachine.ClearCurrentCommand();
            stateMachine.ResetStateFrameCounter();
            
            isComboEvaluated = false;
            bufferedNextNode = null;
            UpdateCurrentFrameData();
            return;
        }

        if (currentFrame >= totalFrames)
        {
            stateMachine.ClearComboSequence();
            stateMachine.ClearCurrentCommand();
            stateMachine.TransitionTo(PlayerState_Type.Idle);
        }
    }
    public int GetCancelWindow()
    {
        if (currentActionData != null && currentActionData.frameData != null)
        {
            return currentActionData.frameData.logicData.cancelWindowStartFrame;
        }
        return 999;
    }
}