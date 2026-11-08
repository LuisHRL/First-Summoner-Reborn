using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only inventory of existing lighting and flame effects in a local asset folder.</summary>
public sealed class EnvironmentEffectInventory : EditorWindow
{
    private DefaultAsset rootFolder;
    private Vector2 scroll;
    private string summary = "Select the local Environment folder, then scan.";
    private readonly List<string> results = new List<string>();

    [MenuItem("Tools/Environment/Find Existing Torch Effects")]
    private static void Open() => GetWindow<EnvironmentEffectInventory>("Torch Effect Finder");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Read-only scan of prefab assets. Does not change or import materials, scenes or prefabs. Results can be exported as a small TSV file outside Assets.", MessageType.Info);
        rootFolder = (DefaultAsset)EditorGUILayout.ObjectField("Folder to scan", rootFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("Scan prefabs (read-only)", GUILayout.Height(30)))
            EditorApplication.delayCall += Scan;
        EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedLabel);
        using (new EditorGUI.DisabledScope(results.Count == 0))
        {
            if (GUILayout.Button("Export TSV report", GUILayout.Height(26)))
                Export();
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string line in results.Take(250))
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        results.Clear();
        string folder = rootFolder == null ? "" : AssetDatabase.GetAssetPath(rootFolder);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            summary = "Choose a folder under Assets (for example Assets/Environment).";
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        int scanned = 0;
        int failed = 0;
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar("Find existing torch effects", path, guids.Length == 0 ? 1f : (float)i / guids.Length))
                    break;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { failed++; continue; }
                scanned++;
                try
                {
                    // Include disabled components and nested children of each prefab.
                    Light[] lights = prefab.GetComponentsInChildren<Light>(true);
                    ParticleSystem[] particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
                    Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    Component[] components = prefab.GetComponentsInChildren<Component>(true);
                    List<string> vfx = new List<string>();
                    foreach (Component component in components)
                    {
                        if (component == null) continue;
                        if (component.GetType().FullName == "UnityEngine.VFX.VisualEffect")
                            vfx.Add(RelativePath(prefab.transform, component.transform));
                    }

                    bool torchName = ContainsAny(prefab.name, "torch", "flame", "fire", "brazier", "campfire");
                    bool effectName = ContainsAny(prefab.name, "fx", "particle", "vfx", "light");
                    if (lights.Length == 0 && particles.Length == 0 && vfx.Count == 0 && !torchName && !effectName)
                        continue;

                    string lightInfo = string.Join("; ", lights.Select(l => RelativePath(prefab.transform, l.transform) +
                        " [" + l.type + ", enabled=" + l.enabled + ", intensity=" + l.intensity + "]").ToArray());
                    string particleInfo = string.Join("; ", particles.Select(p => RelativePath(prefab.transform, p.transform) +
                        " [active=" + p.gameObject.activeSelf + "]").ToArray());
                    string rendererInfo = torchName ? string.Join("; ", renderers.Take(12).Select(m =>
                        RelativePath(prefab.transform, m.transform)).ToArray()) : "";
                    results.Add(Escape(path) + "\t" + Escape(prefab.name) + "\t" + lights.Length + "\t" +
                        particles.Length + "\t" + vfx.Count + "\t" + Escape(lightInfo) + "\t" +
                        Escape(particleInfo) + "\t" + Escape(string.Join("; ", vfx.ToArray())) + "\t" + Escape(rendererInfo));
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogWarning("[Torch Effect Finder] Could not inspect " + path + ": " + ex.Message);
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        summary = "Scanned " + scanned + " prefabs; " + results.Count + " relevant candidates; " + failed +
                  " unreadable/failed. No assets modified. Export the TSV for review.";
        Debug.Log("[Torch Effect Finder] " + summary);
    }

    private void Export()
    {
        string destination = EditorUtility.SaveFilePanel("Export torch effect inventory", "", "torch_effect_inventory.tsv", "tsv");
        if (string.IsNullOrEmpty(destination)) return;
        string header = "AssetPath\tPrefabName\tLightCount\tParticleSystemCount\tVisualEffectCount\tLights\tParticles\tVisualEffects\tRenderers";
        File.WriteAllText(destination, header + "\n" + string.Join("\n", results.ToArray()), new UTF8Encoding(true));
        Debug.Log("[Torch Effect Finder] Report exported to " + destination);
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string RelativePath(Transform root, Transform child)
    {
        if (child == root) return ".";
        List<string> parts = new List<string>();
        for (Transform t = child; t != null && t != root; t = t.parent)
            parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    }
}
