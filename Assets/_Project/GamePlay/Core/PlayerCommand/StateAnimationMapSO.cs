using UnityEngine;

[CreateAssetMenu(fileName = "StateAnimationMap", menuName = "ScriptableObjects/StateAnimationMap")]
public class StateAnimationMapSO : ScriptableObject
{
    [Header("Hit & Block States")]
    public AnimationClip standHitHigh;
    public AnimationClip standHitMid;
    public AnimationClip standHitLow;
    public AnimationClip crouchHit;
    public AnimationClip standBlockHigh;
    public AnimationClip standBlockMid;
    public AnimationClip crouchBlock;
    
    [Header("Fall States")]
    public AnimationClip airHit;
    public AnimationClip stunning;
    public AnimationClip groundSmash;
    public AnimationClip wakeUp;

    [Header("Laying Down States")]
    public AnimationClip layingDownInitial;
    public AnimationClip layingDownIdle;

    [Header("Wake Up Types")]
    public AnimationClip wakeUpInPlace;
    public AnimationClip wakeUpRollForward;
    public AnimationClip wakeUpRollBackward;
    public AnimationClip wakeUpRollLeft;
    public AnimationClip wakeUpRollRight;
    public AnimationClip wakeUpAttack;

    [Header("Match End States")]
    public AnimationClip dead;
    public AnimationClip win;

    public AnimationClip GetHurtAnimationClip(PlayerState_Type state, Attack_Height attackHeight)
    {
        switch (state)
        {
            case PlayerState_Type.StandHit:
                if (attackHeight == Attack_Height.Low) return standHitLow;
                if (attackHeight == Attack_Height.Mid) return standHitMid;
                return standHitHigh;
            case PlayerState_Type.StandBlock:
                return (attackHeight == Attack_Height.Mid) ? standBlockMid : standBlockHigh;
            case PlayerState_Type.CrouchHit:
                return crouchHit;
            case PlayerState_Type.CrouchBlock:
                return crouchBlock;
        }
        return null;
    }

    public AnimationClip GetStateAnimationClip(PlayerState_Type state)
    {
        switch (state)
        {
            case PlayerState_Type.AirHit: return airHit;
            case PlayerState_Type.Stunning: return stunning;
            case PlayerState_Type.GroundSmash: return groundSmash;
            case PlayerState_Type.WakeUp: return wakeUp;
            case PlayerState_Type.Dead: return dead;
            case PlayerState_Type.Win: return win;
        }
        return null;
    }

    public AnimationClip GetLayingDownAnimationClip(bool isFromRoll)
    {
        return isFromRoll ? layingDownIdle : layingDownInitial;
    }

    public AnimationClip GetWakeUpAnimationClip(WakeUp_Type wakeUpType)
    {
        switch (wakeUpType)
        {
            case WakeUp_Type.InPlace: return wakeUpInPlace;
            case WakeUp_Type.RollForward: return wakeUpRollForward;
            case WakeUp_Type.RollBackward: return wakeUpRollBackward;
            case WakeUp_Type.RollLeft: return wakeUpRollLeft;
            case WakeUp_Type.RollRight: return wakeUpRollRight;
            case WakeUp_Type.Attack: return wakeUpAttack;
        }
        return null;
    }
}