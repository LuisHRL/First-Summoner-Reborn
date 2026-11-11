using System;
using UnityEngine;

/// <summary>Completion is stored per mission and difficulty, never by display name.</summary>
public static class MissionProgress
{
    public enum Difficulty { Normal = 1, Medium = 2 }

    private const string Prefix = "FSR.MissionProgress.v1.";

    public static bool IsCompleted(MissionDefinition mission, Difficulty difficulty)
    {
        return HasId(mission) && PlayerPrefs.GetInt(Key(mission, difficulty), 0) == 1;
    }

    public static void MarkCompleted(MissionDefinition mission, Difficulty difficulty)
    {
        if (!HasId(mission) || !Enum.IsDefined(typeof(Difficulty), difficulty)) return;
        PlayerPrefs.SetInt(Key(mission, difficulty), 1);
        PlayerPrefs.Save();
    }

    /// <summary>Medium unlocks globally when every mission in every region is complete on Normal.</summary>
    public static bool IsUnlocked(MissionWorldDefinition world, Difficulty difficulty)
    {
        if (difficulty == Difficulty.Normal) return true;
        if (difficulty != Difficulty.Medium || world == null ||
            world.regions == null || world.regions.Length == 0) return false;

        bool hasMission = false;
        foreach (MissionRegionDefinition region in world.regions)
        {
            if (region == null || region.missions == null || region.missions.Length == 0)
                return false;
            foreach (MissionDefinition mission in region.missions)
            {
                if (!HasId(mission) || !IsCompleted(mission, Difficulty.Normal))
                    return false;
                hasMission = true;
            }
        }
        return hasMission;
    }

    /// <summary>Regions unlock in world order after all missions in prior regions.</summary>
    public static bool IsRegionUnlocked(MissionWorldDefinition world,
        MissionRegionDefinition region, Difficulty difficulty)
    {
        if (world == null || region == null || world.regions == null ||
            !IsUnlocked(world, difficulty)) return false;
        int index = Array.IndexOf(world.regions, region);
        if (index < 0) return false;
        for (int r = 0; r < index; r++)
        {
            var previousRegion = world.regions[r];
            if (previousRegion == null || previousRegion.missions == null ||
                previousRegion.missions.Length == 0) return false;
            foreach (var mission in previousRegion.missions)
                if (!HasId(mission) || !IsCompleted(mission, difficulty))
                    return false;
        }
        return true;
    }

    /// <summary>Missions unlock sequentially inside an unlocked region.</summary>
    public static bool IsMissionUnlocked(MissionWorldDefinition world,
        MissionRegionDefinition region, MissionDefinition mission, Difficulty difficulty)
    {
        if (!IsRegionUnlocked(world, region, difficulty) ||
            mission == null || region.missions == null) return false;
        int index = Array.IndexOf(region.missions, mission);
        if (index < 0) return false;
        if (index == 0) return true;
        var previous = region.missions[index - 1];
        return HasId(previous) && IsCompleted(previous, difficulty);
    }

    public static int CompletedCount(MissionRegionDefinition region, Difficulty difficulty)
    {
        if (region == null || region.missions == null) return 0;
        int count = 0;
        foreach (MissionDefinition mission in region.missions)
            if (IsCompleted(mission, difficulty)) count++;
        return count;
    }

    private static bool HasId(MissionDefinition mission)
    {
        return mission != null && !string.IsNullOrWhiteSpace(mission.PersistentId);
    }

    private static string Key(MissionDefinition mission, Difficulty difficulty)
    {
        return Prefix + mission.PersistentId + "." + (int)difficulty;
    }
}
