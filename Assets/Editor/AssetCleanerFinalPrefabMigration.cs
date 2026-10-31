using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AssetCleanerFinalPrefabMigration
{
    private const string Root = "Assets/Prefabs/New-Troops/SkeletonArcher-new";
    private const string Old = "Assets/tests-animations3/SkeletonArcher";

    private struct Rule
    {
        public string File, OldAsset, NewAsset;
        public Rule(string file, string oldAsset, string newAsset)
        { File = file; OldAsset = oldAsset; NewAsset = newAsset; }
    }

    private static readonly Rule[] Rules = {
        new Rule(Root + "/Prefab/SkeletonArcher-new.prefab", Old + "/Prefab/Bow.prefab", Root + "/Prefab/Bow.prefab"),
        new Rule(Root + "/SkeletonArcher-new.prefab", Old + "/Prefab/Bow.prefab", Root + "/Prefab/Bow.prefab"),
        new Rule(Root + "/Prefab/Arrow.prefab", Old + "/Mesh/Arrow.fbx", Root + "/Mesh/Arrow.fbx"),
        new Rule(Root + "/DemoScene/DemoScene.unity", Old + "/Prefab/SkeletonArcher.prefab", Root + "/Prefab/SkeletonArcher-new.prefab")
    };

    private static bool Validate(Rule rule, out string oldToken, out string newToken, out string content, out string error)
    {
        oldToken = newToken = content = error = null;
        if (!File.Exists(rule.File) || !File.Exists(rule.NewAsset) || !File.Exists(rule.OldAsset))
        { error = "Missing file or asset: " + rule.File; return false; }
        string oldGuid = AssetDatabase.AssetPathToGUID(rule.OldAsset);
        string newGuid = AssetDatabase.AssetPathToGUID(rule.NewAsset);
        if (oldGuid.Length != 32 || newGuid.Length != 32)
        { error = "GUID unavailable: " + rule.File; return false; }
        oldToken = "m_SourcePrefab: {fileID: 100100000, guid: " + oldGuid + ", type: 3}";
        newToken = "m_SourcePrefab: {fileID: 100100000, guid: " + newGuid + ", type: 3}";
        try { content = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(rule.File)); }
        catch (Exception ex) { error = "Cannot read text YAML: " + ex.Message; return false; }
        int count = Count(content, oldToken);
        if (count != 1) { error = "Expected exactly 1 old source reference, found " + count + ": " + rule.File; return false; }
        if (Count(content, newToken) != 0) { error = "New source reference already exists: " + rule.File; return false; }
        return true;
    }

    private static int Count(string text, string token)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        { count++; index += token.Length; }
        return count;
    }

    [MenuItem("Tools/Asset Cleaner/Final Prefab Migration/1 - Preview")]
    public static void Preview()
    {
        int ready = 0;
        foreach (Rule rule in Rules)
        {
            string oldToken, newToken, content, error;
            if (Validate(rule, out oldToken, out newToken, out content, out error))
            { Debug.Log("[READY] " + rule.File + "\n  " + rule.OldAsset + " -> " + rule.NewAsset); ready++; }
            else Debug.LogWarning("[NOT READY] " + error);
        }
        Debug.Log("[Final Prefab Migration] Preview: " + ready + "/4 ready. No files modified.");
    }

    [MenuItem("Tools/Asset Cleaner/Final Prefab Migration/2 - Apply (with backup)")]
    public static void Apply()
    {
        // All-or-nothing preflight: never begin if a reference is unexpected.
        var oldTokens = new string[Rules.Length];
        var newTokens = new string[Rules.Length];
        var contents = new string[Rules.Length];
        for (int i = 0; i < Rules.Length; i++)
        {
            string error;
            if (!Validate(Rules[i], out oldTokens[i], out newTokens[i], out contents[i], out error))
            { Debug.LogError("[Final Prefab Migration] Aborted: " + error); return; }
        }
        if (!EditorUtility.DisplayDialog("Final SkeletonArcher prefab migration",
            "Replace four exact m_SourcePrefab references? Original files will be backed up outside Assets. No files or folders will be deleted.",
            "Apply with backup", "Cancel")) return;
        string backup = Path.Combine(Directory.GetCurrentDirectory(), "AssetCleanerBackups", "FinalPrefab_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        try
        {
            // Back up all four BEFORE writing any changes.
            for (int i = 0; i < Rules.Length; i++)
            {
                string destination = Path.Combine(backup, Rules[i].File.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(Rules[i].File, destination, false);
            }
            for (int i = 0; i < Rules.Length; i++)
            {
                // Preserve original bytes except for ASCII GUID (same length).
                byte[] original = File.ReadAllBytes(Rules[i].File);
                string updated = contents[i].Replace(oldTokens[i], newTokens[i]);
                File.WriteAllBytes(Rules[i].File, new UTF8Encoding(false).GetBytes(updated));
                Debug.Log("[UPDATED] " + Rules[i].File);
            }
            AssetDatabase.Refresh();
            Debug.Log("[Final Prefab Migration] Four source references updated. Backup: " + backup + ". No assets deleted. Run Analyze SkeletonArcher and test gameplay.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Final Prefab Migration] Error: " + ex.Message + ". Restore files from backup if needed: " + backup);
        }
    }
}
