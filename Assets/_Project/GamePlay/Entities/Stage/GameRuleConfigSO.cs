using UnityEngine;

[CreateAssetMenu(fileName = "GameRuleConfig", menuName = "ScriptableObjects/GameRuleConfig")]
public class GameRuleConfigSO : ScriptableObject
{
    public float playerCollisionMinDistance = 0.5f;
    public float globalGravity = 0.02f;
    public Vector3 p1SpawnPos = new Vector3(-2, 0, 0);
    public Vector3 p2SpawnPos = new Vector3(2, 0, 0);
    public float preRoundDelaySeconds = 3.0f;
    public float postRoundDelaySeconds = 3.0f;
    public float climaxSlowMoScale = 0.1f;
    public float climaxHealthRatio = 0.15f;
    public float climaxRecoverySeconds = 1.0f;
    public float climaxActivationDistance = 2.0f;
}