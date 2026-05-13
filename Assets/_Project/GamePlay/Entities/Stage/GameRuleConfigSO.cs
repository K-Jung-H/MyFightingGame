using UnityEngine;

[CreateAssetMenu(fileName = "GameRuleConfig", menuName = "ScriptableObjects/GameRuleConfig")]
public class GameRuleConfigSO : ScriptableObject
{
    public float globalGravity = 0.02f;
    public Vector3 p1SpawnPos = new Vector3(-2, 0, 0);
    public Vector3 p2SpawnPos = new Vector3(2, 0, 0);
    public int preRoundDelayFrames = 180;
    public int postRoundDelayFrames = 180;
    public float climaxSlowMoScale = 0.1f;
    public FP64 FP_ClimaxSlowMoScale => FP64.FromFloat(climaxSlowMoScale);

    public float climaxHealthRatio = 0.15f;
    public int climaxRecoveryFrames = 60;
    public float climaxActivationDistance = 2.0f;

    public FP64 FP_MaxPlayerDistance = new FP64(60 * 65536); // 60
    public int maxWallBouncesPerCombo = 2;
    public float wallBounceYBoost = 0.2f; 
    public float minBounceXZSpeed = 0.05f;
    public FP64 FP_WallBounceYBoost => FP64.FromFloat(wallBounceYBoost);
    public FP64 FP_MinBounceXZSpeed => FP64.FromFloat(minBounceXZSpeed);
}