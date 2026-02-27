using UnityEngine;



public class HitState : PlayerStateBase
{
    private HurtInfo currentHurtInfo;

    public HitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Hit;

    public override void Enter()
    {
        currentHurtInfo = stateMachine.GetCurrentHurtInfo();
        
        stateMachine.ClearComboSequence();
        stateMachine.ClearCurrentCommand();

        switch (currentHurtInfo.targetHurtState)
        {
            case HurtState_Type.StandHit:
            case HurtState_Type.KnockDown:
            case HurtState_Type.GroundHit:
                stateMachine.ApplyPushback(currentHurtInfo.pushbackVector);
                break;
            case HurtState_Type.AirHit:
                
                break;
            case HurtState_Type.GuardHit:
                stateMachine.ApplyPushback(currentHurtInfo.pushbackVector * 0.5f);
                break;
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (currentHurtInfo.targetHurtState == HurtState_Type.AirHit)
        {
            
        }

        if (stateMachine.GetStateFrameCounter() >= currentHurtInfo.hurtStunFrames)
        {
            if (currentHurtInfo.isHardKnockdown || currentHurtInfo.targetHurtState == HurtState_Type.KnockDown)
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
