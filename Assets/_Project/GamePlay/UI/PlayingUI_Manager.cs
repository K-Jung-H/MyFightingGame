using UnityEngine;
using System;

public class PlayingUI_Manager : MonoBehaviour
{
    public CameraManager cameraManager;
    public HealthBarController leftHealthBar;
    public HealthBarController rightHealthBar;
    public SpriteNumberDisplay roundTimerDisplay;
    public WinCounterUIManager winCounterUI;
    public MatchResultUIManager matchResultUI;
    public RoundUIManager roundBannerUI;

    public Sprite[] timerNumberSprites;
    public Sprite[] counterNumberSprites;

    private bool isTrainingMode = false;

    public void InitializeUI()
    {
        if (GameFlowManager.Instance != null)
        {
            isTrainingMode = GameFlowManager.Instance.currentBattleType == BattleType.Training;
        }

        if (roundTimerDisplay != null)
        {
            roundTimerDisplay.InitializeSprites(timerNumberSprites);
            if (isTrainingMode)
            {
                roundTimerDisplay.gameObject.SetActive(false);
            }
        }
        
        if (roundBannerUI != null)
        {
            roundBannerUI.InitializeCounters(counterNumberSprites);
        }
        
        if (matchResultUI != null)
        {
            matchResultUI.gameObject.SetActive(false);
        }

        if (isTrainingMode && winCounterUI != null)
        {
            winCounterUI.HideAllCounters();
        }
    }

    public void SetCameraFlip(bool isFlipped)
    {
        if (cameraManager != null) cameraManager.SetCameraFlip(isFlipped);
    }

    public void SetCameraTargets(GameObject p1, GameObject p2)
    {
        if (cameraManager != null) cameraManager.SetTargetPlayers(p1, p2);
    }

    public bool IsPlayerOneOnRightSide()
    {
        if (cameraManager != null) return cameraManager.IsPlayerOneOnRightSide();
        return false;
    }

    public void InitializeHealthBars(PlayerController p1, PlayerController p2, bool isFlipped)
    {
        if (leftHealthBar != null) leftHealthBar.Initialize(isFlipped ? p2.GetCombat() : p1.GetCombat(), false);
        if (rightHealthBar != null) rightHealthBar.Initialize(isFlipped ? p1.GetCombat() : p2.GetCombat(), true);
    }

    public void SetupWinCounter(int requiredWins)
    {
        if (isTrainingMode) return;
        
        if (winCounterUI != null)
        {
            winCounterUI.InitializeCounters(requiredWins);
            winCounterUI.UpdateCounters(0, 0);
        }
    }

    public void UpdateWinCounter(int leftWins, int rightWins)
    {
        if (isTrainingMode) return;
        if (winCounterUI != null) winCounterUI.UpdateCounters(leftWins, rightWins);
    }

    public void UpdateRoundTimer(int remainingSeconds)
    {
        if (isTrainingMode) return;
        if (roundTimerDisplay != null) roundTimerDisplay.SetNumber(remainingSeconds);
    }

    public void SyncBannerState(RoundPhase phase, int delayTicks, int timerFrames)
    {
        if (isTrainingMode) return;
        if (roundBannerUI != null) roundBannerUI.SyncBannerState(phase, delayTicks, timerFrames);
    }

    public void ShowMatchResult(int p1Wins, int p2Wins, int requiredWins, int localSlot)
    {
        if (matchResultUI != null)
        {
            matchResultUI.gameObject.SetActive(true);
            matchResultUI.ShowResult(p1Wins, p2Wins, requiredWins, localSlot);
        }
    }

    public void HideMatchResult()
    {
        if (matchResultUI != null)
        {
            matchResultUI.gameObject.SetActive(false);
        }
    }

    public void UpdateRematchSync(bool p1Ready, bool p2Ready, bool isFlipped)
    {
        if (matchResultUI != null) matchResultUI.UpdateRematchSync(p1Ready, p2Ready, isFlipped);
    }

    public void BindMatchResultAction(Action<MatchEndActionType> action)
    {
        if (matchResultUI != null) matchResultUI.OnActionRequested += action;
    }

    public void UnbindMatchResultAction(Action<MatchEndActionType> action)
    {
        if (matchResultUI != null) matchResultUI.OnActionRequested -= action;
    }
}