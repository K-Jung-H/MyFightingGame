using UnityEngine;

public class GameFlowUIController : MonoBehaviour
{
    public void OnTrainingButtonClicked()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.SelectTrainingMode();
        }
    }

    public void OnOfflineButtonClicked()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.SelectOfflineMode();
        }
    }

    public void OnBackButtonClicked()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.GoBack();
        }
    }

    public void OnHomeButtonClicked()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ChangeScene(GameSceneType.Start);
        }
    }
}