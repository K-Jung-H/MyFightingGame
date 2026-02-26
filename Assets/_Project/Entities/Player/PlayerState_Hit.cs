using UnityEngine;



public class HitState : PlayerStateBase
{
    private HitInfo currentHitInfo;

    public HitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Hit;

    public override void Enter()
    {
        currentHitInfo = stateMachine.GetCurrentHitInfo();
        
        stateMachine.ClearComboSequence();
        stateMachine.ClearCurrentCommand();

        switch (currentHitInfo.hitType)
        {
            case HitState_Type.StandHit:
            case HitState_Type.KnockDown:
            case HitState_Type.GroundHit:
                stateMachine.ApplyPushback(currentHitInfo.pushbackVector);
                break;
            case HitState_Type.AirHit:
                
                break;
            case HitState_Type.GuardHit:
                stateMachine.ApplyPushback(currentHitInfo.pushbackVector * 0.5f);
                break;
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (currentHitInfo.hitType == HitState_Type.AirHit)
        {
            
        }

        if (stateMachine.GetStateFrameCounter() >= currentHitInfo.hitstunFrames)
        {
            if (currentHitInfo.isHardKnockdown || currentHitInfo.hitType == HitState_Type.KnockDown)
            {
                
                stateMachine.TransitionTo(PlayerState_Type.Idle);
            }
            else
            {
                stateMachine.TransitionTo(PlayerState_Type.Idle);
            }
        }
    }
}
