public static class MatchDataManager
{
    public static CharacterDataSO P1CharacterData { get; set; }
    public static CharacterDataSO P2CharacterData { get; set; }

    public static InputBindingPresetSO LeftKeyBindPreset { get; set; }
    public static InputBindingPresetSO RightKeyBindPreset { get; set; }

    public static InputBindingPresetSO LocalKeyBindPreset 
    { 
        get => LeftKeyBindPreset; 
        set => LeftKeyBindPreset = value; 
    }
}