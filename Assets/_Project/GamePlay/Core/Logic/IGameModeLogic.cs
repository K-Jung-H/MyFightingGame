using UnityEngine;

public interface IGameModeLogic
{
    void Initialize(GameLoopManager manager);
    void StartGame();
    void ProcessFixedUpdate();
    void OnGUI();
    bool ShouldCheckRoundEnd();
    bool ShouldUpdateTimer();
    void HandleMatchEndAction(MatchEndActionType actionType);
}