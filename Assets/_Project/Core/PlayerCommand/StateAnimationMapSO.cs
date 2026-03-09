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

    public string GetHurtAnimationName(PlayerState_Type state, Attack_Height attackHeight)
    {
        AnimationClip clip = null;

        switch (state)
        {
            case PlayerState_Type.StandHit:
                if (attackHeight == Attack_Height.Low) clip = standHitLow;
                else if (attackHeight == Attack_Height.Mid) clip = standHitMid;
                else clip = standHitHigh;
                break;
            case PlayerState_Type.StandBlock:
                clip = (attackHeight == Attack_Height.Mid) ? standBlockMid : standBlockHigh;
                break;
            case PlayerState_Type.CrouchHit:
                clip = crouchHit;
                break;
            case PlayerState_Type.CrouchBlock:
                clip = crouchBlock;
                break;
        }

        return clip != null ? clip.name : state.ToString();
    }

    public string GetStateAnimationName(PlayerState_Type state)
    {
        AnimationClip clip = null;

        switch (state)
        {
            case PlayerState_Type.AirHit: clip = airHit; break;
            case PlayerState_Type.Stunning: clip = stunning; break;
            case PlayerState_Type.GroundSmash: clip = groundSmash; break;
            case PlayerState_Type.WakeUp: clip = wakeUp; break;
        }

        return clip != null ? clip.name : state.ToString();
    }

    public string GetLayingDownAnimationName(bool isFromRoll)
    {
        AnimationClip clip = isFromRoll ? layingDownIdle : layingDownInitial;
        return clip != null ? clip.name : (isFromRoll ? "LayingDown_Idle" : "LayingDown_Initial");
    }

    public string GetWakeUpAnimationName(WakeUp_Type wakeUpType)
    {
        AnimationClip clip = null;

        switch (wakeUpType)
        {
            case WakeUp_Type.InPlace: clip = wakeUpInPlace; break;
            case WakeUp_Type.RollForward: clip = wakeUpRollForward; break;
            case WakeUp_Type.RollBackward: clip = wakeUpRollBackward; break;
            case WakeUp_Type.RollLeft: clip = wakeUpRollLeft; break;
            case WakeUp_Type.RollRight: clip = wakeUpRollRight; break;
            case WakeUp_Type.Attack: clip = wakeUpAttack; break;
        }

        return clip != null ? clip.name : $"WakeUp_{wakeUpType}";
    }
}