using UnityEngine;

public abstract class HurtStateBase : PlayerStateBase
{
    protected HurtInfo currentHurtInfo;
    protected int currentStunFrames;

    public HurtStateBase(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public int GetCurrentStunFrames() => currentStunFrames;
    public void SetCurrentStunFrames(int frames) => currentStunFrames = frames;

    public override void Enter()
    {
        currentHurtInfo = combat.GetCurrentHurtInfo();
        currentStunFrames = currentHurtInfo.hurtStunFrames;
        stateMachine.ClearCurrentAction();
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick())
        {
            return;
        }

        currentStunFrames--;

        if (currentStunFrames <= 0)
        {
            bool isLethal = combat.GetCurrentHealth() <= 0;
            if (isLethal)
            {
                stateMachine.TransitionTo(PlayerState_Type.Dead, true);
                return;
            }

            stateMachine.TransitionTo(GetRecoveryState(), true);
        }
    }

    protected abstract PlayerState_Type GetRecoveryState();
}

public class StandHitState : HurtStateBase
{
    public StandHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.StandHit;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Idle;
}

public class CrouchHitState : HurtStateBase
{
    public CrouchHitState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.CrouchHit;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Crouching;
}

public class StandBlockState : HurtStateBase
{
    public StandBlockState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.StandBlock;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Idle;
}

public class CrouchBlockState : HurtStateBase
{
    public CrouchBlockState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.CrouchBlock;

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Crouching;
}

public class StunningState : HurtStateBase
{
    public StunningState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Stunning;

    public override void Enter()
    {
        base.Enter();

        currentStunFrames = config.GetStunningFrames();

        bool isFromStand = physics.GetFPPosition().y.rawValue <= 0;
        if (isFromStand)
        {
            FPVector3 horizontalPushback = currentHurtInfo.pushbackVector;
            horizontalPushback.y = new FP64(0);
            physics.ApplyFPPushback(horizontalPushback);
        }
    }

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.LayingDown;
}

public class Knockback_AirState : HurtStateBase
{
    public Knockback_AirState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Knockback_Air;

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick()) return;

        bool isFallingAndGrounded = physics.GetIsGrounded() && physics.GetFPVelocity().y.rawValue <= 0;

        if (isFallingAndGrounded)
        {
            bool isLethal = combat.GetCurrentHealth() <= 0;
            if (isLethal)
            {
                stateMachine.TransitionTo(PlayerState_Type.Dead, true);
            }
            else
            {
                stateMachine.TransitionTo(PlayerState_Type.GroundSmash);
            }
        }
    }

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.None;
}

public class WallBounceState : HurtStateBase
{
    private const int MIN_WALL_BOUNCE_FRAMES = 15;

    public WallBounceState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.WallBounce;

    public override void Enter()
    {
        base.Enter();

        if (currentStunFrames < MIN_WALL_BOUNCE_FRAMES)
        {
            currentStunFrames = MIN_WALL_BOUNCE_FRAMES;
        }
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick()) return;

        bool isFallingAndGrounded = physics.GetIsGrounded() && physics.GetFPVelocity().y.rawValue <= 0;
        if (isFallingAndGrounded)
        {
            bool isLethal = combat.GetCurrentHealth() <= 0;
            if (isLethal)
            {
                stateMachine.TransitionTo(PlayerState_Type.Dead, true);
            }
            else
            {
                stateMachine.TransitionTo(PlayerState_Type.GroundSmash);
            }
            return;
        }

        currentStunFrames--;

        if (currentStunFrames <= 0)
        {
            stateMachine.TransitionTo(GetRecoveryState(), true);
        }
    }

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.Knockback_Air;
}

public class GroundSmashState : HurtStateBase
{
    private bool isBouncing;

    public GroundSmashState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.GroundSmash;

    public bool GetIsBouncing() => isBouncing;
    public void SetIsBouncing(bool bounce) => isBouncing = bounce;

    public override void Enter()
    {
        base.Enter();

        FP64 impactFallSpeed = physics.GetFPLastImpactFallSpeed();
        
        isBouncing = impactFallSpeed.rawValue <= cachedBounceThreshold.rawValue;

        if (isBouncing)
        {
            currentStunFrames = config.GetGroundSmashBounceFrames();
            FPVector3 currentVelocity = physics.GetFPVelocity();
            
            FP64 absImpactSpeed = FP64.Abs(impactFallSpeed);
            
            currentVelocity.y = absImpactSpeed * cachedBounceMultiplier;
            physics.SetFPVelocity(currentVelocity);
        }
        else
        {
            currentStunFrames = config.GetGroundSmashLayFrames();
            FPVector3 currentVelocity = physics.GetFPVelocity();
            currentVelocity.x = new FP64(0);
            currentVelocity.z = new FP64(0);
            physics.SetFPVelocity(currentVelocity);
        }
    }

