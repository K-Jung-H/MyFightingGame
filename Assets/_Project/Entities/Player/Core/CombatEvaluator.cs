using UnityEngine;

public struct EvaluationResult
{
    public bool isEvaded;
    public PlayerState_Type targetState;
    public HurtInfo hurtInfo;
    public HitFeedbackData feedbackData;
}

public class CombatEvaluator
{
    public EvaluationResult EvaluateHit(HitboxEvent hitEvent, PlayerState_Type defenderState, PlayerConfigSO config, bool isMoving)
    {
        EvaluationResult result = new EvaluationResult();
        
        PlayerState_Type? determinedState = DetermineTargetState(hitEvent, defenderState, isMoving);
        result.isEvaded = determinedState == null;
        
        if (result.isEvaded)
        {
            return result;
        }

        bool isBlocked = determinedState.Value == PlayerState_Type.StandBlock || determinedState.Value == PlayerState_Type.CrouchBlock;

        if (!isBlocked)
        {
            if (hitEvent.targetHurtState == HurtState_Type.AirHit)
            {
                result.targetState = PlayerState_Type.AirHit;
            }
            else if (hitEvent.targetHurtState == HurtState_Type.StunHit)
            {
                result.targetState = PlayerState_Type.Stunning;
            }
            else
            {
                result.targetState = determinedState.Value;
            }
        }
        else
        {
            result.targetState = determinedState.Value;
        }

        int actualHitStun = hitEvent.hitstunFrames > 0 ? hitEvent.hitstunFrames : config.GetDefaultHitStunFrames();
        int actualBlockStun = hitEvent.blockStunFrames > 0 ? hitEvent.blockStunFrames : config.GetDefaultBlockStunFrames();

        result.hurtInfo = new HurtInfo
        {
            damage = isBlocked ? 0 : hitEvent.damage,
            hurtStunFrames = isBlocked ? actualBlockStun : actualHitStun,
            pushbackVector = isBlocked ? Vector3.zero : hitEvent.localPushbackVector,
            targetHurtState = isBlocked ? HurtState_Type.Hit : hitEvent.targetHurtState,
            isHardKnockdown = isBlocked ? false : hitEvent.isHardKnockdown,
            attackHeight = hitEvent.attackHeight
        };

        result.feedbackData = new HitFeedbackData
        {
            attackType = hitEvent.attackType,
            hitstopFrames = CalculateHitstop(hitEvent.attackType, isBlocked),
            cameraShakeIntensity = CalculateCameraShake(hitEvent.attackType, isBlocked)
        };

        return result;
    }

    private PlayerState_Type? DetermineTargetState(HitboxEvent hitEvent, PlayerState_Type defenderState, bool isMoving)
    {
        bool isStanding = defenderState == PlayerState_Type.Idle ||
                          defenderState == PlayerState_Type.Walking ||
                          defenderState == PlayerState_Type.SideWalk ||
                          defenderState == PlayerState_Type.Running ||
                          defenderState == PlayerState_Type.Sprinting ||
                          defenderState == PlayerState_Type.SideStep ||
                          defenderState == PlayerState_Type.StandBlock ||
                          defenderState == PlayerState_Type.StandHit;

        bool isCrouching = defenderState == PlayerState_Type.Crouching ||
                           defenderState == PlayerState_Type.CrouchBlock ||
                           defenderState == PlayerState_Type.CrouchHit;

        bool canBlock = defenderState == PlayerState_Type.Idle || 
                        (defenderState == PlayerState_Type.Crouching && !isMoving) || 
                        defenderState == PlayerState_Type.StandBlock || 
                        defenderState == PlayerState_Type.CrouchBlock;

        if (isStanding)
        {
            if (hitEvent.attackHeight == Attack_Height.Low) return PlayerState_Type.StandHit;
            return canBlock ? PlayerState_Type.StandBlock : PlayerState_Type.StandHit;
        }

        if (isCrouching)
        {
            if (hitEvent.attackHeight == Attack_Height.High) return null;
            if (hitEvent.attackHeight == Attack_Height.Low) return canBlock ? PlayerState_Type.CrouchBlock : PlayerState_Type.CrouchHit;
            if (hitEvent.attackHeight == Attack_Height.Mid) return PlayerState_Type.CrouchHit;
        }

        return PlayerState_Type.StandHit;
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