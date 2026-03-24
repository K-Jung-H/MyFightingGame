using UnityEngine;

public class RoundTimerManager
{
    private int initialTimerFrames;
    private int currentTimerFrames;
    private bool isTimerRunning;
    private bool isTimerPaused;

    public void InitializeTimer(int durationSeconds)
    {
        initialTimerFrames = durationSeconds * 60;
        currentTimerFrames = initialTimerFrames;
        isTimerRunning = true;
        isTimerPaused = false;
    }

    public void UpdateTick()
    {
        if (!isTimerRunning)
        {
            return;
        }

        if (isTimerPaused)
        {
            return;
        }

        if (currentTimerFrames > 0)
        {
            currentTimerFrames--;
        }
        else
        {
            isTimerRunning = false;
        }
    }

    public void SetPauseState(bool isPaused)
    {
        isTimerPaused = isPaused;
    }

    public void ExportState(ref GameStateSnapshot snapshot)
    {
        snapshot.currentTimerFrames = currentTimerFrames;
        snapshot.isTimerPaused = isTimerPaused;
    }

    public void ImportState(GameStateSnapshot snapshot)
    {
        currentTimerFrames = snapshot.currentTimerFrames;
        isTimerPaused = snapshot.isTimerPaused;
        isTimerRunning = currentTimerFrames > 0;
    }

    public int GetRemainingSeconds()
    {
        return CalculateSecondsFromFrames(currentTimerFrames);
    }

    private int CalculateSecondsFromFrames(int frames)
    {
        return Mathf.CeilToInt((float)frames / 60f);
    }
}