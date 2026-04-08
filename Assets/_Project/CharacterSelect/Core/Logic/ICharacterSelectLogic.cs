public interface ICharacterSelectLogic
{
    void Initialize(CharacterSelectManager manager);
    void ProcessInput();
    void OnStateUpdatedFromServer(int p1Idx, bool p1Lock, int p1Side, int p2Idx, bool p2Lock, int p2Side);
}