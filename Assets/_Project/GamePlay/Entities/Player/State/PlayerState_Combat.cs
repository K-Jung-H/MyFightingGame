using UnityEngine;

public class AttackingState : PlayerStateBase
{
    private ActionDataSO currentActionData;

    public AttackingState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Attacking;

    public override void Enter()
    {
        currentActionData = stateMachine.GetCurrentActionData();
    }

    public override void UpdateTick(PlayerInput input)
    {
        bool isAttackingStateValid = stateMachine.GetCurrentState() == PlayerState_Type.Attacking;
        if (!isAttackingStateValid) return;

        int currentFrame = stateMachine.GetStateFrameCounter();
        int totalFrames = currentActionData.frameData.logicData.totalFrames;

        bool isRootMotionUsed = currentActionData.frameData.logicData.useRootMotion;
        bool hasRootMotionData = currentActionData.frameData.rootMotionPath != null;

        if (isRootMotionUsed && hasRootMotionData)
        {
            bool isFrameWithinBounds = currentFrame < currentActionData.frameData.rootMotionPath.Length;
            if (isFrameWithinBounds)
            {
                RootMotionData rootData = currentActionData.frameData.rootMotionPath[currentFrame];
                physics.ApplyRootMotion(rootData.deltaPosition, rootData.deltaRotation);
            }
        }

        bool isActionCompleted = currentFrame >= totalFrames;
        if (isActionCompleted)
        {
            actionController.ClearComboSequence();
            stateMachine.ClearCurrentAction();
            stateMachine.TransitionTo(PlayerState_Type.Idle);
        }
    }

    public override int GetCancelWindow()
    {
        bool hasValidFrameData = currentActionData != null && currentActionData.frameData != null;
        if (hasValidFrameData)
        {
            return currentActionData.frameData.logicData.cancelWindowStartFrame;
        }
        return 999;
    }
}