using UnityEngine;
using UnityEngine.UI;

public class RoundUIManager : MonoBehaviour
{
    public Image counterImage;
    public Image resultBannerImage;

    public Sprite spriteGameStart;
    public Sprite spriteKO;
    public Sprite spriteTimeUp;

    private Sprite[] counterSprites;
    private RoundPhase lastPhase;
    private float displayTimer;
    private const float DISPLAY_DURATION = 1.0f;

    public void InitializeCounters(Sprite[] counters)
    {
        counterSprites = counters;
        HideAll();
    }

    public void SyncBannerState(RoundPhase currentPhase, int delayTicks, int timerFrames)
    {
        if (counterImage == null || resultBannerImage == null) return;

        if (currentPhase == RoundPhase.PreRound)
        {
            int secondsLeft = Mathf.CeilToInt(delayTicks / 60f);
            ShowCountdown(secondsLeft);
            displayTimer = DISPLAY_DURATION;
        }
        else if (currentPhase == RoundPhase.Fighting)
        {
            if (lastPhase == RoundPhase.PreRound || displayTimer > 0f)
            {
                ShowResultBanner(spriteGameStart);
                displayTimer -= Time.deltaTime;
                
                if (displayTimer <= 0f)
                {
                    HideAll();
                }
            }
            else
            {
                HideAll();
            }
        }
        else if (currentPhase == RoundPhase.PostRound)
        {
            if (timerFrames <= 0)
            {
                ShowResultBanner(spriteTimeUp);
            }
            else
            {
                ShowResultBanner(spriteKO);
            }
        }

        lastPhase = currentPhase;
    }

    private void ShowCountdown(int seconds)
    {
        if (counterSprites != null && seconds >= 0 && seconds < counterSprites.Length)
        {
            if (counterImage.sprite != counterSprites[seconds])
            {
                counterImage.sprite = counterSprites[seconds];
            }

            if (!counterImage.gameObject.activeSelf) 
            {
                counterImage.gameObject.SetActive(true);
            }

            if (resultBannerImage.gameObject != counterImage.gameObject && resultBannerImage.gameObject.activeSelf) 
            {
                resultBannerImage.gameObject.SetActive(false);
            }
            
            if (!gameObject.activeSelf) 
            {
                gameObject.SetActive(true);
            }
        }
    }

    private void ShowResultBanner(Sprite targetSprite)
    {
        if (targetSprite != null)
        {
            if (resultBannerImage.sprite != targetSprite)
            {
                resultBannerImage.sprite = targetSprite;
            }

            if (!resultBannerImage.gameObject.activeSelf) 
            {
                resultBannerImage.gameObject.SetActive(true);
            }

            if (counterImage.gameObject != resultBannerImage.gameObject && counterImage.gameObject.activeSelf) 
            {
                counterImage.gameObject.SetActive(false);
            }

            if (!gameObject.activeSelf) 
            {
                gameObject.SetActive(true);
            }
        }
    }

    private void HideAll()
    {
        if (counterImage != null && counterImage.gameObject.activeSelf) 
        {
            counterImage.gameObject.SetActive(false);
        }

        if (resultBannerImage != null && resultBannerImage.gameObject != counterImage?.gameObject && resultBannerImage.gameObject.activeSelf) 
        {
            resultBannerImage.gameObject.SetActive(false);
        }

        if (gameObject.activeSelf) 
        {
            gameObject.SetActive(false);
        }
    }
}