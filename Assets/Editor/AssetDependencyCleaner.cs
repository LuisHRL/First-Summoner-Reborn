using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class AssetDependencyCleaner
{
    private const string SourceFolder = "Assets/tests-animations3/SkeletonArcher";
    private const string TargetFolder = "Assets/Prefabs/New-Troops/SkeletonArcher-new";

    private static string[] GetAssets(string folder)
    {
        return AssetDatabase.FindAssets("", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
            .Distinct().OrderBy(p => p).ToArray();
    }

    private static string[] GetProjectAssets()
    {
        return AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
            .Where(p => !p.StartsWith(SourceFolder + "/", StringComparison.Ordinal))
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .ToArray();
    }

    private static string GetExpectedTarget(string source)
    {
        string relative = source.Substring(SourceFolder.Length + 1);
        return relative == "Prefab/SkeletonArcher.prefab"
            ? TargetFolder + "/Prefab/SkeletonArcher-new.prefab"
            : TargetFolder + "/" + relative;
    }

    [MenuItem("Tools/Asset Cleaner/Analyze SkeletonArcher")]
    public static void Analyze()
    {
        var sourceAssets = new HashSet<string>(GetAssets(SourceFolder));
        int pairs = 0, referringAssets = 0;
        foreach (string path in GetProjectAssets())
        {
            string[] dependencies;
            try { dependencies = AssetDatabase.GetDependencies(path, false); }
            catch (Exception ex)
            {
                Debug.LogWarning("[ANALYZE ERROR] " + path + ": " + ex.Message);
                continue;
            }
            var matches = dependencies.Where(sourceAssets.Contains).Distinct().ToArray();
            if (matches.Length == 0) continue;
            referringAssets++;
            foreach (string dependency in matches)
            {
                Debug.Log("[DEPENDENCY] " + path + " -> " + dependency);
                pairs++;
            }
        }
        Debug.Log("[Asset Cleaner] Analysis complete. Dependency pairs: " + pairs +
                  ", Referring assets: " + referringAssets + ". No files modified.");
    }

    [MenuItem("Tools/Asset Cleaner/Preview Replacements")]
    public static void PreviewReplacements()
    {
        var sourceAssets = new HashSet<string>(GetAssets(SourceFolder));
        string[] targets = GetAssets(TargetFolder);
        var targetSet = new HashSet<string>(targets);
        var usedSources = new HashSet<string>();
        foreach (string path in GetProjectAssets())
        {
            try
            {
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    if (sourceAssets.Contains(dependency)) usedSources.Add(dependency);
            }
            catch (Exception ex) { Debug.LogWarning("[PREVIEW ERROR] " + path + ": " + ex.Message); }
        }
        int candidates = 0, ambiguous = 0, missing = 0;
        foreach (string source in usedSources.OrderBy(p => p))
        {
            string expected = GetExpectedTarget(source);
            if (targetSet.Contains(expected))
            {
                Debug.Log("[CANDIDATE] " + source + " -> " + expected);
                candidates++;
                continue;
            }
            string name = Path.GetFileName(source);
            string[] matches = targets.Where(p => Path.GetFileName(p) == name).ToArray();
            if (matches.Length == 1) { Debug.Log("[CANDIDATE] " + source + " -> " + matches[0]); candidates++; }
            else if (matches.Length > 1) { Debug.LogWarning("[AMBIGUOUS] " + source + "\n" + string.Join("\n", matches)); ambiguous++; }
            else { Debug.LogWarning("[MISSING] " + source); missing++; }
        }
        Debug.Log("[Asset Cleaner] Preview complete. Candidates: " + candidates +
                  ", Ambiguous: " + ambiguous + ", Missing: " + missing + ". No files modified.");
    }

    [MenuItem("Tools/Asset Cleaner/Compare Asset IDs")]
    public static void CompareAssetIDs()
    {
        int compared = 0, missing = 0, unresolved = 0, differentIds = 0, skippedScenes = 0;
        foreach (string source in GetAssets(SourceFolder))
        {
            if (source.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            { Debug.Log("[SKIPPED SCENE] " + source); skippedScenes++; continue; }
            string target = GetExpectedTarget(source);
            if (AssetDatabase.LoadMainAssetAtPath(target) == null)
            { Debug.LogWarning("[NO TARGET] " + source); missing++; continue; }
            Debug.Log("[GUID CHECK] " + source + "\n  Source GUID: " +
                      AssetDatabase.AssetPathToGUID(source) + "\n  Target GUID: " +
                      AssetDatabase.AssetPathToGUID(target));
            UnityEngine.Object[] sourceObjects = AssetDatabase.LoadAllAssetsAtPath(source);
            UnityEngine.Object[] targetObjects = AssetDatabase.LoadAllAssetsAtPath(target);
            foreach (UnityEngine.Object sourceObject in sourceObjects)
            {
                if (sourceObject == null) continue;
                string sourceGuid; long sourceId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceObject, out sourceGuid, out sourceId)) continue;
                var matches = targetObjects.Where(o => o != null && o.name == sourceObject.name &&
                                                        o.GetType() == sourceObject.GetType()).ToArray();
                if (matches.Length > 1)
                    matches = matches.Where(o => {
                        string guid; long id;
                        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out guid, out id) && id == sourceId;
                    }).ToArray();
                if (matches.Length != 1)
                {
                    Debug.LogWarning("[SUB-ASSET UNRESOLVED] " + source + " : " +
                                     sourceObject.name + " (" + sourceObject.GetType().Name +
                                     "), matches: " + matches.Length);
                    unresolved++; continue;
                }
                string targetGuid; long targetId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(matches[0], out targetGuid, out targetId))
                { unresolved++; continue; }
                Debug.Log("[ID CHECK] " + sourceObject.name + " (" + sourceObject.GetType().Name +
                          ")\n  Source: " + sourceId + "\n  Target: " + targetId +
                          "\n  Status: " + (sourceId == targetId ? "SAME LOCAL ID" : "DIFFERENT LOCAL ID"));
                if (sourceId != targetId) differentIds++;
            }
            compared++;
        }
        Debug.Log("[Asset Cleaner] ID comparison complete. Compared files: " + compared +
                  ", Missing targets: " + missing + ", Unresolved sub-assets: " + unresolved +
                  ", Different local IDs: " + differentIds + ", Skipped scenes: " +
                  skippedScenes + ". No files modified.");
    }

    // This first migration stage is deliberately READ-ONLY. It checks exact target paths,
    // referenced fileIDs and text serialization before allowing any future rewrite.
    [MenuItem("Tools/Asset Cleaner/Preview Safe Migration")]
    public static void PreviewSafeMigration()
    {
        var sourcePaths = GetAssets(SourceFolder);
        var sourceByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string source in sourcePaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(source);
            if (!string.IsNullOrEmpty(guid)) sourceByGuid[guid] = source;
        }

        int safe = 0, blocked = 0, references = 0;
        var referenceRegex = new Regex(@"fileID:\s*(-?\d+)[^\r\n{}]*,\s*guid:\s*([0-9a-fA-F]{32})|guid:\s*([0-9a-fA-F]{32})");
        var idCache = new Dictionary<string, HashSet<long>>();

        foreach (string assetPath in GetProjectAssets())
        {
            var paths = new List<string> { assetPath };
            if (File.Exists(assetPath + ".meta")) paths.Add(assetPath + ".meta");
            foreach (string filePath in paths)
            {
                if (!File.Exists(filePath)) continue;
                string content;
                try
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    if (bytes.Any(b => b == 0)) continue; // binary files must never be rewritten as text
                    content = new UTF8Encoding(false, true).GetString(bytes);
                }
                catch { continue; }

                foreach (Match match in referenceRegex.Matches(content))
                {
                    string guid = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                    string source;
                    if (!sourceByGuid.TryGetValue(guid, out source)) continue;
                    references++;
                    string target = GetExpectedTarget(source);
                    if (!File.Exists(target))
                    {
                        Debug.LogWarning("[BLOCKED: NO EXACT TARGET] " + filePath + " -> " + source);
                        blocked++; continue;
                    }
                    if (match.Groups[1].Success)
                    {
                        long id;
                        if (!long.TryParse(match.Groups[1].Value, out id))
                        { Debug.LogWarning("[BLOCKED: INVALID ID] " + filePath); blocked++; continue; }
                        HashSet<long> targetIds;
                        if (!idCache.TryGetValue(target, out targetIds))
                        {
                            targetIds = new HashSet<long>();
                            if (!target.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(target))
                                {
                                    if (obj == null) continue;
                                    string objGuid; long objId;
                                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out objGuid, out objId))
                                        targetIds.Add(objId);
                                }
                            }
                            idCache[target] = targetIds;
                        }
                        if (!targetIds.Contains(id))
                        {
                            Debug.LogWarning("[BLOCKED: FILE ID NOT VERIFIED] " + filePath +
                                             " -> " + source + " (fileID " + id + ")");
                            blocked++; continue;
                        }
                    }
                    Debug.Log("[SAFE CANDIDATE] " + filePath + " : " + source + " -> " + target);
                    safe++;
                }
            }
        }
        Debug.Log("[Asset Cleaner] Safe migration preview complete. References found: " +
                  references + ", Safe candidates: " + safe + ", Blocked: " + blocked +
                  ". No files modified.");
    }

    // Applies only references whose target sub-asset ID was verified by the preview rules.
    // Each modified file is backed up outside Assets, and blocked references stay untouched.
    [MenuItem("Tools/Asset Cleaner/Apply Verified Replacements")]
    public static void ApplyVerifiedReplacements()
    {
        if (!EditorUtility.DisplayDialog("Apply verified replacements",
            "Replace only verified SkeletonArcher references? Unverified references will remain unchanged. " +
            "Backups will be written outside Assets. Nothing will be deleted.",
            "Apply verified", "Cancel")) return;

        var sourceByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string source in GetAssets(SourceFolder))
        {
            string guid = AssetDatabase.AssetPathToGUID(source);
            if (!string.IsNullOrEmpty(guid)) sourceByGuid[guid] = source;
        }

        // Match a complete YAML object reference or an isolated GUID token.
        var referenceRegex = new Regex(@"fileID:\s*(-?\d+)[^\r\n{}]*,\s*guid:\s*([0-9a-fA-F]{32})|guid:\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled);
        var idCache = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
        string backupRoot = Path.Combine(Directory.GetCurrentDirectory(), "AssetCleanerBackups",
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        int changedFiles = 0, replaced = 0, blocked = 0;
        var changedPaths = new List<string>();
        var report = new StringBuilder();

        foreach (string assetPath in GetProjectAssets())
        {
            var paths = new List<string> { assetPath };
            if (File.Exists(assetPath + ".meta")) paths.Add(assetPath + ".meta");
            foreach (string filePath in paths)
            {
                if (!File.Exists(filePath)) continue;
                byte[] bytes;
                string content;
                try
                {
                    bytes = File.ReadAllBytes(filePath);
                    if (bytes.Any(b => b == 0)) continue;
                    content = new UTF8Encoding(false, true).GetString(bytes);
                }
                catch { continue; }

                int localReplaced = 0;
                string updated = referenceRegex.Replace(content, match =>
                {
                    string oldGuid = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                    string source;
                    if (!sourceByGuid.TryGetValue(oldGuid, out source)) return match.Value;
                    string target = GetExpectedTarget(source);
                    if (!File.Exists(target))
                    {
                        blocked++;
                        report.AppendLine("BLOCKED (no target): " + filePath + " -> " + source);
                        return match.Value;
                    }
                    string newGuid = AssetDatabase.AssetPathToGUID(target);
                    if (string.IsNullOrEmpty(newGuid))
                    {
                        blocked++;
                        report.AppendLine("BLOCKED (no target GUID): " + filePath + " -> " + target);
                        return match.Value;
                    }
                    if (match.Groups[1].Success)
                    {
                        long id;
                        if (!long.TryParse(match.Groups[1].Value, out id))
                        {
                            blocked++; return match.Value;
                        }
                        HashSet<long> ids;
                        if (!idCache.TryGetValue(target, out ids))
                        {
                            ids = new HashSet<long>();
                            if (!target.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(target))
                                {
                                    if (obj == null) continue;
                                    string objectGuid; long objectId;
                                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out objectGuid, out objectId))
                                        ids.Add(objectId);
                                }
                            }
                            idCache[target] = ids;
                        }
                        if (!ids.Contains(id))
                        {
                            blocked++;
                            report.AppendLine("BLOCKED (fileID " + id + "): " + filePath + " -> " + source);
                            return match.Value;
                        }
                    }
                    localReplaced++;
                    return match.Value.Substring(0, match.Value.Length - oldGuid.Length) + newGuid;
                });
                if (localReplaced == 0) continue;
                try
                {
                    string backupPath = Path.Combine(backupRoot, filePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                    File.Copy(filePath, backupPath, false);
                    // Keep the original file's encoding and line endings intact (GUIDs are ASCII).
                    File.WriteAllText(filePath, updated, new UTF8Encoding(false));
                    changedPaths.Add(filePath);
                    changedFiles++;
                    replaced += localReplaced;
                    report.AppendLine("CHANGED (" + localReplaced + "): " + filePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Asset Cleaner] Could not write " + filePath + ": " + ex.Message);
                    report.AppendLine("ERROR: " + filePath + ": " + ex.Message);
                }
            }
        }
        if (changedFiles > 0)
        {
            Directory.CreateDirectory(backupRoot);
            File.WriteAllText(Path.Combine(backupRoot, "migration-report.txt"), report.ToString());
            AssetDatabase.Refresh();
        }
        Debug.Log("[Asset Cleaner] Verified migration complete. Changed files: " + changedFiles +
                  ", replaced references: " + replaced + ", blocked references: " + blocked +
                  ". Backup folder: " + (changedFiles > 0 ? backupRoot : "none") +
                  ". No assets deleted.");
    }

}
