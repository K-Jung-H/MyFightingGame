using UnityEngine;

public class RoundReferee
{
    private GameRuleConfigSO config;

    public void Initialize(GameRuleConfigSO ruleConfig)
    {
        config = ruleConfig;
    }

    public bool CheckClimaxCondition(PlayerController p1, PlayerController p2)
    {
        int p1Health = p1.GetCombat().GetCurrentHealth();
        int p2Health = p2.GetCombat().GetCurrentHealth();
        
        int p1MaxHealth = p1.GetConfig().GetMaxHealth();
        int p2MaxHealth = p2.GetConfig().GetMaxHealth();

        bool isP1Critical = p1Health <= (p1MaxHealth * config.climaxHealthRatio);
        bool isP2Critical = p2Health <= (p2MaxHealth * config.climaxHealthRatio);

        if (!isP1Critical && !isP2Critical) return false;

        bool p1Attacking = IsValidAttackAttempt(p1);
        bool p2Attacking = IsValidAttackAttempt(p2);

        if (!p1Attacking || !p2Attacking) return false;

        FPVector3 diff = p1.GetFPPosition() - p2.GetFPPosition();
        diff.y = new FP64(0);
        FP64 distanceSqr = (diff.x * diff.x) + (diff.z * diff.z);

        FP64 climaxDist = FP64.FromFloat(config.climaxActivationDistance);
        FP64 climaxDistSqr = climaxDist * climaxDist;

        if (distanceSqr.rawValue > climaxDistSqr.rawValue) return false;

        return true;
    }

    public bool IsRoundOver(PlayerController p1, PlayerController p2, int timeFrames)
    {
        if (p1 == null || p2 == null) return false;

        int p1Hp = p1.GetCombat().GetCurrentHealth();
        int p2Hp = p2.GetCombat().GetCurrentHealth();

        if (p1Hp > 0 && p2Hp > 0 && timeFrames > 0)
        {
            return false;
        }

        return true;
    }

    public int DetermineWinnerSlot(PlayerController p1, PlayerController p2, int timeFrames)
    {
        if (p1 == null || p2 == null) return -1;

        int p1Hp = p1.GetCombat().GetCurrentHealth();
        int p2Hp = p2.GetCombat().GetCurrentHealth();

        if (p1Hp <= 0 && p2Hp <= 0)
        {
            return -1;
        }
        else if (p1Hp <= 0)
        {
            return 1;
        }
        else if (p2Hp <= 0)
        {
            return 0;
        }
        else if (timeFrames <= 0)
        {
            if (p1Hp > p2Hp) return 0;
            if (p2Hp > p1Hp) return 1;
        }

        return -1;
    }

    private bool IsValidAttackAttempt(PlayerController player)
    {
        ActionDataSO actionData = player.GetStateMachine().GetCurrentActionData();
        bool isAttacking = player.GetStateMachine().GetCurrentState() == PlayerState_Type.Attacking;
        bool hasValidData = actionData != null && actionData.frameData.hitboxEvents != null;

        return isAttacking && hasValidData;
    }
}