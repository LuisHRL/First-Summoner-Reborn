// Asset Dependency Manager - Unity 6 Editor
// Dependency inventory, preview and guarded YAML reference migration.
// Place under any Assets/Editor folder. No project-specific paths are hard-coded.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class AssetDependencyManager : EditorWindow
{
    [Serializable]
    private sealed class Settings
    {
        public string sourceFolder = "";
        public string targetFolder = "";
        public List<string> scenes = new List<string>();
        public bool includeIndirect = true;
        public bool requireIdenticalBytes = true;
    }

    private sealed class Entry
    {
        public string path, category, reason, match, mappingStatus;
        public string evidence = "";
        public long bytes;
    }

    private sealed class Finding
    {
        public string kind, path, detail, severity;
    }

    private readonly List<Finding> findings = new List<Finding>();
    private readonly HashSet<string> dynamicRisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> referencedBy = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex GuidReference = new Regex(@"\bguid:\s*([0-9a-fA-F]{32})\b", RegexOptions.Compiled);
    private static readonly Regex DynamicLoad = new Regex(@"\b(Resources\s*\.\s*Load(?:All|Async)?|Addressables\s*\.\s*Load|AssetBundle\s*\.\s*Load|SceneManager\s*\.\s*LoadScene)\b", RegexOptions.Compiled);
    private static readonly HashSet<string> InspectableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { ".unity", ".prefab", ".mat", ".asset", ".controller", ".overridecontroller", ".anim", ".mask", ".playable", ".lighting", ".terrainlayer", ".spriteatlas" };

    private const string PrefKey = "AssetDependencyManager.Settings.v2";
    private Settings settings = new Settings();
    private readonly List<Entry> inventory = new List<Entry>();
    private readonly HashSet<string> dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> messages = new List<string>();
    private readonly List<string[]> crossReferences = new List<string[]>();
    private Vector2 scroll;
    private int activeTab;
    private static readonly string[] TabNames = { "Configuration", "Dependencies", "Matching", "Diagnostics", "Tests", "Migration", "Reports" };
    private bool analyzed;
    private string reportPath = "";

    [MenuItem("Tools/Asset Dependency Manager/Open")]
    public static void OpenWindow()
    {
        var window = GetWindow<AssetDependencyManager>("Asset Dependency Manager");
        window.minSize = new Vector2(680, 490);
        window.Show();
    }

    private void OnEnable()
    {
        try
        {
            string json = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(json)) settings = JsonUtility.FromJson<Settings>(json) ?? new Settings();
        }
        catch (Exception) { settings = new Settings(); }
        if (settings.scenes == null) settings.scenes = new List<string>();
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(settings));
        analyzed = false;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Asset Dependency Manager", EditorStyles.boldLabel);
        activeTab = GUILayout.Toolbar(activeTab, TabNames);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (activeTab)
        {
            case 0:
                EditorGUILayout.HelpBox("Shared settings for the dependency inventory and migration. Select project folders and scene scope.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        DrawFolder("Original folder (optional)", ref settings.sourceFolder);
        DrawFolder("Copied / inventory folder", ref settings.targetFolder);
        settings.includeIndirect = EditorGUILayout.ToggleLeft("Include indirect dependencies", settings.includeIndirect);
        settings.requireIdenticalBytes = EditorGUILayout.ToggleLeft("Require identical bytes for candidate mappings", settings.requireIdenticalBytes);
        if (EditorGUI.EndChangeCheck()) SaveSettings();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Scenes in analysis scope", EditorStyles.boldLabel);
        for (int i = 0; i < settings.scenes.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            string path = settings.scenes[i];
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            var selected = (SceneAsset)EditorGUILayout.ObjectField(scene, typeof(SceneAsset), false);
            if (selected != scene)
            {
                if (selected == null) settings.scenes.RemoveAt(i--);
                else settings.scenes[i] = AssetDatabase.GetAssetPath(selected);
                SaveSettings();
            }
            else if (GUILayout.Button("Remove", GUILayout.Width(75)))
            {
                settings.scenes.RemoveAt(i--);
                SaveSettings();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add scene slot"))
        {
            settings.scenes.Add("");
            SaveSettings();
        }
        if (GUILayout.Button("Use enabled Build scenes"))
        {
            settings.scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => s.path).Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            SaveSettings();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Scene scope is explicit. Only assets in the selected scenes' dependency graph are eligible for a future migration. Dynamic loads and editor-driven references remain uncertain.", MessageType.Warning);

                break;
            case 1:
                EditorGUILayout.HelpBox("Read-only dependency inventory and global reverse-reference audit.", MessageType.Info);
                if (GUILayout.Button("Analyze dependencies and inventory", GUILayout.Height(30))) Analyze();
                if (GUILayout.Button("Audit all project dependencies (TSV)", GUILayout.Height(30))) AuditGlobalDependencies();
                break;
            case 2:
                EditorGUILayout.HelpBox("Match source and destination assets using the configured folders.", MessageType.Info);
                using (new EditorGUI.DisabledScope(!analyzed))
                    if (GUILayout.Button("Export mapping preview (TSV)", GUILayout.Height(30))) ExportMigrationPreview();
                break;
            case 3:
                EditorGUILayout.HelpBox("Diagnostic functions are being integrated here. No assets are modified by this tab.", MessageType.Info);
                if (GUILayout.Button("Audit all project dependencies (TSV)")) AuditGlobalDependencies();
                if (GUILayout.Button("Audit source-folder reachability and external references (TSV)", GUILayout.Height(30))) AuditSourceReachability();
                if (GUILayout.Button("Audit scene and prefab mesh materials (TSV)", GUILayout.Height(30))) AuditMeshMaterials();
                break;
            case 4:
                EditorGUILayout.HelpBox("Automated isolated tests will be integrated before the legacy test tools are removed. No destructive test is available here yet.", MessageType.Warning);
                break;
            case 5:
                EditorGUILayout.HelpBox("Migration can modify serialized references. Run Analyze and review the dry-run before Apply.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(!analyzed))
                {
                    if (GUILayout.Button("Dry-run safe YAML replacements")) MigrateReferences(false);
                    if (GUILayout.Button("Apply safe YAML replacements (with backups)")) MigrateReferences(true);
                }
                break;
            case 6:
                using (new EditorGUI.DisabledScope(!analyzed))
                    if (GUILayout.Button("Export inventory audit report (TSV)")) ExportReport();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Assets in inventory: " + inventory.Count);
        EditorGUILayout.LabelField("Scene dependencies: " + dependencies.Count);
        foreach (var group in inventory.GroupBy(e => e.category))
            EditorGUILayout.LabelField(group.Key + ": " + group.Count() + " assets / " + FormatBytes(group.Sum(e => e.bytes)));
        if (!string.IsNullOrEmpty(reportPath)) EditorGUILayout.SelectableLabel(reportPath, GUILayout.Height(35));
        EditorGUILayout.LabelField("Diagnostic findings: " + findings.Count);
        EditorGUILayout.LabelField("Assets with explicit scene graph references: " + inventory.Count(e => e.category == "Used by selected scenes"));
        foreach (string message in messages.Take(12)) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
                break;
        }
        EditorGUILayout.EndScrollView();
    }

    // Conservative, read-only audit of any selected source folder; no HDRP/Demo paths baked in.
    // Read-only mesh/material audit across selected scenes and all project prefabs.
    private void AuditMeshMaterials()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[AssetDependencyManager] Exit Play mode before auditing meshes.");
            return;
        }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var rows = new List<string> { "Status\\tAssetPath\\tObjectPath\\tMesh\\tMaterialSlots\\tSubmeshes\\tDetail" };
        var paths = AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        int inspected = 0, issues = 0, failures = 0;
        bool cancelled = false;
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Mesh/material audit", path, (float)i / Math.Max(1, paths.Length)))
                { cancelled = true; break; }
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    GameObject root = null;
                    try
                    {
                        root = PrefabUtility.LoadPrefabContents(path);
                        InspectMeshRenderers(new[] { root }, path, rows, ref inspected, ref issues);
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        rows.Add(string.Join("\\t", new[] { "ERROR", SafeTsv(path), "", "", "", "", SafeTsv(ex.Message) }));
                    }
                    finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
                }
                else
                {
                    UnityEngine.SceneManagement.Scene scene = default(UnityEngine.SceneManagement.Scene);
                    try
                    {
                        scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                        InspectMeshRenderers(scene.GetRootGameObjects(), path, rows, ref inspected, ref issues);
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        rows.Add(string.Join("\\t", new[] { "ERROR", SafeTsv(path), "", "", "", "", SafeTsv(ex.Message) }));
                    }
                    finally
                    {
                        if (scene.IsValid() && scene.isLoaded)
                            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        rows.Add(string.Join("\\t", new[] { cancelled || failures > 0 ? "INCOMPLETE" : "SUMMARY", "", "", "", "", "",
            $"inspected={inspected};issues={issues};failures={failures};cancelled={cancelled}" }));
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(folder);
        var output = Path.Combine(folder, "mesh_material_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        File.WriteAllLines(output, rows, new UTF8Encoding(true));
        reportPath = output;
        Debug.Log("[AssetDependencyManager] Mesh/material audit: " + output);
    }

    private static void InspectMeshRenderers(IEnumerable<GameObject> roots, string assetPath, List<string> rows, ref int inspected, ref int issues)
    {
        foreach (var root in roots)
        {
            if (root == null) continue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                var skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) mesh = skinned.sharedMesh;
                else
                {
                    var meshRenderer = renderer as MeshRenderer;
                    if (meshRenderer == null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                inspected++;
                var hierarchy = new Stack<string>();
                for (var t = renderer.transform; t != null; t = t.parent) hierarchy.Push(t.name);
                string objectPath = SafeTsv(string.Join("/", hierarchy.ToArray()));
                if (mesh == null)
                {
                    issues++;
                    rows.Add(string.Join("\\t", new[] { "MISSING_MESH", SafeTsv(assetPath), objectPath, "", renderer.sharedMaterials.Length.ToString(), "0", "Renderer has no mesh" }));
                    continue;
                }
                int slots = renderer.sharedMaterials.Length;
                int submeshes = mesh.subMeshCount;
                string status = slots > submeshes ? "EXTRA_MATERIAL_SLOTS" : slots < submeshes ? "FEWER_MATERIAL_SLOTS" :
                    renderer.sharedMaterials.Any(m => m == null) ? "NULL_MATERIAL_SLOT" : "";
                if (status.Length == 0) continue;
                issues++;
                rows.Add(string.Join("\\t", new[] { status, SafeTsv(assetPath), objectPath, SafeTsv(mesh.name),
                    slots.ToString(), submeshes.ToString(), "Inspect material assignment" }));
            }
        }
    }

    private void AuditSourceReachability()
    {
        string source = Normalize(settings.sourceFolder);
        if (!AssetDatabase.IsValidFolder(source) || source == "Assets")
        {
            EditorUtility.DisplayDialog("Source folder required", "Select a specific source folder in Configuration.", "OK");
            return;
        }
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(p => Inside(p, "Assets") && !AssetDatabase.IsValidFolder(p))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var candidates = new HashSet<string>(all.Where(p => Inside(p, source)), StringComparer.OrdinalIgnoreCase);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes.Where(x => x.enabled))
        {
            if (string.IsNullOrEmpty(scene.path) || !File.Exists(FullPath(scene.path)))
                errors.Add("Missing build scene: " + scene.path);
            else roots.Add(scene.path);
        }
        foreach (var path in all)
        {
            if (path.Split('/').Any(x => string.Equals(x, "Resources", StringComparison.OrdinalIgnoreCase)))
                roots.Add(path);
            try
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName)) roots.Add(path);
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main != null && AssetDatabase.GetLabels(main).Any(x => x.IndexOf("addressable", StringComparison.OrdinalIgnoreCase) >= 0))
                    roots.Add(path);
            }
            catch (Exception ex) { errors.Add(path + ": " + ex.Message); }
        }
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            try
            {
                reachable.Add(root);
                foreach (var dep in AssetDatabase.GetDependencies(root, true)) reachable.Add(dep);
            }
            catch (Exception ex) { errors.Add(root + ": " + ex.Message); }
        }
        var refs = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        bool cancelled = false;
        try
        {
            for (int i = 0; i < all.Length; i++)
            {
                var owner = all[i];
                if (EditorUtility.DisplayCancelableProgressBar("Source reachability audit", owner, (float)i / Math.Max(1, all.Length)))
                { cancelled = true; break; }
                if (candidates.Contains(owner)) continue;
                try
                {
                    foreach (var dep in AssetDatabase.GetDependencies(owner, false))
                        if (candidates.Contains(dep))
                        {
                            if (!refs.TryGetValue(dep, out var owners)) refs[dep] = owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            owners.Add(owner);
                        }
                }
                catch (Exception ex) { errors.Add(owner + ": " + ex.Message); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        var rows = new List<string> { "Status\\tAssetPath\\tReachable\\tExternalReferences\\tReferencers\\tGUID" };
        foreach (var asset in candidates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            refs.TryGetValue(asset, out var owners);
            bool used = reachable.Contains(asset);
            string status = cancelled || errors.Count > 0 ? "INCOMPLETE_AUDIT" :
                used ? "PROTECT_REACHABLE" : owners != null && owners.Count > 0 ? "REVIEW_EXTERNAL_UNUSED" : "REVIEW_UNREFERENCED";
            rows.Add(string.Join("\\t", new[] { status, SafeTsv(asset), used.ToString(),
                (owners == null ? 0 : owners.Count).ToString(),
                SafeTsv(owners == null ? "" : string.Join(" | ", owners.OrderBy(x => x))),
                AssetDatabase.AssetPathToGUID(asset) }));
        }
        foreach (var error in errors) rows.Add("ERROR\\t" + SafeTsv(error) + "\\t\\t\\t\\t");
        rows.Add("SUMMARY\\t" + SafeTsv(source) + "\\t\\t\\t" + SafeTsv($"roots={roots.Count};errors={errors.Count};cancelled={cancelled};dynamicLoadsNotProven=true") + "\\t");
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, "source_reachability_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        File.WriteAllLines(output, rows, new UTF8Encoding(true));
        reportPath = output;
        Debug.Log("[AssetDependencyManager] Read-only reachability report: " + output + ". REVIEW is never permission to delete.");
    }

    private void AuditGlobalDependencies()
    {
        string source = Normalize(settings.sourceFolder);
        if (!AssetDatabase.IsValidFolder(source) || source == "Assets")
        {
            EditorUtility.DisplayDialog("Source folder required", "Select a specific source folder in Configuration.", "OK");
            return;
        }
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(p => Inside(p, "Assets") && !AssetDatabase.IsValidFolder(p))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var sourceAssets = new HashSet<string>(all.Where(p => Inside(p, source)), StringComparer.OrdinalIgnoreCase);
        var reverse = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<string> { "TYPE\tOWNER\tDEPENDENCY\tDETAIL" };
        var directOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int errors = 0, inspected = 0;
        bool cancelled = false;
        try
        {
            for (int i = 0; i < all.Length; i++)
            {
                string owner = all[i];
                if (EditorUtility.DisplayCancelableProgressBar("Global dependency audit", owner, (float)i / Math.Max(1, all.Length)))
                {
                    cancelled = true;
                    break;
                }
                try
                {
                    foreach (var dep in AssetDatabase.GetDependencies(owner, false))
                    {
                        if (string.Equals(dep, owner, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!reverse.TryGetValue(dep, out var parents)) reverse[dep] = parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        parents.Add(owner);
                        if (!Inside(owner, source) && Inside(dep, source))
                        {
                            directOwners.Add(owner);
                            rows.Add(string.Join("\t", new[] { "DIRECT", SafeTsv(owner), SafeTsv(dep), "AssetDatabase.GetDependencies(false)" }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    rows.Add(string.Join("\t", new[] { "ERROR", SafeTsv(owner), "", SafeTsv(ex.Message) }));
                }
                inspected++;
            }
            if (!cancelled)
            {
                var reachable = new HashSet<string>(sourceAssets, StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<string>(sourceAssets);
                while (queue.Count > 0)
                {
                    if (!reverse.TryGetValue(queue.Dequeue(), out var parents)) continue;
                    foreach (var owner in parents) if (reachable.Add(owner)) queue.Enqueue(owner);
                }
                foreach (var owner in reachable.Where(p => !Inside(p, source) && !directOwners.Contains(p)).OrderBy(p => p))
                    rows.Add(string.Join("\t", new[] { "INDIRECT", SafeTsv(owner), SafeTsv(source), "Transitive reverse dependency" }));
            }
            rows.Add(string.Join("\t", new[] { "SUMMARY", SafeTsv(source), "", $"inspected={inspected};errors={errors};cancelled={cancelled}" }));
        }
        finally { EditorUtility.ClearProgressBar(); }
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, "global_dependency_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        File.WriteAllLines(output, rows, new UTF8Encoding(true));
        reportPath = output;
        Debug.Log("[AssetDependencyManager] Global dependency audit: " + output + (cancelled ? " (partial/cancelled)" : ""));
    }

    private static string Normalize(string path) { return (path ?? "").Replace('\\', '/').TrimEnd('/'); }
    private static bool Inside(string path, string root)
    {
        return !string.IsNullOrEmpty(root) && path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
    private static string FullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }
    private static void DrawFolder(string title, ref string path)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(title);
        EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(path) ? "(not selected)" : path, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        if (GUILayout.Button("Select", GUILayout.Width(70)))
        {
            string selected = EditorUtility.OpenFolderPanel(title, Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected))
            {
                string root = Normalize(Path.GetFullPath(Directory.GetCurrentDirectory()));
                string absolute = Normalize(Path.GetFullPath(selected));
                if (Inside(absolute, root + "/Assets") || absolute.Equals(root + "/Assets", StringComparison.OrdinalIgnoreCase))
                { path = absolute.Substring(root.Length + 1); GUI.changed = true; }
                else EditorUtility.DisplayDialog("Invalid folder", "Choose a folder under this project's Assets directory.", "OK");
            }
        }
        if (GUILayout.Button("Clear", GUILayout.Width(50))) { path = ""; GUI.changed = true; }
        EditorGUILayout.EndHorizontal();
    }

    private bool Validate()
    {
        settings.sourceFolder = Normalize(settings.sourceFolder);
        settings.targetFolder = Normalize(settings.targetFolder);
        if (string.IsNullOrEmpty(settings.targetFolder) || !AssetDatabase.IsValidFolder(settings.targetFolder))
        {
            EditorUtility.DisplayDialog("Missing inventory folder", "Select an existing copied/inventory folder under Assets.", "OK");
            return false;
        }
        if (!string.IsNullOrEmpty(settings.sourceFolder) &&
            (!AssetDatabase.IsValidFolder(settings.sourceFolder) ||
             Inside(settings.sourceFolder, settings.targetFolder) ||
             Inside(settings.targetFolder, settings.sourceFolder) ||
             settings.sourceFolder.Equals(settings.targetFolder, StringComparison.OrdinalIgnoreCase)))
        {
            EditorUtility.DisplayDialog("Invalid source folder", "The optional source must exist and must not overlap the inventory folder.", "OK");
            return false;
        }
        if (settings.scenes.Count == 0 || settings.scenes.Any(p => string.IsNullOrEmpty(p) || AssetDatabase.LoadAssetAtPath<SceneAsset>(p) == null))
        {
            EditorUtility.DisplayDialog("Missing scenes", "Select at least one valid scene asset.", "OK");
            return false;
        }
        return true;
    }

    private static string[] AssetsIn(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return new string[0];
        return AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p) && Inside(p, folder) && !AssetDatabase.IsValidFolder(p))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToArray();
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(FullPath(path)))
            return BitConverter.ToString(sha.ComputeHash(stream));
    }

    private void Analyze()
    {
        if (!Validate()) return;
        analyzed = false;
        inventory.Clear();
        findings.Clear(); dynamicRisk.Clear(); referencedBy.Clear(); crossReferences.Clear();
        dependencies.Clear();
        messages.Clear();
        reportPath = "";
        try
        {
            foreach (string scene in settings.scenes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                dependencies.Add(scene);
                foreach (string dependency in AssetDatabase.GetDependencies(scene, settings.includeIndirect))
                    dependencies.Add(dependency);
            }
            AuditReferences();
            AuditCrossFolderDependencies();
            var source = AssetsIn(settings.sourceFolder);
            var sourceByRelative = source.ToDictionary(p => p.Substring(settings.sourceFolder.Length + 1), p => p, StringComparer.OrdinalIgnoreCase);
            var target = AssetsIn(settings.targetFolder);
            var sourceByName = source.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
            foreach (string path in target)
            {
                var item = new Entry { path = path, mappingStatus = "Not evaluated" };
                string full = FullPath(path);
                item.bytes = File.Exists(full) ? new FileInfo(full).Length : 0;
                if (dependencies.Contains(path))
                {
                    item.category = "Used by selected scenes";
                    item.reason = "Included in AssetDatabase.GetDependencies";
                    HashSet<string> owners;
                    if (referencedBy.TryGetValue(path, out owners)) item.evidence = string.Join("; ", owners.OrderBy(x => x).Take(8).ToArray());
                }
                else
                {
                    bool risk = dynamicRisk.Contains(path);
                    item.category = risk ? "Runtime / other-scene risk" : "Uncertain / potentially unused";
                    item.reason = risk ? "Runtime-load location, asset bundle label, or dependency of other scenes; keep until verified" : "Not in selected scene graph; absence does not prove unused";
                }
                if (source.Length > 0)
                {
                    string relative = path.Substring(settings.targetFolder.Length + 1);
                    string matching;
                    if (sourceByRelative.TryGetValue(relative, out matching)) item.match = matching;
                    else
                    {
                        string[] matches;
                        if (sourceByName.TryGetValue(Path.GetFileName(path), out matches) && matches.Length == 1) item.match = matches[0];
                    }
                    if (string.IsNullOrEmpty(item.match)) item.mappingStatus = "No unique source match";
                    else
                    {
                        string oldGuid = AssetDatabase.AssetPathToGUID(item.match);
                        string newGuid = AssetDatabase.AssetPathToGUID(path);
                        if (oldGuid == newGuid) item.mappingStatus = "Same GUID; no migration needed";
                        else if (!File.Exists(FullPath(item.match)) || !File.Exists(full)) item.mappingStatus = "Missing file";
                        else if (new FileInfo(FullPath(item.match)).Length != item.bytes) item.mappingStatus = "Different byte size";
                        else if (Hash(item.match) != Hash(path)) item.mappingStatus = "Different bytes";
                        else item.mappingStatus = "Byte-identical candidate; local file IDs NOT verified";
                    }
                }
                inventory.Add(item);
            }
            analyzed = true;
            messages.Add("Read-only audit completed. No files modified.");
            messages.Add("Findings: " + findings.Count + "; dynamic/other-scene risk: " + inventory.Count(e => e.category == "Runtime / other-scene risk") + ".");
            messages.Add("Report exports a second TSV with missing references and runtime-load signals.");
            messages.Add("Potentially unused assets are NOT safe deletion candidates without additional runtime checks.");
            messages.Add("Git savings are estimates based on working-tree bytes, not compressed Git history.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            messages.Add("Analysis failed: " + ex.Message);
        }
    }

    // Inspects dependencies through Unity import database, including binary scenes.
    // Scan selected scene closure and all destination assets; do not infer deletion safety.
    private void AuditCrossFolderDependencies()
    {
        if (string.IsNullOrEmpty(settings.sourceFolder)) return;
        var owners = new HashSet<string>(dependencies, StringComparer.OrdinalIgnoreCase);
        foreach (string asset in AssetsIn(settings.targetFolder)) owners.Add(asset);
        foreach (string owner in owners.OrderBy(p => p))
        {
            try
            {
                foreach (string dep in AssetDatabase.GetDependencies(owner, false))
                {
                    if (!Inside(dep, settings.sourceFolder)) continue;
                    crossReferences.Add(new[] { owner, dep,
                        dependencies.Contains(owner) ? "Scene closure" : "Destination asset" });
                    dynamicRisk.Add(dep);
                    findings.Add(new Finding
                    {
                        severity = "Warning",
                        kind = "Source dependency still required",
                        path = owner,
                        detail = dep
                    });
                }
            }
            catch (Exception ex)
            {
                findings.Add(new Finding
                {
                    severity = "Warning",
                    kind = "Dependency inspection failed",
                    path = owner,
                    detail = ex.Message
                });
            }
        }
        messages.Add("Cross-folder dependency edges: " + crossReferences.Count + ". Do not exclude source until resolved.");
    }

    // Audits serialized scene/dependency references without opening or modifying scenes.
    // Unity's dependency API is authoritative for imported references; YAML scanning adds
    // provenance and flags unresolved GUIDs. It cannot validate runtime-generated paths.
    private void AuditReferences()
    {
        var guidToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) || p.StartsWith("Packages/", StringComparison.Ordinal)))
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && !guidToPath.ContainsKey(guid)) guidToPath.Add(guid, path);
        }
        foreach (string owner in dependencies.OrderBy(p => p))
        {
            if (!InspectableExtensions.Contains(Path.GetExtension(owner))) continue;
            string content;
            if (!TryReadText(owner, out content)) continue;
            foreach (Match match in GuidReference.Matches(content))
            {
                string guid = match.Groups[1].Value;
                string referenced;
                if (!guidToPath.TryGetValue(guid, out referenced))
                {
                    findings.Add(new Finding { severity = "Warning", kind = "Unresolved serialized GUID", path = owner, detail = guid + " (may be built-in, package or external)" });
                    continue;
                }
                HashSet<string> owners;
                if (!referencedBy.TryGetValue(referenced, out owners)) referencedBy[referenced] = owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                owners.Add(owner);
            }
        }

        // Resources and AssetBundles may be loaded by name without a scene reference.
        foreach (string path in AssetsIn(settings.targetFolder))
        {
            if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/StreamingAssets/", StringComparison.OrdinalIgnoreCase) >= 0)
                dynamicRisk.Add(path);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName)) dynamicRisk.Add(path);
        }

        // Other scenes may require the same inventory even if outside selected scope.
        foreach (string scene in AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !settings.scenes.Contains(p) && p.StartsWith("Assets/", StringComparison.Ordinal)))
        {
            try
            {
                foreach (string dependency in AssetDatabase.GetDependencies(scene, true))
                    if (Inside(dependency, settings.targetFolder)) dynamicRisk.Add(dependency);
            }
            catch (Exception ex)
            {
                findings.Add(new Finding { severity = "Info", kind = "Other scene scan failed", path = scene, detail = ex.Message });
            }
        }

        // Source-code inspection only identifies API usage, not runtime-generated asset paths.
        foreach (string script in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            string content;
            if (!TryReadText(script, out content)) continue;
            var matches = DynamicLoad.Matches(content);
            if (matches.Count > 0)
                findings.Add(new Finding
                {
                    severity = "Info",
                    kind = "Dynamic loading API",
                    path = script,
                    detail = string.Join(", ", matches.Cast<Match>().Select(m => m.Value).Distinct().ToArray()) + "; runtime asset paths not statically proven"
                });
        }

        // Dependency graph consistency check: imported references outside graph are not
        // automatically added; they are flagged for investigation.
        foreach (var kv in referencedBy)
            if (!dependencies.Contains(kv.Key))
                findings.Add(new Finding
                {
                    severity = "Info",
                    kind = "Serialized reference outside dependency graph",
                    path = kv.Key,
                    detail = string.Join("; ", kv.Value.Take(5).ToArray())
                });
    }

    private static bool TryReadText(string path, out string content)
    {
        content = null;
        try
        {
            string full = FullPath(path);
            if (!File.Exists(full) || new FileInfo(full).Length > 64L * 1024 * 1024) return false;
            byte[] bytes = File.ReadAllBytes(full);
            if (bytes.Any(b => b == 0)) return false;
            content = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (Exception) { return false; }
    }

    private void Preview()
    {
        int count = inventory.Count(e => e.mappingStatus.StartsWith("Byte-identical", StringComparison.Ordinal));
        messages.Insert(0, "Preview: " + count + " byte-identical mapping candidates. No YAML changes computed; local file IDs and editable scope must be validated before migration.");
        Debug.Log("[ADM] " + messages[0]);
    }

    // v4: subasset-aware preview. This deliberately does not rewrite serialized GUIDs.
    // An identical file can still import differently when .meta importer settings differ.
    private sealed class LocalObjectId
    {
        public string key;
        public long fileId;
    }

    private static List<LocalObjectId> GetLocalIds(string path, out string error)
    {
        error = "";
        var result = new List<LocalObjectId>();
        try
        {
            var objects = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                string guid;
                long id;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out id))
                {
                    error = "Cannot read local ID: " + obj.name;
                    return result;
                }
                result.Add(new LocalObjectId { key = obj.GetType().FullName + "|" + obj.name, fileId = id });
            }
        }
        catch (Exception ex) { error = ex.Message; }
        return result;
    }

    private void ExportMigrationPreview()
    {
        if (!analyzed || !Validate()) return;
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, "migration_preview_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        var sourceToTarget = inventory.Where(e => !string.IsNullOrEmpty(e.match))
            .GroupBy(e => e.match, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var requiredSource = crossReferences.Select(e => e[1])
            .Concat(dependencies.Where(p => Inside(p, settings.sourceFolder)))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToArray();
        using (var writer = new StreamWriter(output, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("Source\tDestination\tStatus\tReason\tOwnerCount\tSourceGUID\tTargetGUID");
            foreach (string source in requiredSource)
            {
                Entry[] options;
                sourceToTarget.TryGetValue(source, out options);
                string destination = "", status = "BLOCKED", reason = "No unique matching destination";
                if (options != null && options.Length == 1)
                {
                    var entry = options[0];
                    destination = entry.path;
                    if (entry.mappingStatus == "Same GUID; no migration needed")
                    { status = "ALREADY_SHARED"; reason = "Same GUID"; }
                    else if (!entry.mappingStatus.StartsWith("Byte-identical", StringComparison.Ordinal))
                        reason = entry.mappingStatus;
                    else
                    {
                        string sourceError, targetError;
                        var src = GetLocalIds(source, out sourceError);
                        var dst = GetLocalIds(destination, out targetError);
                        if (!string.IsNullOrEmpty(sourceError) || !string.IsNullOrEmpty(targetError))
                            reason = "Local ID inspection failed: " + sourceError + " " + targetError;
                        else if (src.Count == 0 || dst.Count == 0)
                            reason = "No imported objects available";
                        else if (src.GroupBy(x => x.key).Any(g => g.Count() != 1) || dst.GroupBy(x => x.key).Any(g => g.Count() != 1))
                            reason = "Ambiguous duplicate subasset names/types";
                        else
                        {
                            var sm = src.ToDictionary(x => x.key, x => x.fileId);
                            var dm = dst.ToDictionary(x => x.key, x => x.fileId);
                            if (sm.Count != dm.Count || sm.Any(x => !dm.ContainsKey(x.Key)))
                                reason = "Imported subasset sets differ";
                            else if (sm.Any(x => dm[x.Key] != x.Value))
                            { status = "NEEDS_FILEID_REMAP"; reason = "Subasset IDs differ; cannot replace GUID alone"; }
                            else
                            { status = "CANDIDATE"; reason = "Bytes and imported local IDs match; importer settings and owners still require validation"; }
                        }
                    }
                }
                else if (options != null && options.Length > 1) reason = "Multiple destination candidates";
                int owners = crossReferences.Count(e => e[1].Equals(source, StringComparison.OrdinalIgnoreCase));
                writer.WriteLine(string.Join("\t", new[] { SafeTsv(source), SafeTsv(destination), status,
                    SafeTsv(reason), owners.ToString(), AssetDatabase.AssetPathToGUID(source),
                    string.IsNullOrEmpty(destination) ? "" : AssetDatabase.AssetPathToGUID(destination) }));
            }
        }
        messages.Insert(0, "v4 preview exported: " + output + ". No files or references changed.");
        Debug.Log("[ADM] " + messages[0]);
        EditorUtility.RevealInFinder(output);
    }

    // Only GUID-only mappings with matching imported local file IDs are eligible.
    // YAML is edited only in selected scenes and in target-folder dependencies of those scenes.
    // Binary serialized files are explicitly skipped; never edit binary bytes as text.
    private void MigrateReferences(bool apply)
    {
        if (!analyzed || !Validate()) return;
        var sourceToTarget = inventory.Where(e => !string.IsNullOrEmpty(e.match))
            .GroupBy(e => e.match, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sourceToTarget)
        {
            Entry entry = pair.Value;
            if (!entry.mappingStatus.StartsWith("Byte-identical", StringComparison.Ordinal)) continue;
            string srcError, dstError;
            var src = GetLocalIds(pair.Key, out srcError);
            var dst = GetLocalIds(entry.path, out dstError);
            if (!string.IsNullOrEmpty(srcError) || !string.IsNullOrEmpty(dstError) || src.Count == 0 || dst.Count == 0) continue;
            if (src.GroupBy(x => x.key).Any(g => g.Count() != 1) || dst.GroupBy(x => x.key).Any(g => g.Count() != 1)) continue;
            var a = src.ToDictionary(x => x.key, x => x.fileId);
            var b = dst.ToDictionary(x => x.key, x => x.fileId);
            if (a.Count != b.Count || a.Any(x => !b.ContainsKey(x.Key) || b[x.Key] != x.Value)) continue;
            string oldGuid = AssetDatabase.AssetPathToGUID(pair.Key);
            string newGuid = AssetDatabase.AssetPathToGUID(entry.path);
            if (oldGuid.Length == 32 && newGuid.Length == 32 && !oldGuid.Equals(newGuid, StringComparison.OrdinalIgnoreCase))
                mapping[oldGuid] = newGuid;
        }
        var owners = dependencies.Where(p => Inside(p, settings.targetFolder))
            .Concat(settings.scenes).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToArray();
        var changes = new List<string[]>();
        var edits = new List<KeyValuePair<string, string>>();
        foreach (string owner in owners)
        {
            if (!InspectableExtensions.Contains(Path.GetExtension(owner))) continue;
            string content;
            if (!TryReadText(owner, out content))
            {
                changes.Add(new[] { owner, "SKIPPED_BINARY_OR_UNREADABLE", "0", "Requires Unity serialized-object migration" });
                continue;
            }
            int replaced = 0;
            string updated = GuidReference.Replace(content, match =>
            {
                string target;
                if (!mapping.TryGetValue(match.Groups[1].Value, out target)) return match.Value;
                replaced++;
                return match.Value.Substring(0, match.Groups[1].Index - match.Index) + target;
            });
            if (replaced > 0)
            {
                changes.Add(new[] { owner, apply ? "APPLIED" : "WOULD_APPLY", replaced.ToString(), "GUID-only, identical imported fileIDs" });
                edits.Add(new KeyValuePair<string, string>(owner, updated));
            }
        }
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string report = Path.Combine(folder, "migration_" + (apply ? "apply_" : "dryrun_") + stamp + ".tsv");
        if (apply && edits.Count > 0)
        {
            if (!EditorUtility.DisplayDialog("Confirm migration",
                "Replace GUIDs in " + edits.Count + " YAML files? Each original will be backed up first. Binary scenes and blocked mappings are skipped. Please ensure the project is also backed up or committed.",
                "Apply with backups", "Cancel")) return;
            string backup = Path.Combine(folder, "backup_" + stamp);
            // Write all backups before modifying any source files.
            foreach (var edit in edits)
            {
                string dest = Path.Combine(backup, edit.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(FullPath(edit.Key), dest, false);
            }
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var edit in edits)
                    File.WriteAllText(FullPath(edit.Key), edit.Value, new UTF8Encoding(false));
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.Refresh(); }
            messages.Insert(0, "Backups: " + backup);
            analyzed = false; // force a fresh dependency graph after modifications
        }
        using (var writer = new StreamWriter(report, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("Owner\tStatus\tReferenceCount\tDetail");
            foreach (var row in changes) writer.WriteLine(string.Join("\t", row.Select(SafeTsv).ToArray()));
        }
        messages.Insert(0, (apply ? "Migration applied" : "Dry-run complete") + ": " + edits.Count + " files, " +
            changes.Where(x => x[1] == "SKIPPED_BINARY_OR_UNREADABLE").Count() + " skipped binary/unreadable files; " + mapping.Count + " safe GUID mappings. Report: " + report);
        Debug.Log("[ADM] " + messages[0]);
        EditorUtility.RevealInFinder(report);
    }

    private static string SafeTsv(string value)
    {
        return (value ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    private static string FormatBytes(long bytes)
    {
        return (bytes / 1048576.0).ToString("F2") + " MiB";
    }

    private void ExportReport()
    {
        if (!analyzed) return;
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(folder);
        reportPath = Path.Combine(folder, "audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        using (var writer = new StreamWriter(reportPath, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("# Read-only audit; no automatic deletion or migration is authorized");
            writer.WriteLine("# Selected scenes: " + string.Join("; ", settings.scenes.Select(SafeTsv).ToArray()));
            writer.WriteLine("# Include indirect: " + settings.includeIndirect);
            writer.WriteLine("# Source: " + SafeTsv(settings.sourceFolder) + " | Inventory: " + SafeTsv(settings.targetFolder));
            writer.WriteLine("Path\tSizeBytes\tCategory\tReason\tMatchingSource\tMappingStatus\tReferenceEvidence");
            foreach (var item in inventory)
                writer.WriteLine(string.Join("\t", new[] { SafeTsv(item.path), item.bytes.ToString(), SafeTsv(item.category), SafeTsv(item.reason), SafeTsv(item.match), SafeTsv(item.mappingStatus), SafeTsv(item.evidence) }));
            writer.WriteLine("# Source dependencies outside inventory");
            foreach (string path in dependencies.Where(p => Inside(p, settings.sourceFolder)).OrderBy(p => p))
                writer.WriteLine("# " + SafeTsv(path));
        }
        string diagnosticPath = Path.Combine(folder, "diagnostics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        using (var writer = new StreamWriter(diagnosticPath, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("Severity\tKind\tFile\tDetail");
            foreach (var f in findings)
                writer.WriteLine(string.Join("\t", new[] { SafeTsv(f.severity), SafeTsv(f.kind), SafeTsv(f.path), SafeTsv(f.detail) }));
        }
        string crossPath = Path.Combine(folder, "cross_dependencies_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        using (var writer = new StreamWriter(crossPath, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("Owner\tSourceDependency\tOwnerScope");
            foreach (var edge in crossReferences)
                writer.WriteLine(string.Join("\t", edge.Select(SafeTsv).ToArray()));
        }
        Debug.Log("[ADM] Cross-folder report saved: " + crossPath);
        Debug.Log("[ADM] Diagnostic report saved: " + diagnosticPath);
        Debug.Log("[ADM] Report saved: " + reportPath);
        EditorUtility.RevealInFinder(reportPath);
    }
}
