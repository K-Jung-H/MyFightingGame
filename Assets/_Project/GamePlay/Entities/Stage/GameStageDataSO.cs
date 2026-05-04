using UnityEngine;

[CreateAssetMenu(fileName = "NewStageData", menuName = "ScriptableObjects/StageData")]
public class GameStageDataSO : ScriptableObject
{
    public string stageName;

    [SerializeField] 
    private BoundaryPlane[] boundaryPlanes;

    public StageBoundary GetBoundary()
    {
        return new StageBoundary { Planes = boundaryPlanes };
    }

#if UNITY_EDITOR
    public void SetBoundaryPlanes(BoundaryPlane[] planes)
    {
        boundaryPlanes = planes;
    }
#endif
}