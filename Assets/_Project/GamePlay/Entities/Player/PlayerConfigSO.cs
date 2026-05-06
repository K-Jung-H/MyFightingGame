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
    public float crouchWalkSpeed = 0.06f;

    [Header("Side Movement Settings")]
    public float sideStepSpeed = 0.3f;
    public float sideWalkSpeed = 0.12f;
    public int sideStepFrames = 15;

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

    [Header("Hurtbox Settings")]
    public List<HurtboxPreset> defaultHurtboxes;

    [Header("Input Settings")]
    public int commandBufferWindow = 15;
    public int tapWindowFrames = 15;

    [Header("State Settings")]
    [SerializeField] private int autoSprintFrames = 120;
    [SerializeField] private int stunningFrames = 30;

    [Header("Default Combat Settings")]
    [SerializeField] private int defaultHitStunFrames = 15;
    [SerializeField] private int defaultBlockStunFrames = 10;

    [Header("Hitstop Settings")]
    [SerializeField] private int hitstopLightHit = 3;
    [SerializeField] private int hitstopHeavyHit = 8;
    [SerializeField] private int hitstopCrash = 15;
    [SerializeField] private int hitstopDefault = 5;

    [Header("Status Settings")]
    [SerializeField] private int maxHealth = 1000;

    [System.NonSerialized]
    private Dictionary<Hurtbox_Type, FPCollisionBox[]> cachedFPHurtboxes;

    public float GetBounceVelocityThreshold() => bounceVelocityThreshold;
    public float GetBounceVelocityMultiplier() => bounceVelocityMultiplier;
    public int GetGroundSmashBounceFrames() => groundSmashBounceFrames;
    public int GetGroundSmashLayFrames() => groundSmashLayFrames;
    public int GetDefaultHitStunFrames() => defaultHitStunFrames;
    public int GetDefaultBlockStunFrames() => defaultBlockStunFrames;
    public int GetAutoSprintFrames() => autoSprintFrames;
    public int GetStunningFrames() => stunningFrames;

    public FPCollisionBox[] GetHurtboxBoxes(Hurtbox_Type type)
    {
        if (defaultHurtboxes == null)
        {
            return null;
        }

        foreach (var preset in defaultHurtboxes)
        {
            if (preset.type == type)
            {
                return preset.boxes;
            }
        }
        return null;
    }

    public FPCollisionBox[] GetFPHurtboxBoxes(Hurtbox_Type type)
    {
        if (cachedFPHurtboxes == null)
        {
            cachedFPHurtboxes = new Dictionary<Hurtbox_Type, FPCollisionBox[]>();
            if (defaultHurtboxes != null)
            {
                foreach (var preset in defaultHurtboxes)
                {
                    cachedFPHurtboxes[preset.type] = preset.boxes; 
                }
            }
        }

        if (cachedFPHurtboxes.TryGetValue(type, out FPCollisionBox[] result))
        {
            return result;
        }
        
        return null;
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

    public int GetHitstopFrames(Attack_Type type)
    {
        return type switch
        {
            Attack_Type.LightHit => hitstopLightHit,
            Attack_Type.HeavyHit => hitstopHeavyHit,
            Attack_Type.Crash => hitstopCrash,
            _ => hitstopDefault,
        };
    }

    public int GetMaxHealth() => maxHealth;
}