#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class OriginalHPFrameAudit
{
    [MenuItem("Tools/First Summoner/Combat UI/Audit HP Frames (Read Only)")]
    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("source\tpath\tobject\tsprite\tspriteSize\tborder\timageType\tfillCenter\tppuMultiplier\timageRect\tanchors\tpivot\tposition\tsizeDelta\tscale\tmaterial\tactive");
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                try
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        foreach (var root in scene.GetRootGameObjects())
                            foreach (var image in root.GetComponentsInChildren<Image>(true))
                                Record(report, "scene", path, image);
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
                catch (Exception e) { Debug.LogWarning("[HP Audit] Scene skipped: " + path + " " + e.Message); }
            }
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                GameObject prefab = null;
                try
                {
                    prefab = PrefabUtility.LoadPrefabContents(path);
                    foreach (var image in prefab.GetComponentsInChildren<Image>(true))
                        Record(report, "prefab", path, image);
                }
                catch (Exception e) { Debug.LogWarning("[HP Audit] Prefab skipped: " + path + " " + e.Message); }
                finally { if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab); }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }
        string folder = Path.Combine(Application.dataPath, "../HPFrameAudits");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, "hp_frames_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv");
        File.WriteAllText(output, report.ToString(), Encoding.UTF8);
        Debug.Log("[HP Audit] Read-only report: " + Path.GetFullPath(output));
        EditorUtility.RevealInFinder(output);
    }

    private static void Record(StringBuilder report, string source, string assetPath, Image image)
    {
        string name = image.sprite != null ? image.sprite.name : "";
        string hierarchy = PathOf(image.transform);
        if (name.IndexOf("hp", StringComparison.OrdinalIgnoreCase) < 0 &&
            hierarchy.IndexOf("health", StringComparison.OrdinalIgnoreCase) < 0 &&
            hierarchy.IndexOf("hp", StringComparison.OrdinalIgnoreCase) < 0)
            return;
        var sprite = image.sprite;
        var r = image.rectTransform;
        string spriteSize = sprite == null ? "" : sprite.rect.size.ToString("F2");
        string border = sprite == null ? "" : sprite.border.ToString("F2");
        string material = image.material == null ? "" : image.material.name;
        string[] values = { source, assetPath, hierarchy, name, spriteSize, border,
            image.type.ToString(), image.fillCenter.ToString(), image.pixelsPerUnitMultiplier.ToString("R"),
            r.rect.size.ToString("F2"), r.anchorMin.ToString("F3") + "/" + r.anchorMax.ToString("F3"),
            r.pivot.ToString("F3"), r.anchoredPosition.ToString("F3"), r.sizeDelta.ToString("F3"),
            r.localScale.ToString("F3"), material, image.gameObject.activeSelf.ToString() };
        report.AppendLine(string.Join("\t", values.Select(v => v.Replace("\t", " ").Replace("\n", " "))));
    }

    private static string PathOf(Transform t)
    {
        string result = t.name;
        while (t.parent != null) { t = t.parent; result = t.name + "/" + result; }
        return result;
    }
}
#endif
