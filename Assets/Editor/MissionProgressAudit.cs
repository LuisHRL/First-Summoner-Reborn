#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MissionProgressAudit
{
    [MenuItem("Tools/First Summoner/Diagnose Mission Progression")]
    public static void Run()
    {
        const string worldPath = "Assets/GameData/Missions/MissionWorld.asset";
        var world = AssetDatabase.LoadAssetAtPath<MissionWorldDefinition>(worldPath);
        var sb = new StringBuilder();
        sb.AppendLine("Mission progression audit (read-only)");
        sb.AppendLine("World asset: " + worldPath);
        if (world == null)
            sb.AppendLine("ERROR: World asset not found.");
        else if (world.regions == null)
            sb.AppendLine("ERROR: Regions array is null.");
        else
        {
            sb.AppendLine("Regions: " + world.regions.Length);
            for (int r = 0; r < world.regions.Length; r++)
            {
                var region = world.regions[r];
                if (region == null)
                {
                    sb.AppendLine("Region [" + r + "]: NULL");
                    continue;
                }
                sb.AppendLine("Region [" + r + "]: " + region.displayName +
                    " asset=" + AssetDatabase.GetAssetPath(region) +
                    " missions=" + (region.missions == null ? "NULL" : region.missions.Length.ToString()));
                if (region.missions == null) continue;
                for (int m = 0; m < region.missions.Length; m++)
                {
                    var mission = region.missions[m];
                    if (mission == null)
                    {
                        sb.AppendLine("  [" + m + "] NULL - blocks progression after this slot");
                        continue;
                    }
                    bool completed = MissionProgress.IsCompleted(mission, MissionProgress.Difficulty.Normal);
                    bool unlocked = MissionProgress.IsMissionUnlocked(
                        world, region, mission, MissionProgress.Difficulty.Normal);
                    sb.AppendLine("  [" + m + "] " + mission.displayName +
                        " asset=" + AssetDatabase.GetAssetPath(mission) +
                        " unlocked=" + unlocked + " completed=" + completed +
                        " position=" + mission.mapPosition);
                }
            }
        }
        const string folder = "Assets/FirstSummonerDiagnostics";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "FirstSummonerDiagnostics");
        string output = folder + "/MissionProgressAudit.txt";
        File.WriteAllText(output, sb.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(output);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(output);
        Debug.Log("Mission progression audit written to " + output + "\n" + sb);
    }
}
#endif