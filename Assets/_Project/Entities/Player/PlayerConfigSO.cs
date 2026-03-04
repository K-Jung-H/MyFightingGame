using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "ScriptableObjects/PlayerConfig")]
public class PlayerConfigSO : ScriptableObject
{
    [Header("Movement Speeds")]
    public float walkSpeed = 0.1f;
    public float runSpeed = 0.18f;
    public float sprintSpeed = 0.25f;
    public float turnLerpSpeed = 0.2f;

    [Header("Wake Up Frame Settings")]
    [SerializeField] private int wakeUpInPlaceFrames = 50;
    [SerializeField] private int wakeUpRollForwardFrames = 40;
    [SerializeField] private int wakeUpRollBackwardFrames = 45;
    [SerializeField] private int wakeUpRollLeftFrames = 35;
    [SerializeField] private int wakeUpRollRightFrames = 35;
    [SerializeField] private int wakeUpAttackFrames = 30;

    [Header("Physics Settings")]
    public float gravityScale = 1.0f;
    [SerializeField] private float bounceVelocityThreshold = -10f;
    [SerializeField] private float bounceVelocityMultiplier = 0.5f;
    [SerializeField] private int groundSmashBounceFrames = 10;
    [SerializeField] private int groundSmashLayFrames = 15;

    public float GetBounceVelocityThreshold() => bounceVelocityThreshold;
    public float GetBounceVelocityMultiplier() => bounceVelocityMultiplier;
    public int GetGroundSmashBounceFrames() => groundSmashBounceFrames;
    public int GetGroundSmashLayFrames() => groundSmashLayFrames;

    public List<HurtboxPreset> defaultHurtboxes;

    public CollisionBox[] GetHurtboxBoxes(Hurtbox_Type type)
    {
        bool hasDefaultHurtboxes = defaultHurtboxes != null;
        if (hasDefaultHurtboxes)
        {
            for (int i = 0; i < defaultHurtboxes.Count; i++)
            {
                bool isTypeMatch = defaultHurtboxes[i].type == type;
                if (isTypeMatch)
                {
                    return defaultHurtboxes[i].boxes;
                }
            }
        }
        return null;
    }

    [Header("Input Settings")]
    public int commandBufferWindow = 15;
    public int tapWindowFrames = 15;
    public int autoSprintFrames = 60;

    [Header("State Settings")]
    [SerializeField] private int stunningFrames = 30;
    public int attackFrameLimit = 30;

    public int GetStunningFrames()
    {
        return stunningFrames;
    }
    
    public int GetWakeUpFrames(WakeUp_Type type)
    {
        return type switch
        {
            WakeUp_Type.InPlace => wakeUpInPlaceFrames,
            WakeUp_Type.RollForward => wakeUpRollForwardFrames,
            WakeUp_Type.RollBackward => wakeUpRollBackwardFrames,
            WakeUp_Type.RollLeft => wakeUpRollLeftFrames,
            WakeUp_Type.RollRight => wakeUpRollRightFrames,
            WakeUp_Type.Attack => wakeUpAttackFrames,
            _ => 50,
        };
    }
}