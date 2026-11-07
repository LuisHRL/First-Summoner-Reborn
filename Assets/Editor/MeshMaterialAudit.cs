#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MeshMaterialAudit
{
    [MenuItem("Tools/Asset Dependency Manager/Audit mesh materials (read only)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Mesh material audit: exit Play mode first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var rows = new List<string> { "AssetPath\tObjectPath\tMesh\tMaterials\tSubmeshes\tIssue" };
        int inspected = 0;
        int issues = 0;
        var originalSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var paths = AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Mesh material audit", path, (float)i / Math.Max(paths.Length, 1)))
                {
                    Debug.LogWarning("Mesh material audit cancelled; no report written.");
                    return;
                }

                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    GameObject root = null;
                    try
                    {
                        root = PrefabUtility.LoadPrefabContents(path);
                        Inspect(new[] { root }, path, rows, ref inspected, ref issues);
                    }
                    catch (Exception ex) { Debug.LogError("Audit failed for " + path + ": " + ex); }
                    finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
                }
                else
                {
                    Scene scene = default;
                    try
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        Inspect(scene.GetRootGameObjects(), path, rows, ref inspected, ref issues);
                    }
                    catch (Exception ex) { Debug.LogError("Audit failed for " + path + ": " + ex); }
                    finally
                    {
                        if (scene.IsValid() && scene.isLoaded)
                            EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "AssetDependencyManagerReports");
        Directory.CreateDirectory(directory);
        string report = Path.Combine(directory, "mesh_material_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        File.WriteAllLines(report, rows, new UTF8Encoding(false));
        Debug.Log($"Mesh material audit complete: {inspected} renderers inspected, {issues} issues. Report: {report}");
        EditorUtility.RevealInFinder(report);
    }

    private static void Inspect(IEnumerable<GameObject> roots, string assetPath, List<string> rows, ref int inspected, ref int issues)
    {
        foreach (GameObject root in roots)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                inspected++;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                AddIfMismatch(renderer, mesh, assetPath, rows, ref issues);
            }
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                inspected++;
                if (renderer.sharedMesh == null) continue;
                AddIfMismatch(renderer, renderer.sharedMesh, assetPath, rows, ref issues);
            }
        }
    }

    private static void AddIfMismatch(Renderer renderer, Mesh mesh, string assetPath, List<string> rows, ref int issues)
    {
        int materials = renderer.sharedMaterials.Length;
        int submeshes = mesh.subMeshCount;
        if (materials <= submeshes) return;
        issues++;
        var parts = new Stack<string>();
        for (Transform t = renderer.transform; t != null; t = t.parent) parts.Push(t.name);
        rows.Add(string.Join("\t", new[] { Clean(assetPath), Clean(string.Join("/", parts)), Clean(mesh.name), materials.ToString(), submeshes.ToString(), "Extra material slots" }));
    }

    private static string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
#endif
