public interface IStageSelectLogic
{
    void Initialize(StageSelectManager manager);
    void Cleanup();
    void HandleInputs(int p1Move, bool p1Select, int p2Move, bool p2Select);
    void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p2Idx, bool p2Lock);
    void UpdateBackground();
    void EvaluateSceneTransition();
    bool IsPlayerActive(int playerId);
}

