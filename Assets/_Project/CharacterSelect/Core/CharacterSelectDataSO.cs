using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterSelectData", menuName = "Character/Character Select Data")]
public class CharacterSelectDataSO : ScriptableObject
{
    public string characterId;
    public string characterName;
    public Sprite portraitSprite;
    public Sprite fullBodySprite;
    public GameObject modelPrefab;
    public CharacterDataSO inGameData;
}