#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HdrpDemoDependencyAudit
{
    // Category names, not project-specific asset paths. Extend these if needed.
    private static readonly HashSet<string> CandidateFolderNames =
        new HashSet<string>(new[] { "HDRP", "Demo" }, StringComparer.OrdinalIgnoreCase);

    [MenuItem("Tools/Asset Dependency Manager/Audit HDRP-Demo dependencies (read only)")]
    public static void Run()
    {
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p))
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var candidates = new HashSet<string>(all.Where(IsCandidate), StringComparer.Ordinal);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path).Distinct(StringComparer.Ordinal).ToArray();
        var buildDeps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scene in scenes)
        {
            if (!File.Exists(scene))
            {
                Debug.LogWarning("Build scene missing: " + scene);
                continue;
            }
            foreach (var dep in AssetDatabase.GetDependencies(scene, true)) buildDeps.Add(dep);
        }

        // Reverse edges from ALL assets outside candidate folders, not only build scenes.
        // A direct external edge protects its target; transitive external dependencies
        // are also captured by the recursive walk of the candidate subtree.
        var externalRefs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var externalAssets = all.Where(p => !candidates.Contains(p)).ToArray();
        try
        {
            for (int i = 0; i < externalAssets.Length; i++)
            {
                if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar(
                    "HDRP/Demo dependency audit", "Checking " + externalAssets[i],
                    (float)i / Math.Max(1, externalAssets.Length)))
                {
                    Debug.LogWarning("Audit cancelled. No report written; no assets changed.");
                    return;
                }
                var source = externalAssets[i];
                string[] deps;
                try { deps = AssetDatabase.GetDependencies(source, false); }
                catch (Exception e) { Debug.LogWarning("Dependency read failed: " + source + " - " + e.Message); continue; }
                foreach (var dep in deps)
                {
                    if (dep == source || !candidates.Contains(dep)) continue;
                    if (!externalRefs.TryGetValue(dep, out var refs))
                        externalRefs[dep] = refs = new HashSet<string>(StringComparer.Ordinal);
                    refs.Add(source);
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(reportDir);
        var output = Path.Combine(reportDir, "hdrp_demo_dependencies_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        using (var writer = new StreamWriter(output, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("Status\tAssetPath\tInEnabledBuildSceneDependencies\tExternalDirectReferenceCount\tExternalDirectReferences\tGUID");
            foreach (var path in candidates.OrderBy(p => p, StringComparer.Ordinal))
            {
                externalRefs.TryGetValue(path, out var refs);
                var refCount = refs == null ? 0 : refs.Count;
                var inBuild = buildDeps.Contains(path);
                // REVIEW means only that no direct external reference was found.
                var status = inBuild ? "PROTECT_BUILD" : refCount > 0 ? "PROTECT_EXTERNAL" : "REVIEW_ONLY";
                writer.WriteLine(string.Join("\t", new[] {
                    status, Clean(path), inBuild ? "True" : "False", refCount.ToString(),
                    refs == null ? "" : Clean(string.Join(" | ", refs.OrderBy(x => x, StringComparer.Ordinal))),
                    AssetDatabase.AssetPathToGUID(path)
                }));
            }
        }
        Debug.Log($"HDRP/Demo audit complete. Candidates: {candidates.Count}; enabled scenes: {scenes.Length}; report: {output}. REVIEW_ONLY is NOT authorization to delete.");
        EditorUtility.RevealInFinder(output);
    }

    private static bool IsCandidate(string path)
    {
        var parts = path.Split('/');
        // Only folder segments (not filenames) count.
        for (int i = 1; i < parts.Length - 1; i++)
            if (CandidateFolderNames.Contains(parts[i])) return true;
        return false;
    }

    private static string Clean(string value) => value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
#endif
