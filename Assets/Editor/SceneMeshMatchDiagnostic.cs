// Unity 6 Editor. Diagnostic only: never modifies assets or scenes.
// Place in Assets/Editor/SceneMeshMatchDiagnostic.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneMeshMatchDiagnostic
{
    private const string Source = "Assets/tests-scenes/MedievalRuins";
    private const string Target = "Assets/Environment";
    private const string ScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Asset Dependency Manager/Diagnose 28 FBX matches")]
    public static void Diagnose()
    {
        if (!File.Exists(ScenePath) || !AssetDatabase.IsValidFolder(Source) || !AssetDatabase.IsValidFolder(Target))
        {
            EditorUtility.DisplayDialog("Missing paths", "Check ScenePath, Source and Target constants at the top of SceneMeshMatchDiagnostic.cs.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var used = new Dictionary<string, HashSet<Mesh>>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (path.StartsWith(Source + "/", StringComparison.OrdinalIgnoreCase))
            {
                HashSet<Mesh> set;
                if (!used.TryGetValue(path, out set)) used[path] = set = new HashSet<Mesh>();
                set.Add(mesh);
            }
        }
        foreach (var root in scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) continue;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (path.StartsWith(Source + "/", StringComparison.OrdinalIgnoreCase))
            {
                HashSet<Mesh> set;
                if (!used.TryGetValue(path, out set)) used[path] = set = new HashSet<Mesh>();
                set.Add(mesh);
            }
        }
        var rows = new List<string> { "SourceFBX\tSourceMesh\tSourceLocalID\tSourceReadable\tSourceVertices\tSourceSubmeshes\tCandidateFBX\tCandidateExists\tByteIdentical\tCandidateMesh\tCandidateLocalID\tCandidateReadable\tCandidateVertices\tCandidateSubmeshes\tResult" };
        foreach (var entry in used.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            string relative = entry.Key.Substring(Source.Length).TrimStart('/');
            string candidate = Target + "/" + relative;
            bool exists = File.Exists(candidate);
            bool sameBytes = exists && SameBytes(entry.Key, candidate);
            var destMeshes = exists ? AssetDatabase.LoadAllAssetsAtPath(candidate).OfType<Mesh>().ToArray() : new Mesh[0];
            foreach (var src in entry.Value.OrderBy(m => m.name))
            {
                var nameMatches = destMeshes.Where(m => m.name == src.name).ToArray();
                if (!exists) rows.Add(Row(entry.Key, src, candidate, false, false, null, "DESTINATION_FILE_MISSING"));
                else if (destMeshes.Length == 0) rows.Add(Row(entry.Key, src, candidate, true, sameBytes, null, "NO_DESTINATION_MESHES"));
                else if (nameMatches.Length == 0)
                {
                    foreach (var dest in destMeshes) rows.Add(Row(entry.Key, src, candidate, true, sameBytes, dest, "NAME_MISMATCH"));
                }
                else
                {
                    foreach (var dest in nameMatches)
                    {
                        string reason = !src.isReadable || !dest.isReadable ? "MESH_NOT_READABLE" :
                            src.vertexCount != dest.vertexCount ? "VERTEX_COUNT_DIFFERENT" :
                            src.subMeshCount != dest.subMeshCount ? "SUBMESH_COUNT_DIFFERENT" :
                            !sameBytes ? "FILE_BYTES_DIFFERENT_CHECK_GEOMETRY" :
                            "SAME_BYTES_AND_MESH_NAME";
                        rows.Add(Row(entry.Key, src, candidate, true, sameBytes, dest, reason));
                    }
                }
            }
        }
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "AssetDependencyManagerReports");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, "mesh_match_diagnostic_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tsv");
        File.WriteAllLines(output, rows.ToArray());
        Debug.Log("[MeshDiagnostic] " + used.Count + " source FBX; " + rows.Count + " report rows. No changes made. " + output);
        EditorUtility.RevealInFinder(output);
    }

    private static bool SameBytes(string a, string b)
    {
        var x = new FileInfo(a); var y = new FileInfo(b);
        if (x.Length != y.Length) return false;
        using (var sha = SHA256.Create())
        using (var ax = File.OpenRead(a))
        using (var by = File.OpenRead(b))
            return sha.ComputeHash(ax).SequenceEqual(sha.ComputeHash(by));
    }
    private static string Id(UnityEngine.Object obj)
    {
        if (obj == null) return "";
        string guid; long id;
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out id) ? id.ToString() : "UNKNOWN";
    }
    private static string Row(string path, Mesh src, string candidate, bool exists, bool same, Mesh dest, string status)
    {
        string[] values = { path, src.name, Id(src), src.isReadable.ToString(), src.vertexCount.ToString(), src.subMeshCount.ToString(),
            candidate, exists.ToString(), same.ToString(), dest == null ? "" : dest.name, Id(dest),
            dest == null ? "" : dest.isReadable.ToString(), dest == null ? "" : dest.vertexCount.ToString(),
            dest == null ? "" : dest.subMeshCount.ToString(), status };
        return string.Join("\t", values.Select(v => v.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ')).ToArray());
    }
}
