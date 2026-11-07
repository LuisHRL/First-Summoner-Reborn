// Unity 6 Editor — targeted scene and prefab reference migration (v14 cumulative isolated scene resave audit).
// Keep the file named SceneDependencyMigrator.cs in Assets/Editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneDependencyMigrator : EditorWindow
{
    [SerializeField] private DefaultAsset sourceFolder;
    [SerializeField] private DefaultAsset targetFolder;
    [SerializeField] private SceneAsset sceneAsset;
    [SerializeField] private bool includeReferencedPrefabs = true;
    [SerializeField] private bool includeDependentAssets = true;
    private string lastReport = "";

    [MenuItem("Tools/Asset Dependency Manager/Scene reference migrator")]
    public static void Open() { GetWindow<SceneDependencyMigrator>("Scene reference migrator"); }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Scans nested serialized references, reachable destination prefabs, materials, TerrainData and TerrainLayers. Attributes unresolved dependencies to their direct owners. Replaces only byte-identical, uniquely matched subassets with matching fileIDs. Reports imported FBX prefab-instance links separately (no unsafe automatic relinking). Preview before Apply; backups are created. Does not delete assets.", MessageType.Info);
        sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Original folder", sourceFolder, typeof(DefaultAsset), false);
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Destination folder", targetFolder, typeof(DefaultAsset), false);
        sceneAsset = (SceneAsset)EditorGUILayout.ObjectField("Scene", sceneAsset, typeof(SceneAsset), false);
        includeReferencedPrefabs = EditorGUILayout.Toggle("Include scene prefab assets", includeReferencedPrefabs);
        includeDependentAssets = EditorGUILayout.Toggle("Include reachable Environment assets", includeDependentAssets);
        if (GUILayout.Button("1. Preview scene + prefabs")) Run(false);
        if (GUILayout.Button("2. Apply verified changes (backups)")) Run(true);
        if (GUILayout.Button("3. Trace remaining FBX owners (read-only)")) TraceFbxOwners();
        if (GUILayout.Button("4. Trace FBX dependency chains (read-only)")) TraceFbxDependencyChains();
        if (GUILayout.Button("5. Inspect scene FBX identifiers (read-only)")) InspectSceneFbxIdentifiers();
        if (GUILayout.Button("6. Audit loaded scene dependencies (read-only)")) AuditLoadedSceneDependencies();
        if (GUILayout.Button("7. Test isolated scene resave (temporary copy)")) TestIsolatedSceneResave();
        if (GUILayout.Button("8. Run isolated FBX removal test (automated)")) RunIsolatedFbxRemovalTest();
        if (GUILayout.Button("9. Baseline isolated comparison (no FBX removed)")) RunIsolatedFbxRemovalTest(true);
        if (GUILayout.Button("10. Audit all project dependencies (read-only)")) AuditAllProjectDependencies();
        if (GUILayout.Button("11. Audit global replacement matches (read-only)")) AuditGlobalReplacementMatches();
        if (GUILayout.Button("12. Diagnose unmatched source assets (read-only)")) DiagnoseUnmatchedSourceAssets();
        if (!string.IsNullOrEmpty(lastReport)) EditorGUILayout.SelectableLabel(lastReport, EditorStyles.wordWrappedLabel, GUILayout.Height(48));
    }

    // Global dependency audit: scans every AssetDatabase file under Assets, not only the selected scene.
    // The source folder is selected in the UI; no project-specific asset paths or GUIDs.
    private void AuditAllProjectDependencies()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        if (!AssetDatabase.IsValidFolder(source) || source == "Assets" || !Under(source, "Assets"))
        {
            EditorUtility.DisplayDialog("Invalid source folder",
                "Select a specific source folder inside Assets (not the Assets root).", "OK");
            return;
        }

        string reportDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "AssetDependencyManagerReports");
        Directory.CreateDirectory(reportDir);
        string reportPath = Path.Combine(reportDir,
            "global_dependency_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");

        var rows = new List<string> {
            Row("TYPE", "OWNER", "DEPENDENCY", "DETAIL")
        };
        string[] allPaths = AssetDatabase.GetAllAssetPaths()
            .Select(Normalize)
            .Where(p => Under(p, "Assets") && !AssetDatabase.IsValidFolder(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        var reverse = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var directOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int errors = 0, directEdges = 0, inspected = 0;
        try
        {
            for (int i = 0; i < allPaths.Length; i++)
            {
                string owner = allPaths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Global dependency audit (read-only)",
                    owner, allPaths.Length == 0 ? 1f : (float)i / allPaths.Length))
                {
                    rows.Add(Row("STATUS", "", "", "CANCELLED_PARTIAL_REPORT"));
                    break;
                }
                if (Under(owner, source)) sourceAssets.Add(owner);
                try
                {
                    // false = direct dependencies; ignore self-edges.
                    foreach (string raw in AssetDatabase.GetDependencies(owner, false))
                    {
                        string dep = Normalize(raw);
                        if (string.Equals(owner, dep, StringComparison.OrdinalIgnoreCase)) continue;
                        HashSet<string> parents;
                        if (!reverse.TryGetValue(dep, out parents))
                        {
                            parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            reverse.Add(dep, parents);
                        }
                        parents.Add(owner);
                        if (!Under(owner, source) && Under(dep, source))
                        {
                            directOwners.Add(owner);
                            directEdges++;
                            rows.Add(Row("DIRECT", owner, dep, "AssetDatabase.GetDependencies(false)"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    rows.Add(Row("ERROR", owner, "", ex.GetType().Name + ": " + ex.Message));
                }
                inspected++;
            }
            // Traverse the reverse direct-dependency graph: a project asset that reaches
            // any source asset is an indirect owner, even when the chain is many levels deep.
            var queue = new Queue<string>(sourceAssets);
            var reachable = new HashSet<string>(sourceAssets, StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0)
            {
                string dep = queue.Dequeue();
                HashSet<string> parents;
                if (!reverse.TryGetValue(dep, out parents)) continue;
                foreach (string owner in parents)
                    if (reachable.Add(owner)) queue.Enqueue(owner);
            }
            int indirectOwners = 0;
            foreach (string owner in reachable.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (Under(owner, source) || directOwners.Contains(owner)) continue;
                indirectOwners++;
                rows.Add(Row("INDIRECT", owner, source,
                    "Transitive dependency via other assets; inspect direct edges to locate chain"));
            }
            rows.Add(Row("SUMMARY", source, "",
                "inspected=" + inspected + ";sourceAssets=" + sourceAssets.Count +
                ";directOwners=" + directOwners.Count + ";directEdges=" + directEdges +
                ";indirectOwners=" + indirectOwners + ";errors=" + errors));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            File.WriteAllLines(reportPath, rows);
            lastReport = reportPath;
            Debug.Log("[SceneMigrator] Global dependency audit: " + reportPath);
            Repaint();
        }
    }

    // Read-only, project-wide source-to-target matching audit.
    // Byte identity is necessary, not sufficient, for safe serialized subasset replacement.
    private void AuditGlobalReplacementMatches()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) ||
            source == "Assets" || target == "Assets" ||
            !Under(source, "Assets") || !Under(target, "Assets") ||
            Under(source, target) || Under(target, source) ||
            string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Invalid folders",
                "Select two separate, non-overlapping folders inside Assets.", "OK");
            return;
        }
        string reportDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "AssetDependencyManagerReports");
        Directory.CreateDirectory(reportDir);
        string reportPath = Path.Combine(reportDir,
            "global_replacement_matches_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        var rows = new List<string> { Row("TYPE", "SOURCE_OR_OWNER", "TARGET_OR_DEPENDENCY", "DETAIL") };
        int inspected = 0, errors = 0, referencedCount = 0, unique = 0, ambiguous = 0, unmatched = 0;
        bool cancelled = false;
        try
        {
            string[] paths = AssetDatabase.GetAllAssetPaths().Select(Normalize)
                .Where(p => Under(p, "Assets") && !AssetDatabase.IsValidFolder(p))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var referenced = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Length; i++)
            {
                string owner = paths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Global matching audit: dependencies",
                    owner, paths.Length == 0 ? 1f : (float)i / paths.Length))
                { cancelled = true; break; }
                try
                {
                    if (!Under(owner, source))
                        foreach (string depRaw in AssetDatabase.GetDependencies(owner, false))
                        {
                            string dep = Normalize(depRaw);
                            if (!Under(dep, source)) continue;
                            HashSet<string> owners;
                            if (!referenced.TryGetValue(dep, out owners))
                            {
                                owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                referenced.Add(dep, owners);
                            }
                            owners.Add(owner);
                        }
                }
                catch (Exception ex)
                {
                    errors++;
                    rows.Add(Row("ERROR", owner, "", ex.GetType().Name + ": " + ex.Message));
                }
                inspected++;
            }
            if (!cancelled)
            {
                // Index candidate files by size before computing cryptographic hashes.
                var targetsBySize = new Dictionary<long, List<string>>();
                foreach (string path in paths.Where(p => Under(p, target)))
                {
                    string full = Path.GetFullPath(path);
                    if (!File.Exists(full)) continue;
                    long size = new FileInfo(full).Length;
                    List<string> bucket;
                    if (!targetsBySize.TryGetValue(size, out bucket))
                    {
                        bucket = new List<string>();
                        targetsBySize.Add(size, bucket);
                    }
                    bucket.Add(path);
                }
                var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Func<string, string> hash = path =>
                {
                    string value;
                    if (!hashes.TryGetValue(path, out value))
                    {
                        value = Sha(path);
                        hashes[path] = value;
                    }
                    return value;
                };
                string[] sources = referenced.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                for (int i = 0; i < sources.Length; i++)
                {
                    string dep = sources[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Global matching audit: byte matches",
                        dep, sources.Length == 0 ? 1f : (float)i / sources.Length))
                    { cancelled = true; break; }
                    try
                    {
                        string full = Path.GetFullPath(dep);
                        if (!File.Exists(full))
                        {
                            rows.Add(Row("MISSING_SOURCE_FILE", dep, "", "Referenced but file absent"));
                            unmatched++;
                            continue;
                        }
                        List<string> candidates;
                        if (!targetsBySize.TryGetValue(new FileInfo(full).Length, out candidates))
                            candidates = new List<string>();
                        string oldHash = hash(dep);
                        var matches = new List<string>();
                        foreach (string candidate in candidates)
                            if (hash(candidate) == oldHash) matches.Add(candidate);
                        string status = matches.Count == 1 ? "UNIQUE_BYTE_MATCH" :
                            matches.Count > 1 ? "AMBIGUOUS_BYTE_MATCH" : "NO_BYTE_MATCH";
                        if (matches.Count == 1) unique++;
                        else if (matches.Count > 1) ambiguous++;
                        else unmatched++;
                        referencedCount++;
                        rows.Add(Row("MATCH", dep, string.Join(" | ", matches.ToArray()),
                            status + ";owners=" + referenced[dep].Count +
                            ";NOTE=byte identity does not verify Unity GUID/fileID compatibility"));
                        foreach (string owner in referenced[dep].OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            rows.Add(Row("DIRECT_OWNER", owner, dep, status));
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        rows.Add(Row("ERROR", dep, "", ex.GetType().Name + ": " + ex.Message));
                    }
                }
            }
            rows.Add(Row("SUMMARY", source, target,
                "inspected=" + inspected + ";referencedSourceAssets=" + referencedCount +
                ";uniqueByteMatches=" + unique + ";ambiguousByteMatches=" + ambiguous +
                ";noByteMatches=" + unmatched + ";errors=" + errors +
                ";status=" + (cancelled ? "CANCELLED_PARTIAL" : "COMPLETE")));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            File.WriteAllLines(reportPath, rows);
            lastReport = reportPath;
            Debug.Log("[SceneMigrator] Global replacement match audit: " + reportPath);
            Repaint();
        }
    }

    // Diagnoses referenced source assets without a byte-identical target.
    // Uses selected folders and AssetDatabase, not fixed asset names or GUIDs.
    private void DiagnoseUnmatchedSourceAssets()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) ||
            source == "Assets" || target == "Assets" || !Under(source, "Assets") ||
            !Under(target, "Assets") || Under(source, target) || Under(target, source) ||
            string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Invalid folders", "Select two separate, non-overlapping folders inside Assets.", "OK");
            return;
        }
        string reportDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "AssetDependencyManagerReports");
        Directory.CreateDirectory(reportDir);
        string reportPath = Path.Combine(reportDir,
            "unmatched_source_diagnostics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        var rows = new List<string> { Row("TYPE", "SOURCE_OR_OWNER", "CANDIDATE", "DETAIL") };
        int inspected = 0, sourceReferenced = 0, unmatched = 0, missing = 0;
        int sameNameDifferent = 0, noNameCandidate = 0, errors = 0;
        bool cancelled = false;
        try
        {
            string[] paths = AssetDatabase.GetAllAssetPaths().Select(Normalize)
                .Where(p => Under(p, "Assets") && !AssetDatabase.IsValidFolder(p))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var referenced = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Length; i++)
            {
                string owner = paths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Unmatched audit: dependencies", owner,
                    paths.Length == 0 ? 1f : (float)i / paths.Length))
                { cancelled = true; break; }
                try
                {
                    if (!Under(owner, source))
                        foreach (string depRaw in AssetDatabase.GetDependencies(owner, false))
                        {
                            string dep = Normalize(depRaw);
                            if (!Under(dep, source)) continue;
                            HashSet<string> owners;
                            if (!referenced.TryGetValue(dep, out owners))
                            {
                                owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                referenced.Add(dep, owners);
                            }
                            owners.Add(owner);
                        }
                }
                catch (Exception ex)
                {
                    errors++;
                    rows.Add(Row("ERROR", owner, "", ex.GetType().Name + ": " + ex.Message));
                }
                inspected++;
            }
            if (!cancelled)
            {
                var targetsBySize = new Dictionary<long, List<string>>();
                var targetsByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (string candidate in paths.Where(p => Under(p, target)))
                {
                    string full = Path.GetFullPath(candidate);
                    if (!File.Exists(full)) continue;
                    long size = new FileInfo(full).Length;
                    List<string> bucket;
                    if (!targetsBySize.TryGetValue(size, out bucket))
                    {
                        bucket = new List<string>(); targetsBySize.Add(size, bucket);
                    }
                    bucket.Add(candidate);
                    string name = Path.GetFileName(candidate);
                    if (!targetsByName.TryGetValue(name, out bucket))
                    {
                        bucket = new List<string>(); targetsByName.Add(name, bucket);
                    }
                    bucket.Add(candidate);
                }
                var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Func<string, string> hash = path =>
                {
                    string result;
                    if (!hashes.TryGetValue(path, out result))
                    {
                        result = Sha(path); hashes[path] = result;
                    }
                    return result;
                };
                var sources = referenced.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                for (int i = 0; i < sources.Length; i++)
                {
                    string dep = sources[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Unmatched audit: classify", dep,
                        sources.Length == 0 ? 1f : (float)i / sources.Length))
                    { cancelled = true; break; }
                    try
                    {
                        string full = Path.GetFullPath(dep);
                        if (!File.Exists(full))
                        {
                            missing++;
                            rows.Add(Row("MISSING_SOURCE_FILE", dep, "", "owners=" + referenced[dep].Count));
                            continue;
                        }
                        sourceReferenced++;
                        long size = new FileInfo(full).Length;
                        List<string> sameSize;
                        if (!targetsBySize.TryGetValue(size, out sameSize))
                            sameSize = new List<string>();
                        string sourceHash = hash(dep);
                        // Exclude all byte-identical matches; diagnose only unmatched assets.
                        if (sameSize.Any(candidate => hash(candidate) == sourceHash)) continue;
                        unmatched++;
                        List<string> names;
                        if (!targetsByName.TryGetValue(Path.GetFileName(dep), out names))
                            names = new List<string>();
                        if (names.Count == 0)
                        {
                            noNameCandidate++;
                            rows.Add(Row("NO_SAME_NAME_CANDIDATE", dep, "",
                                "owners=" + referenced[dep].Count + ";no byte-identical copy in destination"));
                        }
                        else
                        {
                            sameNameDifferent++;
                            foreach (string candidate in names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                long targetSize = new FileInfo(Path.GetFullPath(candidate)).Length;
                                rows.Add(Row("SAME_NAME_DIFFERENT_CONTENT", dep, candidate,
                                    "sourceBytes=" + size + ";targetBytes=" + targetSize +
                                    ";owners=" + referenced[dep].Count +
                                    ";sameSizeDifferentHash=" + (size == targetSize)));
                            }
                        }
                        foreach (string owner in referenced[dep].OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            rows.Add(Row("DIRECT_OWNER", owner, dep, "UNMATCHED_SOURCE"));
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        rows.Add(Row("ERROR", dep, "", ex.GetType().Name + ": " + ex.Message));
                    }
                }
            }
            rows.Add(Row("SUMMARY", source, target,
                "inspected=" + inspected + ";referencedSourceFiles=" + sourceReferenced +
                ";unmatched=" + unmatched + ";sameNameDifferent=" + sameNameDifferent +
                ";noSameNameCandidate=" + noNameCandidate + ";missingSourceFiles=" + missing +
                ";errors=" + errors + ";status=" + (cancelled ? "CANCELLED_PARTIAL" : "COMPLETE")));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            File.WriteAllLines(reportPath, rows);
            lastReport = reportPath;
            Debug.Log("[SceneMigrator] Unmatched source diagnostics: " + reportPath);
            Repaint();
        }
    }

    private static string Normalize(string p) { return (p ?? "").Replace('\\', '/').TrimEnd('/'); }
    private static bool Under(string path, string root) { return !string.IsNullOrEmpty(path) && path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase); }
    private static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '); }
    private static string Row(params string[] values) { return string.Join("\t", values.Select(Clean).ToArray()); }
    private static string Sha(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(Path.GetFullPath(path))) return BitConverter.ToString(sha.ComputeHash(stream));
    }

    private static IEnumerable<string> Candidates(string oldPath, string source, string target)
    {
        var pieces = oldPath.Substring(source.Length).TrimStart('/').Split('/');
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pieces.Length; i++)
        {
            string p = target + "/" + string.Join("/", pieces.Skip(i).ToArray());
            if (File.Exists(Path.GetFullPath(p)) && !string.Equals(p, oldPath, StringComparison.OrdinalIgnoreCase)) found.Add(p);
        }
        return found.OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class MatchContext
    {
        public string Source, Target;
        public readonly Dictionary<UnityEngine.Object, UnityEngine.Object> Cache = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        public readonly Dictionary<UnityEngine.Object, string> Failures = new Dictionary<UnityEngine.Object, string>();
        public readonly Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string HashOf(string path)
        {
            string h;
            if (!Hashes.TryGetValue(path, out h)) { h = Sha(path); Hashes[path] = h; }
            return h;
        }
        public UnityEngine.Object Find(UnityEngine.Object oldObj, out string reason)
        {
            reason = "";
            UnityEngine.Object cached;
            if (Cache.TryGetValue(oldObj, out cached)) return cached;
            if (Failures.TryGetValue(oldObj, out reason)) return null;
            string oldPath = AssetDatabase.GetAssetPath(oldObj);
            string oldGuid;
            long oldId;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(oldObj, out oldGuid, out oldId))
            { reason = "NO_SOURCE_LOCAL_ID"; Failures[oldObj] = reason; return null; }
            var oldFile = new FileInfo(Path.GetFullPath(oldPath));
            if (!oldFile.Exists) { reason = "SOURCE_FILE_MISSING"; Failures[oldObj] = reason; return null; }
            var matches = new List<UnityEngine.Object>();
            int paths = 0, byteMatches = 0, idMatches = 0;
            foreach (string p in Candidates(oldPath, Source, Target))
            {
                paths++;
                var file = new FileInfo(Path.GetFullPath(p));
                if (!file.Exists || file.Length != oldFile.Length || HashOf(p) != HashOf(oldPath)) continue;
                byteMatches++;
                foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    if (candidate == null || candidate.GetType() != oldObj.GetType() || candidate.name != oldObj.name) continue;
                    string guid;
                    long id;
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out guid, out id) || id != oldId) continue;
                    idMatches++;
                    Mesh om = oldObj as Mesh;
                    if (om != null)
                    {
                        Mesh nm = candidate as Mesh;
                        if (nm == null || om.vertexCount != nm.vertexCount || om.subMeshCount != nm.subMeshCount || om.blendShapeCount != nm.blendShapeCount) continue;
                    }
                    matches.Add(candidate);
                }
            }
            if (matches.Count == 1) { Cache[oldObj] = matches[0]; return matches[0]; }
            reason = matches.Count > 1 ? "AMBIGUOUS_MATCH" : paths == 0 ? "NO_DESTINATION_PATH" : byteMatches == 0 ? "CONTENT_DIFFERS" : idMatches == 0 ? "NO_MATCHING_SUBASSET_ID" : "MESH_METADATA_DIFFERS";
            Failures[oldObj] = reason;
            return null;
        }
    }

    private static IEnumerable<UnityEngine.Object> SceneObjects(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                yield return t.gameObject;
                foreach (var c in t.GetComponents<Component>()) if (c != null) yield return c;
            }
    }

    private static int ScanObject(UnityEngine.Object obj, string owner, MatchContext context, bool apply, List<string> report)
    {
        int changes = 0;
        SerializedObject so;
        try { so = new SerializedObject(obj); } catch (Exception ex) { report.Add(Row(owner, obj.name, "", "", "", "SERIALIZE_ERROR:" + ex.GetType().Name)); return 0; }
        var prop = so.GetIterator();
        if (!prop.NextVisible(true)) return 0;
        do
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            var oldObj = prop.objectReferenceValue;
            if (oldObj == null) continue;
            string oldPath = AssetDatabase.GetAssetPath(oldObj);
            if (!Under(oldPath, context.Source)) continue;
            string reason;
            var replacement = context.Find(oldObj, out reason);
            if (replacement == null)
            { report.Add(Row(owner, obj.name, prop.propertyPath, oldPath, "", "BLOCKED_" + reason)); continue; }
            report.Add(Row(owner, obj.name, prop.propertyPath, oldPath, AssetDatabase.GetAssetPath(replacement), apply ? "APPLIED" : "WOULD_APPLY"));
            changes++;
            if (apply) prop.objectReferenceValue = replacement;
        } while (prop.NextVisible(true));
        if (apply && changes > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(obj);
        }
        return changes;
    }

    private static string GetHierarchyPath(Transform t)
    {
        var names = new List<string>();
        while (t != null) { names.Add(t.name); t = t.parent; }
        names.Reverse();
        return string.Join("/", names.ToArray());
    }


    // Read-only investigation of imported-model links that are not represented
    // by ordinary ObjectReference properties in Unity's serialized scene.
    private void TraceFbxOwners()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string scenePath = Normalize(AssetDatabase.GetAssetPath(sceneAsset));
        if (!AssetDatabase.IsValidFolder(source) || string.IsNullOrEmpty(scenePath) ||
            !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Invalid selection", "Select the original folder and GameScene.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var remaining = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, true)
            .Where(p => Under(p, source) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(dir);
        string output = Path.Combine(dir, "fbx_owner_trace_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        var rows = new List<string> { "Kind\tHierarchy\tComponentOrProperty\tSourceFBX\tDetail" };
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in SceneObjects(scene))
        {
            var go = obj as GameObject;
            var component = obj as Component;
            Transform transform = go != null ? go.transform : component != null ? component.transform : null;
            if (transform == null) continue;
            string hierarchy = GetHierarchyPath(transform);
            // Check all prefab correspondences, not only instance roots. Imported
            // FBX model children and nested prefab objects may carry the link.
            var corresponding = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            if (corresponding != null)
            {
                string path = AssetDatabase.GetAssetPath(corresponding);
                if (remaining.Contains(path))
                {
                    rows.Add(Row("PREFAB_SOURCE_OBJECT", hierarchy, obj.GetType().Name + ":" + obj.name,
                        path, PrefabUtility.GetPrefabInstanceStatus(obj).ToString()));
                    found.Add(path);
                }
            }
            // Also capture ordinary serialized references for completeness.
            try
            {
                var so = new SerializedObject(obj);
                var prop = so.GetIterator();
                if (prop.Next(true)) do
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var reference = prop.objectReferenceValue;
                    if (reference == null) continue;
                    string path = AssetDatabase.GetAssetPath(reference);
                    if (!remaining.Contains(path)) continue;
                    rows.Add(Row("SERIALIZED_REFERENCE", hierarchy, obj.GetType().Name + "." + prop.propertyPath,
                        path, reference.name));
                    found.Add(path);
                } while (prop.NextVisible(true));
            }
            catch (Exception ex)
            {
                rows.Add(Row("INSPECTION_ERROR", hierarchy, obj.GetType().Name, "", ex.GetType().Name));
            }
        }
        // CollectDependencies can reveal non-SerializedProperty links, but cannot
        // necessarily attribute them to a specific nested object.
        foreach (var root in scene.GetRootGameObjects())
        {
            try
            {
                var objects = root.GetComponentsInChildren<Component>(true)
                    .Where(c => c != null).Cast<UnityEngine.Object>()
                    .Concat(new UnityEngine.Object[] { root }).ToArray();
                foreach (var dependency in EditorUtility.CollectDependencies(objects))
                {
                    if (dependency == null) continue;
                    string path = AssetDatabase.GetAssetPath(dependency);
                    if (!remaining.Contains(path)) continue;
                    rows.Add(Row("ROOT_DEPENDENCY", GetHierarchyPath(root.transform), "CollectDependencies",
                        path, dependency.GetType().Name + ":" + dependency.name));
                    found.Add(path);
                }
            }
            catch (Exception ex)
            {
                rows.Add(Row("ROOT_INSPECTION_ERROR", root.name, "CollectDependencies", "", ex.GetType().Name));
            }
        }
        foreach (string path in remaining.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            rows.Add(Row("SUMMARY", "", "", path, found.Contains(path) ? "OWNER_FOUND" : "OWNER_NOT_EXPOSED_BY_UNITY_APIS"));
        File.WriteAllLines(output, rows);
        lastReport = output;
        Debug.Log("[SceneMigrator] FBX owner trace: " + remaining.Count + " source FBX, " + found.Count +
            " attributed; read-only report: " + output);
        EditorUtility.RevealInFinder(output);
    }

    // v10: Find shortest AssetDatabase dependency paths from the scene to source FBX.
    // No scene objects or project assets are modified by this diagnostic.
    private void TraceFbxDependencyChains()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        string scenePath = Normalize(AssetDatabase.GetAssetPath(sceneAsset));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) ||
            string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ||
            Under(source, target) || Under(target, source) ||
            !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Invalid selection", "Select separate source/destination folders and a scene.", "OK");
            return;
        }

        string[] closure;
        try { closure = AssetDatabase.GetDependencies(scenePath, true); }
        catch (Exception ex) { Debug.LogException(ex); return; }
        var sourceFbx = new HashSet<string>(closure.Where(p => Under(p, source) &&
            p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { scenePath };
        var queue = new Queue<string>();
        var rows = new List<string> { "Kind\tDepth\tOwner\tDependency\tDetail" };
        queue.Enqueue(scenePath);
        while (queue.Count > 0)
        {
            string owner = queue.Dequeue();
            string[] direct;
            try { direct = AssetDatabase.GetDependencies(owner, false); }
            catch (Exception ex)
            {
                rows.Add(Row("ERROR", "", owner, "", ex.GetType().Name + ":" + ex.Message));
                continue;
            }
            foreach (string dep in direct.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(owner, dep, StringComparison.OrdinalIgnoreCase)) continue;
                if (sourceFbx.Contains(dep)) rows.Add(Row("DIRECT_FBX_EDGE", "", owner, dep,
                    Under(owner, source) ? "SOURCE_OWNER" : Under(owner, target) ? "DESTINATION_OWNER" : "OTHER_OWNER"));
                if (visited.Add(dep)) { parent[dep] = owner; queue.Enqueue(dep); }
            }
        }
        foreach (string fbx in sourceFbx.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var chain = new List<string> { fbx };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fbx };
            string cursor = fbx;
            while (parent.ContainsKey(cursor) && seen.Add(parent[cursor]))
            {
                cursor = parent[cursor];
                chain.Add(cursor);
            }
            chain.Reverse();
            rows.Add(Row("SHORTEST_CHAIN", (chain.Count - 1).ToString(), scenePath, fbx,
                chain.Count > 1 ? string.Join(" -> ", chain.ToArray()) : "NO_CHAIN_EXPOSED"));
        }
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(dir);
        string output = Path.Combine(dir, "fbx_dependency_chains_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        File.WriteAllLines(output, rows);
        lastReport = output;
        Debug.Log("[SceneMigrator] v10 read-only FBX dependency chains: " + sourceFbx.Count +
            " source FBX; " + visited.Count + " dependency nodes. Report: " + output);
        EditorUtility.RevealInFinder(output);
    }

    // v11: Read-only evidence for direct dependencies not visible as ObjectReference.
    // Unity binary serialization is not decoded here; byte hits are diagnostic clues,
    // NOT proof of a specific prefab link or permission to rewrite the scene.
    private void InspectSceneFbxIdentifiers()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        string scenePath = Normalize(AssetDatabase.GetAssetPath(sceneAsset));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) ||
            string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ||
            Under(source, target) || Under(target, source) ||
            !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.GetFullPath(scenePath)))
        {
            EditorUtility.DisplayDialog("Invalid selection", "Select separate source/destination folders and an existing scene.", "OK");
            return;
        }
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(dir);
        string output = Path.Combine(dir, "fbx_identifier_inspection_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        var rows = new List<string> { "Kind\tSourceFBX\tSourceGUID\tTargetFBX\tTargetGUID\tDetail" };
        try
        {
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(scenePath));
            bool textScene = bytes.Length >= 5 && bytes[0] == (byte)'%' && bytes[1] == (byte)'Y';
            rows.Add(Row("SCENE_FORMAT", scenePath, "", "", "",
                (textScene ? "TEXT_YAML" : "BINARY_OR_UNKNOWN") + ";bytes=" + bytes.Length));
            var dependencies = AssetDatabase.GetDependencies(scenePath, true)
                .Where(p => Under(p, source) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (string oldPath in dependencies)
            {
                string oldGuid = AssetDatabase.AssetPathToGUID(oldPath);
                var candidates = Candidates(oldPath, source, target).ToArray();
                string sourceHash = "";
                try { sourceHash = Sha(oldPath); }
                catch (Exception ex) { rows.Add(Row("SOURCE_READ_ERROR", oldPath, oldGuid, "", "", ex.Message)); }
                if (candidates.Length == 0)
                    rows.Add(Row("NO_TARGET_CANDIDATE", oldPath, oldGuid, "", "", ""));
                foreach (string candidate in candidates)
                {
                    string candidateGuid = AssetDatabase.AssetPathToGUID(candidate);
                    string match = "UNVERIFIED";
                    try { match = Sha(candidate) == sourceHash ? "BYTE_IDENTICAL" : "CONTENT_DIFFERS"; }
                    catch (Exception ex) { match = "HASH_ERROR:" + ex.GetType().Name; }
                    rows.Add(Row("CANDIDATE", oldPath, oldGuid, candidate, candidateGuid, match));
                }
                if (string.IsNullOrEmpty(oldGuid) || oldGuid.Length != 32)
                {
                    rows.Add(Row("INVALID_GUID", oldPath, oldGuid, "", "", ""));
                    continue;
                }
                byte[] ascii = System.Text.Encoding.ASCII.GetBytes(oldGuid);
                byte[] raw = new byte[16];
                bool valid = true;
                for (int i = 0; i < 16; i++)
                {
                    byte value;
                    if (!byte.TryParse(oldGuid.Substring(i * 2, 2),
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out value)) { valid = false; break; }
                    raw[i] = value;
                }
                if (!valid) continue;
                byte[] reversed = (byte[])raw.Clone();
                Array.Reverse(reversed);
                int asciiHits = CountSequence(bytes, ascii);
                int rawHits = CountSequence(bytes, raw);
                int reversedHits = CountSequence(bytes, reversed);
                rows.Add(Row("GUID_BYTE_OCCURRENCES", oldPath, oldGuid, "", "",
                    "ascii=" + asciiHits + ";raw=" + rawHits + ";reversed=" + reversedHits +
                    ";interpretation=HEURISTIC_ONLY"));
            }
        }
        catch (Exception ex)
        {
            rows.Add(Row("ERROR", scenePath, "", "", "", ex.GetType().Name + ":" + ex.Message));
            Debug.LogException(ex);
        }
        File.WriteAllLines(output, rows);
        lastReport = output;
        Debug.Log("[SceneMigrator] v11 read-only identifier inspection: " + output);
        EditorUtility.RevealInFinder(output);
    }

    private static int CountSequence(byte[] haystack, byte[] needle)
    {
        if (needle == null || needle.Length == 0 || haystack.Length < needle.Length) return 0;
        int count = 0;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i] != needle[0]) continue;
            int j = 1;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) count++;
        }
        return count;
    }

    // v12: Audit references in the loaded scene without editing or saving it.
    // A missing owner is NOT evidence that a dependency is safe to delete.
    private void AuditLoadedSceneDependencies()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        string scenePath = Normalize(AssetDatabase.GetAssetPath(sceneAsset));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) ||
            string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ||
            Under(source, target) || Under(target, source) ||
            string.IsNullOrEmpty(scenePath) ||
            !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Invalid selection", "Select separate source/destination folders and a scene.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene;
        try { scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
        catch (Exception ex) { Debug.LogException(ex); return; }

        var remaining = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, true)
            .Where(p => Under(p, source) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<string> { "Kind\tHierarchy\tObject\tProperty\tSourceFBX\tDetail" };
        int objectsScanned = 0, referencesScanned = 0, errors = 0;
        foreach (var obj in SceneObjects(scene))
        {
            if (obj == null) continue;
            objectsScanned++;
            var go = obj as GameObject;
            var component = obj as Component;
            Transform tr = go != null ? go.transform : component != null ? component.transform : null;
            string hierarchy = tr != null ? GetHierarchyPath(tr) : "";
            string objectType = obj.GetType().Name;
            try
            {
                // Prefab sources are structural relationships, not ordinary serialized references.
                var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(obj);
                if (prefabSource != null)
                {
                    string path = AssetDatabase.GetAssetPath(prefabSource);
                    if (remaining.Contains(path))
                    {
                        rows.Add(Row("PREFAB_SOURCE", hierarchy, objectType, "CorrespondingObjectFromSource", path,
                            prefabSource.name));
                        found.Add(path);
                    }
                }
                // Explicit runtime component properties help where SerializedObject is opaque.
                var mf = obj as MeshFilter;
                if (mf != null) RecordLoadedFbx(mf.sharedMesh, remaining, found, rows, hierarchy, objectType, "sharedMesh");
                var smr = obj as SkinnedMeshRenderer;
                if (smr != null) RecordLoadedFbx(smr.sharedMesh, remaining, found, rows, hierarchy, objectType, "sharedMesh");
                var renderer = obj as Renderer;
                if (renderer != null)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        RecordLoadedFbx(materials[i], remaining, found, rows, hierarchy, objectType, "sharedMaterials[" + i + "]");
                }
                var collider = obj as MeshCollider;
                if (collider != null) RecordLoadedFbx(collider.sharedMesh, remaining, found, rows, hierarchy, objectType, "sharedMesh");
                var terrain = obj as Terrain;
                if (terrain != null) RecordLoadedFbx(terrain.terrainData, remaining, found, rows, hierarchy, objectType, "terrainData");
                var so = new SerializedObject(obj);
                var prop = so.GetIterator();
                if (prop.Next(true))
                {
                    do
                    {
                        if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                        referencesScanned++;
                        RecordLoadedFbx(prop.objectReferenceValue, remaining, found, rows,
                            hierarchy, objectType, prop.propertyPath);
                    } while (prop.Next(true));
                }
            }
            catch (Exception ex)
            {
                errors++;
                rows.Add(Row("SCAN_ERROR", hierarchy, objectType, "", "", ex.GetType().Name + ":" + ex.Message));
            }
        }
        foreach (string path in remaining.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(Row("SUMMARY", "", "", "", path,
                found.Contains(path) ? "LOADED_REFERENCE_FOUND" : "NOT_ATTRIBUTED_IN_LOADED_OBJECTS_NOT_PROOF_UNUSED"));
        }
        rows.Add(Row("TOTAL", "", "", "", "", "fbx=" + remaining.Count +
            ";attributed=" + found.Count + ";objects=" + objectsScanned +
            ";serializedReferencesChecked=" + referencesScanned + ";errors=" + errors));
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(dir);
        string output = Path.Combine(dir, "fbx_loaded_scene_audit_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        File.WriteAllLines(output, rows);
        lastReport = output;
        Debug.Log("[SceneMigrator] v12 read-only loaded scene audit: " + output);
        EditorUtility.RevealInFinder(output);
    }

    private static void RecordLoadedFbx(UnityEngine.Object reference, HashSet<string> remaining,
        HashSet<string> found, List<string> rows, string hierarchy, string ownerType, string property)
    {
        if (reference == null) return;
        string path = AssetDatabase.GetAssetPath(reference);
        if (!remaining.Contains(path)) return;
        rows.Add(Row("LOADED_REFERENCE", hierarchy, ownerType, property, path,
            reference.GetType().Name + ":" + reference.name));
        found.Add(path);
    }

    // v13: Save a separate temporary scene copy, reopen and resave ONLY that copy,
    // then compare direct and recursive source dependencies. Never rewrite source GUIDs.
    // The original scene is not saved by this operation.
    private void TestIsolatedSceneResave()
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        string scenePath = Normalize(AssetDatabase.GetAssetPath(sceneAsset));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) ||
            string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ||
            Under(source, target) || Under(target, source) ||
            string.IsNullOrEmpty(scenePath) || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.GetFullPath(scenePath)))
        {
            EditorUtility.DisplayDialog("Invalid selection", "Select separate folders and an existing scene.", "OK");
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Not available", "Exit Play mode before this diagnostic.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(dir);
        string reportPath = Path.Combine(dir, "scene_resave_isolation_" + stamp + ".tsv");
        string tempPath = "Assets/__SceneMigrator_Isolation_" + stamp + ".unity";
        var rows = new List<string> { "Kind\tAsset\tBefore\tAfter\tDetail" };
        bool tempCreated = false;
        string originalHash = "";
        string finalHash = "";
        try
        {
            originalHash = Sha(scenePath);
            var beforeDirect = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, false)
                .Where(p => Under(p, source)), StringComparer.OrdinalIgnoreCase);
            var beforeRecursive = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, true)
                .Where(p => Under(p, source)), StringComparer.OrdinalIgnoreCase);

            // SaveAsCopy preserves the original scene's path and creates a new scene asset.
            Scene original = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(original, tempPath, true))
                throw new IOException("Unity could not save the temporary scene copy.");
            tempCreated = true;
            AssetDatabase.ImportAsset(tempPath, ImportAssetOptions.ForceSynchronousImport);
            var copiedDirect = new HashSet<string>(AssetDatabase.GetDependencies(tempPath, false)
                .Where(p => Under(p, source)), StringComparer.OrdinalIgnoreCase);
            var copiedRecursive = new HashSet<string>(AssetDatabase.GetDependencies(tempPath, true)
                .Where(p => Under(p, source)), StringComparer.OrdinalIgnoreCase);

            // Reopen and save only the temporary copy. Do not save the original.
            Scene temporary = EditorSceneManager.OpenScene(tempPath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(temporary))
                throw new IOException("Unity could not resave the temporary scene.");
            AssetDatabase.ImportAsset(tempPath, ImportAssetOptions.ForceSynchronousImport);
            var afterDirect = new HashSet<string>(AssetDatabase.GetDependencies(tempPath, false)
                .Where(p => Under(p, source)), StringComparer.OrdinalIgnoreCase);
            var afterRecursive = new HashSet<string>(AssetDatabase.GetDependencies(tempPath, true)
                .Where(p => Under(p, source)), StringComparer.OrdinalIgnoreCase);

            foreach (string asset in beforeRecursive.Union(copiedRecursive).Union(afterRecursive)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                rows.Add(Row("RECURSIVE_SOURCE_DEPENDENCY", asset,
                    beforeRecursive.Contains(asset) ? "YES" : "NO",
                    afterRecursive.Contains(asset) ? "YES" : "NO",
                    "copy=" + (copiedRecursive.Contains(asset) ? "YES" : "NO")));
            foreach (string asset in beforeDirect.Union(copiedDirect).Union(afterDirect)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                rows.Add(Row("DIRECT_SOURCE_DEPENDENCY", asset,
                    beforeDirect.Contains(asset) ? "YES" : "NO",
                    afterDirect.Contains(asset) ? "YES" : "NO",
                    "copy=" + (copiedDirect.Contains(asset) ? "YES" : "NO")));
            rows.Add(Row("TOTAL", "direct", beforeDirect.Count.ToString(), afterDirect.Count.ToString(),
                "initialCopy=" + copiedDirect.Count));
            rows.Add(Row("TOTAL", "recursive", beforeRecursive.Count.ToString(), afterRecursive.Count.ToString(),
                "initialCopy=" + copiedRecursive.Count));
        }
        catch (Exception ex)
        {
            rows.Add(Row("ERROR", "", "", "", ex.GetType().Name + ":" + ex.Message));
            Debug.LogException(ex);
        }
        finally
        {
            // Restore the selected original scene, then remove only our temporary asset.
            try { EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
            catch (Exception ex) { rows.Add(Row("RESTORE_ERROR", scenePath, "", "", ex.Message)); Debug.LogException(ex); }
            if (tempCreated || File.Exists(Path.GetFullPath(tempPath)))
            {
                try
                {
                    if (!AssetDatabase.DeleteAsset(tempPath))
                        rows.Add(Row("CLEANUP_ERROR", tempPath, "", "", "Temporary scene could not be deleted"));
                }
                catch (Exception ex) { rows.Add(Row("CLEANUP_ERROR", tempPath, "", "", ex.Message)); }
            }
            try
            {
                finalHash = Sha(scenePath);
                rows.Add(Row("ORIGINAL_SCENE_HASH", scenePath, originalHash, finalHash,
                    originalHash == finalHash ? "UNCHANGED" : "CHANGED_WARNING"));
            }
            catch (Exception ex) { rows.Add(Row("HASH_ERROR", scenePath, "", "", ex.Message)); }
            File.WriteAllLines(reportPath, rows);
            lastReport = reportPath;
            Debug.Log("[SceneMigrator] v13 isolation report: " + reportPath);
            EditorUtility.RevealInFinder(reportPath);
        }
    }

    // v14: A separate Unity batchmode process operates on a standalone project copy.
    // No assets in the original project are removed, renamed or reimported.
    private void RunIsolatedFbxRemovalTest(bool baseline = false)
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        string scenePath = Normalize(AssetDatabase.GetAssetPath(sceneAsset));
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) ||
            source.Equals(target, StringComparison.OrdinalIgnoreCase) || Under(source, target) || Under(target, source) ||
            !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(project, scenePath)) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Invalid test settings", "Select valid separate folders and an existing scene, outside Play mode.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string[] affected = AssetDatabase.GetDependencies(scenePath, true)
            .Where(p => Under(p, source) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToArray();
        if (affected.Length == 0)
        {
            EditorUtility.DisplayDialog("No source FBX", "The selected scene has no FBX dependencies under the original folder.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog(baseline ? "Isolated baseline test" : "Isolated FBX removal test",
            "A standalone project copy will be created outside your Unity project (potentially several GB). " +
            (baseline ? "No FBX will be removed. The worker will compare missing references against the latest isolated removal report. " :
            affected.Length + " FBX will be removed ONLY from that copy. ") +
            "The original project will not be modified. Continue?", "Run test", "Cancel")) return;

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string reportDir = Path.Combine(project, "AssetDependencyManagerReports");
        Directory.CreateDirectory(reportDir);
        string report = Path.Combine(reportDir, (baseline ? "fbx_baseline_isolation_" : "fbx_removal_isolation_") + stamp + ".tsv");
        string comparisonReport = "";
        if (baseline)
        {
            comparisonReport = Directory.GetFiles(reportDir, "fbx_removal_isolation_*_worker.tsv")
                .OrderByDescending(p => File.GetLastWriteTimeUtc(p)).FirstOrDefault();
            if (string.IsNullOrEmpty(comparisonReport))
            {
                EditorUtility.DisplayDialog("Missing comparison report", "Run the isolated removal test first, then retry baseline.", "OK");
                return;
            }
        }
        string sandbox = Path.Combine(Path.GetTempPath(), "UnityFbxIsolation_" + Guid.NewGuid().ToString("N"));
        var rows = new List<string> { "Kind\tAsset\tStatus\tDetail" };
        string originalSceneHash = Sha(Path.Combine(project, scenePath));
        string[] originalHashes = affected.Select(p => Sha(Path.Combine(project, p))).ToArray();
        try
        {
            Directory.CreateDirectory(sandbox);
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string from = Path.Combine(project, folder);
                if (!Directory.Exists(from)) throw new IOException("Missing project folder: " + folder);
                CopyProjectTree(from, Path.Combine(sandbox, folder));
            }
            foreach (string path in affected)
            {
                string file = Path.Combine(sandbox, path);
                if (!File.Exists(file)) throw new IOException("Missing copied FBX: " + path);
                if (!baseline)
                {
                    File.Delete(file);
                    string meta = file + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                    rows.Add(Row("ISOLATED_REMOVAL", path, "REMOVED_FROM_COPY_ONLY", ""));
                }
            }
            string config = Path.Combine(sandbox, "fbx_isolation_config.txt");
            File.WriteAllLines(config, baseline ? new[] { scenePath, source, report, comparisonReport } : new[] { scenePath, source, report });
            string unity = EditorApplication.applicationPath;
            string log = Path.Combine(reportDir, (baseline ? "fbx_baseline_unity_" : "fbx_removal_unity_") + stamp + ".log");
            var start = new ProcessStartInfo(unity)
            {
                Arguments = "-batchmode -nographics -quit -projectPath " + QuoteArg(sandbox) +
                    " -executeMethod SceneDependencyMigrator.RunFbxIsolationWorker" +
                    " -fbxIsolationConfig " + QuoteArg(config) + " -logFile " + QuoteArg(log),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            rows.Add(Row("WORKER", "", "STARTING", log));
            // Do not block the Unity Editor UI; completion is reported by the worker.
            var process = Process.Start(start);
            if (process == null) throw new IOException("Could not start isolated Unity worker");
            rows.Add(Row("WORKER", "", "LAUNCHED", "pid=" + process.Id));
            rows.Add(Row("SANDBOX", sandbox, "PRESERVED_FOR_INSPECTION", "Delete after reviewing the report"));
            rows.Add(Row("ORIGINAL_SCENE", scenePath, Sha(Path.Combine(project, scenePath)) == originalSceneHash ? "UNCHANGED" : "CHANGED_WARNING", ""));
            for (int i = 0; i < affected.Length; i++)
                if (Sha(Path.Combine(project, affected[i])) != originalHashes[i])
                    rows.Add(Row("ORIGINAL_FBX", affected[i], "CHANGED_WARNING", ""));
        }
        catch (Exception ex)
        {
            rows.Add(Row("ERROR", "", "TEST_NOT_COMPLETED", ex.GetType().Name + ":" + ex.Message));
            UnityEngine.Debug.LogException(ex);
        }
        finally
        {
            // The worker writes a separate *_worker.tsv; this launcher report is never overwritten.
            File.WriteAllLines(report, rows);
            lastReport = report;
            EditorUtility.RevealInFinder(report);
        }
    }

    private static string QuoteArg(string s) { return "\"" + s.Replace("\"", "\\\"") + "\""; }

    private static void CopyProjectTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        foreach (string folder in Directory.GetDirectories(source))
        {
            // Unity generated caches and VCS metadata must never be copied.
            string name = Path.GetFileName(folder);
            if (name == ".git" || name == "Library" || name == "Temp" || name == "Obj") continue;
            // Refuse reparse-point / symlink directories to avoid traversing outside the project.
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symlink directory cannot be safely copied: " + folder);
            CopyProjectTree(folder, Path.Combine(destination, name));
        }
    }

    // Executed only by Unity launched against the isolated copied project.
    public static void RunFbxIsolationWorker()
    {
        string[] args = Environment.GetCommandLineArgs();
        int arg = Array.IndexOf(args, "-fbxIsolationConfig");
        if (arg < 0 || arg + 1 >= args.Length) throw new ArgumentException("Missing worker config");
        string[] cfg = File.ReadAllLines(args[arg + 1]);
        if (cfg.Length != 3 && cfg.Length != 4) throw new IOException("Invalid worker config");
        string scenePath = Normalize(cfg[0]);
        string source = Normalize(cfg[1]);
        string report = Path.Combine(Path.GetDirectoryName(cfg[2]), Path.GetFileNameWithoutExtension(cfg[2]) + "_worker.tsv");
        var rows = new List<string> { "Kind\tAsset\tStatus\tDetail" };
        int errors = 0, missing = 0, components = 0;
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded) throw new IOException("Scene did not load");
            rows.Add(Row("SCENE", scenePath, "LOADED", ""));
            foreach (UnityEngine.Object obj in SceneObjects(scene))
            {
                if (obj == null) continue;
                components++;
                var go = obj as GameObject;
                if (go != null)
                {
                    int scripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (scripts > 0) { missing += scripts; rows.Add(Row("MISSING_SCRIPTS", GetHierarchyPath(go.transform), "FOUND", scripts.ToString())); }
                }
                var component = obj as Component;
                if (component != null)
                {
                    var so = new SerializedObject(component);
                    var iterator = so.GetIterator();
                    if (iterator.Next(true)) do
                    {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (!iterator.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)) && iterator.objectReferenceValue == null)
                        {
                            missing++;
                            rows.Add(Row("MISSING_REFERENCE", GetHierarchyPath(component.transform), "FOUND",
                                component.GetType().Name + "." + iterator.propertyPath));
                        }
                    } while (iterator.Next(true));
                }
            }
            foreach (string path in AssetDatabase.GetDependencies(scenePath, true))
                if (Under(path, source) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    rows.Add(Row("DEPENDENCY", path, File.Exists(Path.GetFullPath(path)) ? "PRESENT" : "MISSING", ""));
            rows.Add(Row("SUMMARY", scenePath, "INSPECTED", "objects=" + components + ";errors=" + errors + ";missing=" + missing));
            if (cfg.Length == 4)
            {
                if (!File.Exists(cfg[3])) throw new IOException("Comparison report missing: " + cfg[3]);
                // Multisets preserve duplicate hierarchy paths, unlike simple set comparison.
                var previous = File.ReadAllLines(cfg[3]).Skip(1)
                    .Where(line => line.StartsWith("MISSING_REFERENCE\t", StringComparison.Ordinal) ||
                                   line.StartsWith("MISSING_SCRIPTS\t", StringComparison.Ordinal))
                    .GroupBy(line => line, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
                var current = rows.Skip(1)
                    .Where(line => line.StartsWith("MISSING_REFERENCE\t", StringComparison.Ordinal) ||
                                   line.StartsWith("MISSING_SCRIPTS\t", StringComparison.Ordinal))
                    .GroupBy(line => line, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
                int introduced = 0, baselineOnly = 0;
                foreach (string key in previous.Keys.Union(current.Keys))
                {
                    int before = current.ContainsKey(key) ? current[key] : 0;
                    int after = previous.ContainsKey(key) ? previous[key] : 0;
                    if (after > before)
                    {
                        introduced += after - before;
                        rows.Add(Row("COMPARISON", key, "NEW_AFTER_REMOVAL", (after - before).ToString()));
                    }
                    if (before > after)
                    {
                        baselineOnly += before - after;
                        rows.Add(Row("COMPARISON", key, "BASELINE_ONLY", (before - after).ToString()));
                    }
                }
                rows.Add(Row("COMPARISON_SUMMARY", scenePath,
                    introduced == 0 ? "NO_NEW_MISSING_REFERENCES" : "NEW_MISSING_REFERENCES",
                    "newAfterRemoval=" + introduced + ";baselineOnly=" + baselineOnly +
                    ";baselineMissing=" + missing + ";removedCopyReport=" + cfg[3]));
            }
            rows.Add(Row("LIMITATION", "", "NOT_VISUAL_OR_RUNTIME_PROOF", "No automatic visual equivalence or gameplay guarantee"));
        }
        catch (Exception ex)
        {
            errors++;
            rows.Add(Row("ERROR", scenePath, "FAILED", ex.GetType().Name + ":" + ex.Message));
            UnityEngine.Debug.LogException(ex);
        }
        finally
        {
            File.WriteAllLines(report, rows);
            UnityEngine.Debug.Log("[SceneMigrator] v14 worker report: " + report);
        }
        if (errors != 0) EditorApplication.Exit(1);
    }

    private void Run(bool apply)
    {
        string source = Normalize(AssetDatabase.GetAssetPath(sourceFolder));
        string target = Normalize(AssetDatabase.GetAssetPath(targetFolder));
        string scenePath = Normalize(AssetDatabase.GetAssetPath(sceneAsset));
        if (!AssetDatabase.IsValidFolder(source) || !AssetDatabase.IsValidFolder(target) || source == target ||
            Under(source, target) || Under(target, source) || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        { EditorUtility.DisplayDialog("Invalid selection", "Choose source, target, and a scene.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (apply && !EditorUtility.DisplayDialog("Apply replacements?", "Backups of the scene and reachable destination assets will be created first. Save/commit your current work separately too.", "Apply", "Cancel")) return;

        string reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(reportsDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var report = new List<string> { "Owner\tObject\tProperty\tOldAsset\tNewAsset\tStatus" };
        var ctx = new MatchContext { Source = source, Target = target };
        int sceneChanges = 0, prefabChanges = 0, assetChanges = 0;
        string backupDir = Path.Combine(reportsDir, "scene_migration_backup_" + stamp);
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Identify prefab assets actually instantiated in the scene, not all demo prefabs.
        var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (includeReferencedPrefabs)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (prefabSource == null) continue;
                    string path = AssetDatabase.GetAssetPath(prefabSource);
                    if (Under(path, target) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) prefabPaths.Add(path);
                }
        }
        // Include ALL reachable destination prefabs, not only instantiated prefab roots.
        // Nested prefab references and asset-to-prefab links are otherwise missed.
        if (includeReferencedPrefabs)
            foreach (var path in AssetDatabase.GetDependencies(scenePath, true))
                if (Under(path, target) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    prefabPaths.Add(path);

        // Build the dependency closure from the selected scene. Only edit owners under
        // the destination folder; original pack and unrelated demo assets stay untouched.
        var dependentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var editableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".mat", ".asset", ".controller", ".overridecontroller", ".anim", ".playable", ".mask", ".physicmaterial", ".physicsmaterial2d", ".terrainlayer" };
        if (includeDependentAssets)
            foreach (var path in AssetDatabase.GetDependencies(scenePath, true))
                if (Under(path, target) && editableExtensions.Contains(Path.GetExtension(path)))
                    dependentPaths.Add(path);

        // Back up BEFORE any changes. No file is removed from the project.
        if (apply)
        {
            Directory.CreateDirectory(backupDir);
            File.Copy(Path.GetFullPath(scenePath), Path.Combine(backupDir, Path.GetFileNameWithoutExtension(scenePath) + "_backup.unity"), true);
            foreach (var p in prefabPaths.Concat(dependentPaths))
            {
                string relative = p.Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.Combine(backupDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(Path.GetFullPath(p), destination, true);
            }
        }

        try
        {
            foreach (var obj in SceneObjects(scene)) sceneChanges += ScanObject(obj, scenePath, ctx, apply, report);
            if (apply && sceneChanges > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save scene");
            }

            foreach (var path in prefabPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                // Preview and apply both load prefab contents; do not write on preview.
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    int local = 0;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        local += ScanObject(t.gameObject, path, ctx, apply, report);
                        foreach (var c in t.GetComponents<Component>()) if (c != null) local += ScanObject(c, path, ctx, apply, report);
                    }
                    if (apply && local > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabChanges += local;
                }
                catch (Exception ex) { report.Add(Row(path, "", "", "", "", "ERROR_" + ex.Message)); }
                finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            }
            // Scan native Unity assets as owners too: materials, terrain data,
            // ScriptableObjects and imported subassets. These hold indirect references.
            foreach (var path in dependentPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    int local = 0;
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (asset == null || asset is GameObject || asset is Component) continue;
                        // Importers are editor settings, not serialized runtime references.
                        if (asset is AssetImporter) continue;
                        local += ScanObject(asset, path, ctx, apply, report);
                    }
                    if (apply && local > 0) AssetDatabase.SaveAssets();
                    assetChanges += local;
                }
                catch (Exception ex)
                {
                    report.Add(Row(path, "", "", "", "", "ERROR_" + ex.GetType().Name + ":" + ex.Message));
                }
            }
        }
        catch (Exception ex) { report.Add(Row(scenePath, "", "", "", "", "ERROR_" + ex.Message)); Debug.LogException(ex); }

        // AssetDatabase dependencies include indirect material/texture/shader/terrain references.
        // Report them explicitly: a scene-level SerializedObject scan cannot safely rewrite them.
        if (apply) AssetDatabase.SaveAssets();
        // Refresh only after applying changes; preview must not alter the project.
        if (apply) AssetDatabase.Refresh();
        // Imported FBX prefab-instance links are NOT normal SerializedProperty object
        // references. Identify them explicitly rather than reporting a false zero.
        // Rebuilding a prefab instance automatically could discard overrides and
        // nested prefab connections; no destructive replacement is attempted here.
        var seenPrefabRoots = new HashSet<GameObject>();
        foreach (var obj in SceneObjects(scene))
        {
            var go = obj as GameObject;
            if (go == null || !PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
            if (!seenPrefabRoots.Add(go)) continue;
            var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (sourcePrefab == null) continue;
            string prefabPath = AssetDatabase.GetAssetPath(sourcePrefab);
            if (!Under(prefabPath, source) || !prefabPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            string reason;
            var match = ctx.Find(sourcePrefab, out reason);
            report.Add(Row(scenePath, GetHierarchyPath(go.transform), "PrefabInstanceSource", prefabPath,
                match == null ? "" : AssetDatabase.GetAssetPath(match),
                match == null ? "FBX_PREFAB_LINK_BLOCKED_" + reason : "FBX_PREFAB_LINK_VERIFIED_MANUAL_RELINK_REQUIRED"));
        }

        // Attribute unresolved source dependencies to their direct owners. A source-only
        // owner cannot be edited safely; the scene or a destination asset must first
        // point to a verified replacement for that owner.
        var closure = AssetDatabase.GetDependencies(scenePath, true).Distinct().ToArray();
        var remaining = new HashSet<string>(closure.Where(p => Under(p, source)), StringComparer.OrdinalIgnoreCase);
        var accounted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string owner in closure.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(owner, scenePath, StringComparison.OrdinalIgnoreCase) ||
                Under(owner, target) || Under(owner, source))
            {
                string[] direct;
                try { direct = AssetDatabase.GetDependencies(owner, false); }
                catch (Exception ex) { report.Add(Row(owner, "", "", "", "", "DIRECT_DEPENDENCY_ERROR_" + ex.GetType().Name)); continue; }
                foreach (string dependency in direct.Distinct())
                    if (remaining.Contains(dependency) && !string.Equals(owner, dependency, StringComparison.OrdinalIgnoreCase))
                    {
                        accounted.Add(dependency);
                        string status = Under(owner, source) ? "SOURCE_OWNER_CHAIN" : "UNRESOLVED_DIRECT_REFERENCE";
                        report.Add(Row(owner, "", "", dependency, "", status));
                    }
            }
        }
        foreach (string p in remaining.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            report.Add(Row("DEPENDENCY_AUDIT", "", "", p, "", accounted.Contains(p) ? "STILL_DEPENDS_ON_SOURCE" : "STILL_DEPENDS_ON_SOURCE_OWNER_UNKNOWN"));
        string output = Path.Combine(reportsDir, "scene_migration_" + (apply ? "apply_" : "preview_") + stamp + ".tsv");
        File.WriteAllLines(output, report);
        lastReport = output;
        Debug.Log("[SceneMigrator] " + (apply ? "Applied" : "Previewed") + " scene=" + sceneChanges + ", prefab=" + prefabChanges + ", other assets=" + assetChanges + ", remaining source dependencies=" + remaining.Count + ". Report: " + output + (apply ? " Backups: " + backupDir : ""));
        EditorUtility.RevealInFinder(output);
    }
}
