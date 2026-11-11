public static class GameSession
{
    // 1 = Normal, 2 = Medium. Kept for compatibility with existing gameplay.
    public static int SelectedDifficulty = 1;

    // Runtime-only selection; ScriptableObject definitions are never mutated.
    public static MissionDefinition SelectedMission { get; private set; }
    public static MissionWorldDefinition SelectedWorld { get; private set; }

    public static bool TrySelectMission(
        MissionWorldDefinition world,
        MissionRegionDefinition region,
        MissionDefinition mission,
        MissionProgress.Difficulty difficulty)
    {
        if (world == null || region == null || mission == null ||
            !MissionProgress.IsMissionUnlocked(world, region, mission, difficulty))
            return false;

        if (world.regions == null ||
            System.Array.IndexOf(world.regions, region) < 0 ||
            region.missions == null ||
            System.Array.IndexOf(region.missions, mission) < 0)
            return false;

        SelectedWorld = world;
        SelectedMission = mission;
        SelectedDifficulty = (int)difficulty;
        return true;
    }

    public static void ClearMissionSelection()
    {
        SelectedMission = null;
        SelectedWorld = null;
    }

    /// <summary>Call only from the actual victory condition, never on scene exit.</summary>
    public static bool CompleteSelectedMission()
    {
        if (SelectedMission == null || SelectedWorld == null)
            return false;
        var difficulty = (MissionProgress.Difficulty)SelectedDifficulty;
        if (!System.Enum.IsDefined(typeof(MissionProgress.Difficulty), difficulty))
            return false;
        MissionProgress.MarkCompleted(SelectedMission, difficulty);
        return true;
    }
}
