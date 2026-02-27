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

    public List<HurtboxPreset> defaultHurtboxes;

    public CollisionBox[] GetHurtboxBoxes(Hurtbox_Type type)
    {
        if (defaultHurtboxes != null)
        {
            for (int i = 0; i < defaultHurtboxes.Count; i++)
            {
                if (defaultHurtboxes[i].type == type)
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
    public int attackFrameLimit = 30;
}