    protected override PlayerState_Type GetRecoveryState() 
    {
        if (isBouncing) return PlayerState_Type.Knockback_Air;
        
        bool isLethal = combat.GetCurrentHealth() <= 0;
        return isLethal ? PlayerState_Type.Dead : PlayerState_Type.LayingDown;
    }
}

public class LayingDownState : HurtStateBase
{
    private bool isFromRoll;

    public LayingDownState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.LayingDown;

    public override void Enter()
    {
        base.Enter();
        FPVector3 currentVelocity = physics.GetFPVelocity();
        currentVelocity.x = new FP64(0);
        currentVelocity.z = new FP64(0);
        physics.SetFPVelocity(currentVelocity);
    }

    public override void Exit()
    {
        base.Exit();
        isFromRoll = false;
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick()) return;

        bool isLethal = combat.GetCurrentHealth() <= 0;
        if (isLethal)
        {
            stateMachine.TransitionTo(PlayerState_Type.Dead, true);
            return;
        }

        bool isDirectionalInputDetected = (input.flags & (InputFlags.Forward | InputFlags.Back | InputFlags.Up | InputFlags.Down)) != 0;

        if (isDirectionalInputDetected)
        {
            actionController.ClearAllBuffers();

            WakeUp_Type selectedType = EvaluateWakeUpInput(input);
            WakeUpState wakeUpState = stateMachine.GetStateObject(PlayerState_Type.WakeUp) as WakeUpState;

            bool isWakeUpStateValid = wakeUpState != null;
            if (isWakeUpStateValid)
            {
                wakeUpState.SetWakeUpType(selectedType);
            }

            stateMachine.TransitionTo(PlayerState_Type.WakeUp);
        }
    }

    public void SetFromRoll(bool value)
    {
        isFromRoll = value;
    }

    public bool IsFromRoll()
    {
        return isFromRoll;
    }

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.None;

    private WakeUp_Type EvaluateWakeUpInput(PlayerInput input)
    {
        bool isLeftPressed = (input.flags & InputFlags.Up) != 0;
        if (isLeftPressed) return WakeUp_Type.RollLeft;

        bool isRightPressed = (input.flags & InputFlags.Down) != 0;
        if (isRightPressed) return WakeUp_Type.RollRight;

        bool isForwardPressed = (input.flags & InputFlags.Forward) != 0;
        if (isForwardPressed) return WakeUp_Type.RollForward;

        bool isBackwardPressed = (input.flags & InputFlags.Back) != 0;
        if (isBackwardPressed) return WakeUp_Type.RollBackward;

        return WakeUp_Type.InPlace;
    }
}

public class WakeUpState : HurtStateBase
{
    private WakeUp_Type scheduledWakeUpType;

    public WakeUpState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.WakeUp;

    public override void Enter()
    {
        base.Enter();
        currentStunFrames = config.GetWakeUpFrames(scheduledWakeUpType);

        FPVector3 currentVelocity = physics.GetFPVelocity();
        currentVelocity.x = new FP64(0);
        currentVelocity.z = new FP64(0);
        physics.SetFPVelocity(currentVelocity);
    }

    public override void UpdateTick(PlayerInput input)
    {
        if (combat.ProcessHitstopTick()) return;

        currentStunFrames--;

        if (currentStunFrames <= 0)
        {
            bool isLethal = combat.GetCurrentHealth() <= 0;
            if (isLethal)
            {
                stateMachine.TransitionTo(PlayerState_Type.Dead, true);
                return;
            }

            bool isGroundRoll = scheduledWakeUpType == WakeUp_Type.RollLeft || scheduledWakeUpType == WakeUp_Type.RollRight;

            if (isGroundRoll)
            {
                LayingDownState layState = stateMachine.GetStateObject(PlayerState_Type.LayingDown) as LayingDownState;
                bool isLayStateValid = layState != null;
                if (isLayStateValid)
                {
                    layState.SetFromRoll(true);
                }

                stateMachine.TransitionTo(PlayerState_Type.LayingDown);
            }
            else
            {
                stateMachine.TransitionTo(PlayerState_Type.Idle);
            }
        }
    }

    public void SetWakeUpType(WakeUp_Type type)
    {
        scheduledWakeUpType = type;
    }

    public WakeUp_Type GetScheduledWakeUpType()
    {
        return scheduledWakeUpType;
    }

    protected override PlayerState_Type GetRecoveryState() => PlayerState_Type.None;
}