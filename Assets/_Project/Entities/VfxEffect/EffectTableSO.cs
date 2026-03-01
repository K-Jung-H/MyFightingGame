using UnityEngine;
using System;


[Serializable]
public struct EffectMapping
{
    [HideInInspector] public string name;
    [HideInInspector] public EffectType effectType;
    public VfxClipSO vfxClip;
}

[CreateAssetMenu(fileName = "NewEffectTable", menuName = "VFX/Effect Table")]
public class EffectTableSO : ScriptableObject
{
    public EffectMapping[] mappings;

    public VfxClipSO GetClip(EffectType type)
    {
        if (type == EffectType.None) return null;

        foreach (var mapping in mappings)
        {
            if (mapping.effectType == type)
            {
                if (mapping.vfxClip == null)
                {
                    Debug.LogError($"[EffectTable] {name} 테이블에 {type} 이펙트가 할당되지 않았습니다!");
                    return null;
                }
                return mapping.vfxClip;
            }
        }
        return null;
    }

    private void OnValidate()
    {
        Array enumValues = Enum.GetValues(typeof(EffectType));
        int enumCount = enumValues.Length;

        if (mappings == null || mappings.Length != enumCount)
        {
            EffectMapping[] newMappings = new EffectMapping[enumCount];
            
            for (int i = 0; i < enumCount; i++)
            {
                EffectType currentType = (EffectType)enumValues.GetValue(i);
                newMappings[i] = new EffectMapping
                {
                    name = currentType.ToString(),
                    effectType = currentType,
                    vfxClip = GetExistingClip(currentType)
                };
            }
            mappings = newMappings;
        }
    }

    private VfxClipSO GetExistingClip(EffectType type)
    {
        if (mappings == null) return null;
        
        foreach (var mapping in mappings)
        {
            if (mapping.effectType == type)
            {
                return mapping.vfxClip;
            }
        }
        return null;
    }
}