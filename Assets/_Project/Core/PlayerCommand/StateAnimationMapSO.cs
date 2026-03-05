using UnityEngine;

[CreateAssetMenu(fileName = "StateAnimationMap", menuName = "ScriptableObjects/StateAnimationMap")]
public class StateAnimationMapSO : ScriptableObject
{
    [Header("Hit & Fall States")]
    public AnimationClip standHit;
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

    public string GetStateAnimationName(PlayerState_Type state)
    {
        AnimationClip clip = null;

        switch (state)
        {
            case PlayerState_Type.StandHit: clip = standHit; break;
            case PlayerState_Type.AirHit: clip = airHit; break;
            case PlayerState_Type.Stunning: clip = stunning; break;
            case PlayerState_Type.GroundSmash: clip = groundSmash; break;
            case PlayerState_Type.WakeUp: clip = wakeUp; break;
        }

        if (clip != null)
        {
            return clip.name;
        }
        
        return state.ToString();
    }

    public string GetLayingDownAnimationName(bool isFromRoll)
    {
        AnimationClip clip = isFromRoll ? layingDownIdle : layingDownInitial;

        if (clip != null)
        {
            return clip.name;
        }

        return isFromRoll ? "LayingDown_Idle" : "LayingDown_Initial";
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

        if (clip != null)
        {
            return clip.name;
        }

        return $"WakeUp_{wakeUpType}";
    }
}