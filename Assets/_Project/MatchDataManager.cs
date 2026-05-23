public static class MatchDataManager
{
    public static int TrainingLocalPlayerSide = 0; // 0 = Left, 1 = Right
    public static CharacterDataSO P1CharacterData { get; set; }
    public static CharacterDataSO P2CharacterData { get; set; }

    public static GameStageDataSO SelectedStageData;

    public static InputBindingPresetSO LeftKeyBindPreset { get; set; }
    public static InputBindingPresetSO RightKeyBindPreset { get; set; }

    public static InputBindingPresetSO LocalKeyBindPreset 
    { 
        get => LeftKeyBindPreset; 
        set => LeftKeyBindPreset = value; 
    }
}