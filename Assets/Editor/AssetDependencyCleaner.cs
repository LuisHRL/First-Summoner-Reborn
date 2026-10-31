using System;
using System.IO;
using System.Linq;
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
            catch (Exception ex)
            {
                Debug.LogWarning("[PREVIEW ERROR] " + path + ": " + ex.Message);
            }
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
            if (matches.Length == 1)
            {
                Debug.Log("[CANDIDATE] " + source + " -> " + matches[0]);
                candidates++;
            }
            else if (matches.Length > 1)
            {
                Debug.LogWarning("[AMBIGUOUS] " + source + "\n" + string.Join("\n", matches));
                ambiguous++;
            }
            else
            {
                Debug.LogWarning("[MISSING] " + source);
                missing++;
            }
        }
        Debug.Log("[Asset Cleaner] Preview complete. Candidates: " + candidates +
                  ", Ambiguous: " + ambiguous + ", Missing: " + missing +
                  ". No files modified.");
    }

    [MenuItem("Tools/Asset Cleaner/Compare Asset IDs")]
    public static void CompareAssetIDs()
    {
        int compared = 0, missing = 0, unresolved = 0, differentIds = 0, skippedScenes = 0;
        foreach (string source in GetAssets(SourceFolder))
        {
            if (source.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[SKIPPED SCENE] " + source);
                skippedScenes++;
                continue;
            }
            string target = GetExpectedTarget(source);
            if (AssetDatabase.LoadMainAssetAtPath(target) == null)
            {
                Debug.LogWarning("[NO TARGET] " + source);
                missing++;
                continue;
            }
            Debug.Log("[GUID CHECK] " + source + "\n  Source GUID: " +
                      AssetDatabase.AssetPathToGUID(source) + "\n  Target GUID: " +
                      AssetDatabase.AssetPathToGUID(target));

            UnityEngine.Object[] sourceObjects = AssetDatabase.LoadAllAssetsAtPath(source);
            UnityEngine.Object[] targetObjects = AssetDatabase.LoadAllAssetsAtPath(target);
            foreach (UnityEngine.Object sourceObject in sourceObjects)
            {
                if (sourceObject == null) continue;
                string sourceGuid;
                long sourceId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceObject, out sourceGuid, out sourceId))
                    continue;
                var matches = targetObjects
                    .Where(o => o != null && o.name == sourceObject.name &&
                                o.GetType() == sourceObject.GetType()).ToArray();
                if (matches.Length > 1)
                    matches = matches.Where(o =>
                    {
                        string guid;
                        long id;
                        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out guid, out id)
                               && id == sourceId;
                    }).ToArray();
                if (matches.Length != 1)
                {
                    Debug.LogWarning("[SUB-ASSET UNRESOLVED] " + source + " : " +
                                     sourceObject.name + " (" + sourceObject.GetType().Name +
                                     "), matches: " + matches.Length);
                    unresolved++;
                    continue;
                }
                string targetGuid;
                long targetId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(matches[0], out targetGuid, out targetId))
                {
                    unresolved++;
                    continue;
                }
                Debug.Log("[ID CHECK] " + sourceObject.name + " (" +
                          sourceObject.GetType().Name + ")\n  Source: " + sourceId +
                          "\n  Target: " + targetId + "\n  Status: " +
                          (sourceId == targetId ? "SAME LOCAL ID" : "DIFFERENT LOCAL ID"));
                if (sourceId != targetId) differentIds++;
            }
            compared++;
        }
        Debug.Log("[Asset Cleaner] ID comparison complete. Compared files: " + compared +
                  ", Missing targets: " + missing + ", Unresolved sub-assets: " + unresolved +
                  ", Different local IDs: " + differentIds + ", Skipped scenes: " +
                  skippedScenes + ". No files modified.");
    }
}
