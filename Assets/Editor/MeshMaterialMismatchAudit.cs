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
