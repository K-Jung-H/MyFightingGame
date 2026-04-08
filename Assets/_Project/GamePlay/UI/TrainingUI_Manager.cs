using UnityEngine;

public class TrainingUI_Manager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameLoopManager gameLoopManager;

    private void Start()
    {

        if (GameFlowManager.Instance.currentBattleType != BattleType.Training)
        {
            gameObject.SetActive(false);
        }
    }

    public void OnClickReset()
    {
        if (gameLoopManager != null)
        {
            gameLoopManager.ResetTrainingState();
        }
    }

    public void OnClickExit()
    {
        GameFlowManager.Instance.ChangeScene(GameSceneType.GameModeSelect);
    }
}