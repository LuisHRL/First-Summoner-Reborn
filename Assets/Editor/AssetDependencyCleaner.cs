
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class AssetDependencyCleaner
{
    private const string SourceFolder =
        "Assets/tests-animations3/SkeletonArcher";

    private const string TargetFolder =
        "Assets/Prefabs/New-Troops/SkeletonArcher-new";

    [MenuItem("Tools/Asset Cleaner/Preview Replacements")]
    public static void PreviewReplacements()
    {
        var sourcePaths = AssetDatabase
            .FindAssets("", new[] { SourceFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .Distinct()
            .ToArray();

        var targetPaths = AssetDatabase
            .FindAssets("", new[] { TargetFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .Distinct()
            .ToArray();

        var projectPaths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/"))
            .Where(p => !p.StartsWith(SourceFolder + "/"))
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .ToArray();

        int safe = 0;
        int ambiguous = 0;
        int missing = 0;

        var usedSources = new HashSet<string>();

        foreach (string path in projectPaths)
        {
            foreach (string dependency in
                     AssetDatabase.GetDependencies(path, false))
            {
                if (dependency.StartsWith(SourceFolder + "/"))
                    usedSources.Add(dependency);
            }
        }

        foreach (string source in usedSources.OrderBy(p => p))
        {
            string relativePath =
                source.Substring(SourceFolder.Length + 1);

            string expectedPath =
                TargetFolder + "/" + relativePath;

            string extension = Path.GetExtension(source);

            var matches = targetPaths
                .Where(p =>
                    Path.GetFileName(p) ==
                    Path.GetFileName(source))
                .ToArray();

            if (matches.Length == 0)
            {
                Debug.LogWarning(
                    $"[MISSING] {source}");
                missing++;
            }
            else if (matches.Length == 1)
            {
                Debug.Log(
                    $"[CANDIDATE] {source} → {matches[0]}");
                safe++;
            }
            else
            {
                Debug.LogWarning(
                    $"[AMBIGUOUS] {source}\n" +
                    string.Join("\n", matches));
                ambiguous++;
            }
        }

        Debug.Log(
            $"[Asset Cleaner] Preview complete. " +
            $"Candidates: {safe}, " +
            $"Ambiguous: {ambiguous}, " +
            $"Missing: {missing}. " +
            $"No assets were modified.");
    }
}
