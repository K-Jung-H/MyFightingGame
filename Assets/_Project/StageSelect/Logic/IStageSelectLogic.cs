public interface IStageSelectLogic
{
    void Initialize(StageSelectManager manager);
    void ProcessInput();
    void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock);
    void UpdateBackground();
    void EvaluateSceneTransition();
    bool IsPlayerActive(int playerId);
}

