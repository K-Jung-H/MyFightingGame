public struct EvaluationResult
{
    public bool isEvaded;
    public PlayerState_Type targetState;
    public HurtInfo hurtInfo;
    public HitFeedbackData feedbackData;
}

public class CombatEvaluator
{
    public EvaluationResult EvaluateHit(HitboxEvent hitEvent, PlayerState_Type defenderState)
    {
        EvaluationResult result = new EvaluationResult();
        
        PlayerState_Type? determinedState = DetermineTargetState(hitEvent, defenderState);
        result.isEvaded = determinedState == null;
        
        if (result.isEvaded)
        {
            return result;
        }

        result.targetState = determinedState.Value;
        bool isBlocked = result.targetState == PlayerState_Type.StandBlock || result.targetState == PlayerState_Type.CrouchBlock;

        result.hurtInfo = new HurtInfo
        {
            damage = isBlocked ? 0 : hitEvent.damage,
            hurtStunFrames = isBlocked ? hitEvent.blockStunFrames : hitEvent.hitstunFrames,
            pushbackVector = hitEvent.localPushbackVector,
            targetHurtState = MapToHurtState(result.targetState),
            isHardKnockdown = isBlocked ? false : hitEvent.isHardKnockdown
        };

        result.feedbackData = new HitFeedbackData
        {
            attackType = hitEvent.attackType,
            hitstopFrames = CalculateHitstop(hitEvent.attackType, isBlocked),
            cameraShakeIntensity = CalculateCameraShake(hitEvent.attackType, isBlocked)
        };

        return result;
    }

    private PlayerState_Type? DetermineTargetState(HitboxEvent hitEvent, PlayerState_Type defenderState)
    {
        bool isStanding = defenderState == PlayerState_Type.Idle ||
                          defenderState == PlayerState_Type.Walking ||
                          defenderState == PlayerState_Type.SideWalk ||
                          defenderState == PlayerState_Type.Running ||
                          defenderState == PlayerState_Type.Sprinting;

        bool isCrouching = defenderState == PlayerState_Type.Crouching;

        if (isStanding)
        {
            if (hitEvent.attackHeight == Attack_Height.Low) return PlayerState_Type.StandHit;
            return PlayerState_Type.StandBlock;
        }

        if (isCrouching)
        {
            if (hitEvent.attackHeight == Attack_Height.High) return null;
            if (hitEvent.attackHeight == Attack_Height.Low) return PlayerState_Type.CrouchBlock;
            if (hitEvent.attackHeight == Attack_Height.Mid) return PlayerState_Type.CrouchHit;
        }

        return PlayerState_Type.StandHit;
    }

    private HurtState_Type MapToHurtState(PlayerState_Type state)
    {
        if (state == PlayerState_Type.StandBlock || state == PlayerState_Type.CrouchBlock) return HurtState_Type.GuardHit;
        if (state == PlayerState_Type.CrouchHit) return HurtState_Type.GroundHit;
        return HurtState_Type.StandHit;
    }

    private int CalculateHitstop(Attack_Type type, bool isBlocked)
    {
        int baseHitstop = type switch
        {
            Attack_Type.LightHit => 3,
            Attack_Type.HeavyHit => 8,
            Attack_Type.Crash => 15,
            _ => 5
        };
        
        return isBlocked ? baseHitstop / 2 : baseHitstop;
    }

    private float CalculateCameraShake(Attack_Type type, bool isBlocked)
    {
        if (isBlocked) return 0f;
        
        return type switch
        {
            Attack_Type.HeavyHit => 2.0f,
            Attack_Type.Crash => 5.0f,
            _ => 0f
        };
    }
}