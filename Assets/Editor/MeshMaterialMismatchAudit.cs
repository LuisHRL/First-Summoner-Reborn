#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Audits material-slot/submesh mismatches across prefab assets and loaded scenes.
/// Read-only: never changes source assets or scene objects.
/// </summary>
public static class MeshMaterialMismatchAudit
{
    [MenuItem("Tools/First Summoner/Audit Mesh Material Slots")]
    public static void Run()
    {
        var lines = new List<string> { "AssetPath,HierarchyPath,MeshName,Submeshes,MaterialSlots,ExtraSlots,TrailingSlotsDuplicate,Recommendation" };
        int inspected = 0, mismatches = 0;
        var seen = new HashSet<string>();

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                Inspect(renderer, path, lines, seen, ref inspected, ref mismatches);
        }

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    Inspect(renderer, scene.path.Length > 0 ? scene.path : "(unsaved scene)", lines, seen, ref inspected, ref mismatches);
        }

        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Diagnostics");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, "mesh_material_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        File.WriteAllLines(output, lines, new UTF8Encoding(false));
        Debug.Log("[Mesh Material Audit] Inspected " + inspected + " renderers, found " + mismatches
            + " mismatches. Report: " + output
            + ". No assets modified. Additional materials can intentionally redraw the last submesh; review before fixing.");
        EditorUtility.RevealInFinder(output);
    }

    private static void Inspect(MeshRenderer renderer, string assetPath, List<string> lines,
        HashSet<string> seen, ref int inspected, ref int mismatches)
    {
        if (renderer == null) return;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        Mesh mesh = filter.sharedMesh;
        Material[] materials = renderer.sharedMaterials;
        inspected++;
        if (materials.Length <= mesh.subMeshCount) return;
        string hierarchy = GetHierarchyPath(renderer.transform);
        string key = assetPath + "|" + hierarchy;
        if (!seen.Add(key)) return;
        mismatches++;
        bool duplicate = mesh.subMeshCount > 0;
        if (duplicate)
        {
            Material last = materials[mesh.subMeshCount - 1];
            for (int i = mesh.subMeshCount; i < materials.Length; i++)
                if (materials[i] != last) { duplicate = false; break; }
        }
        lines.Add(Csv(assetPath) + "," + Csv(hierarchy) + "," + Csv(mesh.name) + ","
            + mesh.subMeshCount + "," + materials.Length + "," + (materials.Length - mesh.subMeshCount)
            + "," + duplicate + "," + Csv(duplicate
                ? "Review duplicate trailing slots before removal"
                : "Manual review: extra material may intentionally render final submesh"));
    }


    [MenuItem("Tools/First Summoner/Compare Mesh Materials With Reference Folder")]
    public static void CompareWithReferenceFolder()
    {
        string folder = EditorUtility.OpenFolderPanel("Choose original asset reference folder", Application.dataPath, "");
        if (string.IsNullOrEmpty(folder)) return;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        folder = folder.Replace('\\', '/');
        if (!folder.StartsWith(projectRoot + "/Assets/", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("Choose a folder inside this project's Assets directory.");
            return;
        }
        string assetFolder = folder.Substring(projectRoot.Length + 1);
        var reference = new Dictionary<string, List<string>>();
        var referencePaths = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { assetFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            referencePaths.Add(path);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                string key = filter.sharedMesh.name;
                if (!reference.TryGetValue(key, out var entries))
                    reference[key] = entries = new List<string>();
                entries.Add(Describe(renderer));
            }
        }
        var lines = new List<string> { "TargetAsset,Hierarchy,Mesh,TargetSlots,ReferenceMatches,ReferenceDetails,Assessment" };
        int matches = 0, missing = 0;
        var visited = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (referencePaths.Contains(path)) continue;
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                CompareRenderer(renderer, path, reference, visited, lines, ref matches, ref missing);
        }
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    CompareRenderer(renderer, scene.path, reference, visited, lines, ref matches, ref missing);
        }
        string outputFolder = Path.Combine(projectRoot, "Diagnostics");
        Directory.CreateDirectory(outputFolder);
        string output = Path.Combine(outputFolder, "mesh_reference_comparison_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        File.WriteAllLines(output, lines, new UTF8Encoding(false));
        Debug.Log("[Mesh Reference Comparison] " + matches + " matched, " + missing + " without mesh-name reference. Read-only report: " + output);
        EditorUtility.RevealInFinder(output);
    }

    private static void CompareRenderer(MeshRenderer renderer, string path, Dictionary<string, List<string>> reference,
        HashSet<string> visited, List<string> lines, ref int matches, ref int missing)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || renderer.sharedMaterials.Length <= filter.sharedMesh.subMeshCount) return;
        string hierarchy = GetHierarchyPath(renderer.transform);
        if (!visited.Add(path + "|" + hierarchy)) return;
        string meshName = filter.sharedMesh.name;
        bool found = reference.TryGetValue(meshName, out var entries);
        if (found) matches++; else missing++;
        string details = found ? string.Join(" || ", entries) : "";
        string current = Describe(renderer);
        bool identical = found && entries.Contains(current);
        string assessment = !found ? "No reference found" : identical ? "Identical to reference" : "Different from reference - inspect manually";
        lines.Add(Csv(path) + "," + Csv(hierarchy) + "," + Csv(meshName) + "," + Csv(current)
            + "," + (found ? entries.Count.ToString() : "0") + "," + Csv(details) + "," + Csv(assessment));
    }

    private static string Describe(MeshRenderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        var parts = new List<string> { "submeshes=" + mesh.subMeshCount };
        foreach (Material material in renderer.sharedMaterials)
            parts.Add(material == null ? "(null)" : material.name + " [shader=" + (material.shader == null ? "(null)" : material.shader.name) + "]");
        return string.Join(" | ", parts);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var names = new List<string>();
        while (transform != null)
        {
            names.Add(transform.name);
            transform = transform.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
#endif
