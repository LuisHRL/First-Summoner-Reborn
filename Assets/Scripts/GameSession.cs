public static class GameSession
{
    // 1 = Normal, 2 = Medium. Kept for compatibility with existing gameplay.
    public static int SelectedDifficulty = 1;

    // Runtime-only selection; ScriptableObject definitions are never mutated.
    private static bool pendingMissionMenuReturn;
    public static void RequestMissionMenuReturn() { pendingMissionMenuReturn = true; }
    public static bool ConsumeMissionMenuReturn()
    {
        bool pending = pendingMissionMenuReturn;
        pendingMissionMenuReturn = false;
        return pending;
    }

    public static string MissionReturnSceneName { get; private set; }
    public static void SetMissionReturnScene(string sceneName) { MissionReturnSceneName = sceneName; }

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

    public static float MissionElapsedSeconds { get; private set; }
    public static bool MissionTimerRunning { get; private set; }
    public static bool MissionTimedOut { get; private set; }
    public static float MissionTimeRemaining => UnityEngine.Mathf.Max(0f,
        (SelectedMission != null ? SelectedMission.timeLimitSeconds : 0f) - MissionElapsedSeconds);

    public static void StartMissionTimer()
    {
        MissionElapsedSeconds = 0f;
        MissionTimedOut = false;
        MissionTimerRunning = SelectedMission != null;
    }

    public static void TickMissionTimer(float deltaSeconds)
    {
        if (!MissionTimerRunning) return;
        MissionElapsedSeconds += UnityEngine.Mathf.Max(0f, deltaSeconds);
        if (MissionTimeRemaining <= 0f)
        {
            MissionElapsedSeconds = SelectedMission.timeLimitSeconds;
            MissionTimerRunning = false;
            MissionTimedOut = true;
            UnityEngine.Debug.Log("Mission failed: time limit reached.");
        }
    }

    public static void ClearMissionSelection()
    {
        SelectedMission = null;
        SelectedWorld = null;
        MissionReturnSceneName = null;
        MissionTimerRunning = false;
        MissionTimedOut = false;
    }

    /// <summary>Call only from the actual victory condition, never on scene exit.</summary>
    public static bool CompleteSelectedMission()
    {
        if (SelectedMission == null || SelectedWorld == null ||
            MissionTimedOut || !MissionTimerRunning)
            return false;
        var difficulty = (MissionProgress.Difficulty)SelectedDifficulty;
        if (!System.Enum.IsDefined(typeof(MissionProgress.Difficulty), difficulty))
            return false;
        MissionTimerRunning = false;
        MissionProgress.RecordVictory(SelectedMission, difficulty, MissionElapsedSeconds);
        return true;
    }
}
