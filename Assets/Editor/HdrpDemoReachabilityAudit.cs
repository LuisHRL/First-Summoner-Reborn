#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HdrpDemoReachabilityAudit
{
    private static readonly HashSet<string> CandidateSegments =
        new HashSet<string>(new[] { "HDRP", "Demo" }, StringComparer.OrdinalIgnoreCase);

    [MenuItem("Tools/Asset Dependency Manager/Audit HDRP-Demo reachability (read only)")]
    public static void Run()
    {
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p))
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var candidates = new HashSet<string>(all.Where(IsCandidate), StringComparer.Ordinal);
        var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled)
            .Select(s => s.path).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.Ordinal).ToArray();
        var roots = new HashSet<string>(enabledScenes, StringComparer.Ordinal);
        var missingScenes = enabledScenes.Where(s => !File.Exists(s)).ToArray();
        // Resources assets are potential dynamic roots, even when not referenced by a scene.
        foreach (var p in all.Where(IsInResources)) roots.Add(p);
        // AssetBundle names and addressable-style labels indicate possible runtime loading.
        foreach (var p in all)
        {
            var importer = AssetImporter.GetAtPath(p);
            if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName)) roots.Add(p);
            var main = AssetDatabase.LoadMainAssetAtPath(p);
            if (main != null && AssetDatabase.GetLabels(main)
                .Any(l => l.IndexOf("addressable", StringComparison.OrdinalIgnoreCase) >= 0)) roots.Add(p);
        }
        // A conservative reachable set. Dependencies are recursive; this is not a complete
        // representation of code-driven Addressables, string paths or custom asset loading.
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var root in roots)
        {
            try
            {
                reachable.Add(root);
                foreach (var d in AssetDatabase.GetDependencies(root, true)) reachable.Add(d);
            }
            catch (Exception e) { failures.Add(root + " : " + e.Message); }
        }
        var refs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        try
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar(
                    "HDRP/Demo reachability audit", all[i], (float)i / Math.Max(1, all.Length)))
                {
                    Debug.LogWarning("Reachability audit cancelled: no report written.");
                    return;
                }
                var source = all[i];
                try
                {
                    foreach (var d in AssetDatabase.GetDependencies(source, false))
                    {
                        if (d == source || !candidates.Contains(d) || candidates.Contains(source)) continue;
                        if (!refs.TryGetValue(d, out var list)) refs[d] = list = new List<string>();
                        list.Add(source);
                    }
                }
                catch (Exception e) { failures.Add(source + " : " + e.Message); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "hdrp_demo_reachability_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        using (var w = new StreamWriter(output, false, new UTF8Encoding(true)))
        {
            w.WriteLine("Status\tAssetPath\tReachableFromBuildOrDynamicRoots\tDirectExternalReferences\tReachableExternalReferences\tExternalReferencePaths\tGUID");
            foreach (var p in candidates.OrderBy(x => x, StringComparer.Ordinal))
            {
                refs.TryGetValue(p, out var sources);
                sources = sources ?? new List<string>();
                int liveRefs = sources.Count(reachable.Contains);
                string status = failures.Count > 0 ? "INCOMPLETE_AUDIT" :
                    reachable.Contains(p) ? "PROTECT_REACHABLE" :
                    sources.Count > 0 ? "REVIEW_EXTERNAL_UNUSED" : "REVIEW_UNREFERENCED";
                w.WriteLine(string.Join("\t", new[] { status, Clean(p), reachable.Contains(p) ? "True" : "False",
                    sources.Count.ToString(), liveRefs.ToString(), Clean(string.Join(" | ", sources.OrderBy(x => x, StringComparer.Ordinal))),
                    AssetDatabase.AssetPathToGUID(p) }));
            }
        }
        Debug.Log("Reachability audit: " + candidates.Count + " candidates, " + roots.Count +
            " build/dynamic roots, " + failures.Count + " dependency failures, " + missingScenes.Length +
            " missing enabled scenes. Report: " + output +
            ". REVIEW statuses are NOT permission to delete. Addressables and code-driven loading require separate verification.");
        foreach (var s in missingScenes) Debug.LogWarning("Enabled scene missing: " + s);
        foreach (var f in failures.Take(20)) Debug.LogWarning("Audit dependency failure: " + f);
        EditorUtility.RevealInFinder(output);
    }

    private static bool IsCandidate(string path)
    {
        var parts = path.Split('/');
        for (int i = 1; i < parts.Length - 1; i++)
            if (CandidateSegments.Contains(parts[i])) return true;
        return false;
    }

    private static bool IsInResources(string path)
    {
        var parts = path.Split('/');
        for (int i = 1; i < parts.Length - 1; i++)
            if (string.Equals(parts[i], "Resources", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string Clean(string value) => value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
#endif
