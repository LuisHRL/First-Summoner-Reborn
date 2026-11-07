#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only preview of potential HDRP/Demo folder quarantine.</summary>
public static class HdrpDemoQuarantinePreview
{
    [MenuItem("Tools/Asset Dependency Manager/HDRP-Demo Quarantine Preview (Read Only)")]
    public static void Run()
    {
        // Folder names are generic classification rules, not asset paths or GUIDs.
        var folderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "HDRP", "Demo" };
        var candidates = AssetDatabase.FindAssets("", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
            .Where(p => p.Split('/').Skip(1).Any(s => folderNames.Contains(s)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidateSet = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
        var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var buildDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scene in buildScenes)
        {
            buildDependencies.Add(scene);
            foreach (var dependency in AssetDatabase.GetDependencies(scene, true)) buildDependencies.Add(dependency);
        }

        var allAssets = AssetDatabase.FindAssets("", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var externalReferencers = candidates.ToDictionary(p => p, p => new List<string>(), StringComparer.OrdinalIgnoreCase);
        int scanned = 0;
        try
        {
            foreach (var asset in allAssets)
            {
                if (candidateSet.Contains(asset)) continue;
                scanned++;
                if (scanned % 100 == 0)
                    EditorUtility.DisplayProgressBar("Quarantine preview (read-only)", asset, (float)scanned / Math.Max(1, allAssets.Length));
                string[] dependencies;
                try { dependencies = AssetDatabase.GetDependencies(asset, true); }
                catch (Exception ex) { Debug.LogWarning("Dependency scan failed: " + asset + " : " + ex.Message); continue; }
                foreach (var dependency in dependencies)
                    if (externalReferencers.TryGetValue(dependency, out var users) && !string.Equals(asset, dependency, StringComparison.OrdinalIgnoreCase))
                        users.Add(asset);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(reportDir);
        var report = Path.Combine(reportDir, "hdrp_demo_quarantine_preview_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        using (var writer = new StreamWriter(report, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("AssetPath\tClassification\tInBuildDependencies\tExternalReferencerCount\tExternalReferencers\tNotes");
            foreach (var candidate in candidates)
            {
                var refs = externalReferencers[candidate].Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
                bool inBuild = buildDependencies.Contains(candidate);
                string classification = inBuild ? "BLOCK_BUILD" : refs.Length > 0 ? "BLOCK_EXTERNAL" : "REVIEW_ONLY";
                string note = "Preview only; dynamic loads, Addressables, AssetBundles and runtime string references are not proven safe";
                writer.WriteLine(string.Join("\t", new[] {
                    Escape(candidate), classification, inBuild ? "True" : "False", refs.Length.ToString(),
                    Escape(string.Join(" | ", refs)), Escape(note)
                }));
            }
        }
        var blockedBuild = candidates.Count(buildDependencies.Contains);
        var blockedExternal = candidates.Count(p => !buildDependencies.Contains(p) && externalReferencers[p].Count > 0);
        Debug.Log($"HDRP/Demo quarantine preview: {candidates.Length} candidates; {blockedBuild} build-blocked; {blockedExternal} external-blocked. No files moved or deleted. Report: {report}");
        EditorUtility.RevealInFinder(report);
    }

    private static string Escape(string value) => (value ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
#endif
