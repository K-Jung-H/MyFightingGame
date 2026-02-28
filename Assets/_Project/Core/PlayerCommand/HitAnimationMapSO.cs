using UnityEngine;
using System.Collections.Generic;

public enum HitAnimState_Type
{
    StandHit,
    AirHit,
    Knockdown,
    WakeUp
}

[System.Serializable]
public struct StateAnimationMapping
{
    public HitAnimState_Type stateType;
    public AnimationClip animationClip;
}

[CreateAssetMenu(fileName = "HitAnimationMap", menuName = "ScriptableObjects/HitAnimationMap")]
public class HitAnimationMapSO : ScriptableObject
{
    public List<StateAnimationMapping> mappings;

    public string GetHitAnimationName(PlayerState_Type state)
    {
        bool hasMappings = mappings != null;
        if (hasMappings)
        {
            for (int i = 0; i < mappings.Count; i++)
            {
                bool isStateNameMatch = mappings[i].stateType.ToString() == state.ToString();
                if (isStateNameMatch)
                {
                    bool hasValidClip = mappings[i].animationClip != null;
                    if (hasValidClip)
                    {
                        return mappings[i].animationClip.name;
                    }
                }
            }
        }
        return state.ToString();
    }
}