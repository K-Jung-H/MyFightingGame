using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Character/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    public GameObject characterPrefab;
    public PlayerConfigSO config;
    public CommandListSO commandList;
    public ComboTreeSO comboTree;
    public StateAnimationMapSO hitAnimMap;
    public EffectTableSO effectTable;
}