using UnityEngine;

[System.Serializable]
public struct CameraBoundsData
{
    public float minX;
    public float maxX;
    public float minZ;
    public float maxZ;
    
    public int unlockWallIndex; 
}

[CreateAssetMenu(fileName = "NewStageData", menuName = "ScriptableObjects/StageData")]
public class GameStageDataSO : ScriptableObject
{
    [Header("General Info")]
    public string stageName;
    public Sprite thumbnail; 

    [Header("Simulation Data (Physics)")]
    public StageBoundary boundary;
    public CameraBoundsData[] cameraBoundsList;

    [Header("Visual Data (View)")]
    public GameObject visualPrefab; 

    
}