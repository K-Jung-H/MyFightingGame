using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum StartSceneState
{
    Idle,
    Animating,
    Finished
}

public class StartSceneManager : MonoBehaviour
{
    public Button fullScreenPanel;
    public Button dedicatedServerButton;
    public Animator backgroundAnimator;
    public GameSceneType nextSceneType = GameSceneType.GameModeSelect;
    public string targetAnimationStateName = "YourAnimationStateName";

    private StartSceneState currentSceneState = StartSceneState.Idle;
    private Coroutine animationCoroutine;

    private void Start()
    {
        fullScreenPanel.onClick.AddListener(OnPanelClicked);
        
        if (dedicatedServerButton != null)
        {
            dedicatedServerButton.onClick.AddListener(OnDedicatedServerButtonClicked);
        }
    }

    private void OnPanelClicked()
    {
        switch (currentSceneState)
        {
            case StartSceneState.Idle:
                StartAnimation();
                break;
            case StartSceneState.Animating:
                SkipAnimation();
                break;
            case StartSceneState.Finished:
                ProceedToNextScene();
                break;
        }
    }

    private void OnDedicatedServerButtonClicked()
    {
        fullScreenPanel.interactable = false;
        dedicatedServerButton.interactable = false;

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.StartDedicatedServer();
        }
    }

    private void StartAnimation()
    {
        currentSceneState = StartSceneState.Animating;
        backgroundAnimator.SetTrigger("PlayAnim");
        animationCoroutine = StartCoroutine(TrackAnimationProgress());
    }

    private void SkipAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        
        backgroundAnimator.Play(targetAnimationStateName, 0, 1.0f);
        currentSceneState = StartSceneState.Finished;
    }

    private void ProceedToNextScene()
    {
        fullScreenPanel.interactable = false;
        
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ChangeScene(nextSceneType);
        }
    }

    private IEnumerator TrackAnimationProgress()
    {
        yield return null;

        AnimatorStateInfo stateInfo = backgroundAnimator.GetCurrentAnimatorStateInfo(0);
        yield return new WaitForSeconds(stateInfo.length);

        currentSceneState = StartSceneState.Finished;
    }
}