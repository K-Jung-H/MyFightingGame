using UnityEngine;

[CreateAssetMenu(fileName = "NewStageData", menuName = "ScriptableObjects/StageData")]
public class GameStageDataSO : ScriptableObject
{
    [Header("General Info")]
    public string stageName;
    public Sprite thumbnail; 

    [Header("Simulation Data (Physics)")]
    public StageBoundary boundary;

    [Header("Visual Data (View)")]
    public GameObject visualPrefab; 
}