// Scoped, opt-in material shader migration. No scene/prefab edits.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ScopedUrpLitMaterialConverter : EditorWindow
{
    private DefaultAsset folder;
    private readonly List<string> candidates = new List<string>();
    private readonly HashSet<string> excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private Vector2 scroll;
    private string report = "Select a folder under Assets, then Preview.";
    private bool includeAlreadyLit;
    private bool alphaClipMode;
    private string alphaNameTokens = "Branch,Leaf,Leaves,Bush,Foliage,Grass,Plant,Burdock";
    private float alphaThreshold = 0.5f;
    private bool detectTextureAlpha = true;
    private bool includeAmbiguousAlpha;
    private readonly Dictionary<string, string> detectionReasons = new Dictionary<string, string>();

    [MenuItem("Tools/Materials/Scoped URP Lit Converter")]
    private static void Open() => GetWindow<ScopedUrpLitMaterialConverter>("URP Lit Converter");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Converts only .mat assets inside the selected folder and its subfolders. Start with Assets/Environment/URP; review HDRP separately. Shader Graph and HDRP features may not transfer.", MessageType.Warning);
        EditorGUI.BeginChangeCheck();
        folder = (DefaultAsset)EditorGUILayout.ObjectField("Materials folder", folder, typeof(DefaultAsset), false);
        alphaClipMode = EditorGUILayout.Toggle("Alpha Clipping mode", alphaClipMode);
        if (alphaClipMode)
        {
            detectTextureAlpha = EditorGUILayout.Toggle("Detect texture alpha (all names)", detectTextureAlpha);
            if (detectTextureAlpha)
                includeAmbiguousAlpha = EditorGUILayout.Toggle("Select ambiguous alpha", includeAmbiguousAlpha);
            if (!detectTextureAlpha)
                alphaNameTokens = EditorGUILayout.TextField("Name tokens (comma-separated)", alphaNameTokens);
            alphaThreshold = EditorGUILayout.Slider("Alpha Cutoff", alphaThreshold, 0f, 1f);
            EditorGUILayout.HelpBox(detectTextureAlpha ? "Read-only Base Map alpha sampling. Review ambiguous materials before selecting; no automatic changes." : "Matches material filenames. Review and exclude false positives.", MessageType.Info);
        }
        else includeAlreadyLit = EditorGUILayout.Toggle("Include existing URP/Lit", includeAlreadyLit);
        if (EditorGUI.EndChangeCheck()) { candidates.Clear(); excludedPaths.Clear(); detectionReasons.Clear(); report = "Settings changed. Preview again."; }
        if (GUILayout.Button("Preview (read-only)", GUILayout.Height(30)))
            Preview();
        EditorGUILayout.LabelField(report, EditorStyles.wordWrappedLabel);
        int selectedCount = candidates.Count(path => !excludedPaths.Contains(path));
        EditorGUILayout.LabelField("Selected: " + selectedCount + " / " + candidates.Count + " | Excluded: " + excludedPaths.Count);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select all")) excludedPaths.Clear();
            if (GUILayout.Button("Exclude all")) foreach (string path in candidates) excludedPaths.Add(path);
        }
        using (new EditorGUI.DisabledScope(selectedCount == 0))
        {
            if (GUILayout.Button(alphaClipMode ? "Enable Alpha Clipping on selected materials" : "Convert previewed materials to URP/Lit", GUILayout.Height(30)))
                EditorApplication.delayCall += alphaClipMode
                    ? (EditorApplication.CallbackFunction)ApplyAlphaClipping
                    : ConvertPreviewed;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string path in candidates)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = !excludedPaths.Contains(path);
                bool newSelected = EditorGUILayout.Toggle(selected, GUILayout.Width(20));
                if (newSelected) excludedPaths.Remove(path);
                else excludedPaths.Add(path);
                EditorGUILayout.LabelField(path + "  [" + (material != null && material.shader != null ? material.shader.name : "missing shader") + "]" + (detectionReasons.ContainsKey(path) ? " — " + detectionReasons[path] : ""), EditorStyles.wordWrappedMiniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private string SelectedFolder()
    {
        string path = folder == null ? null : AssetDatabase.GetAssetPath(folder);
        return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) &&
               (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal))
            ? path : null;
    }

    private void Preview()
    {
        candidates.Clear();
        excludedPaths.Clear();
        detectionReasons.Clear();
        string path = SelectedFolder();
        if (path == null) { report = "Select a valid folder inside Assets."; return; }
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { report = "URP/Lit shader unavailable. Verify URP installation and pipeline configuration."; return; }
        string[] tokens = alphaNameTokens.Split(',')
            .Select(token => token.Trim()).Where(token => token.Length > 0).ToArray();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { path }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null) continue;
            if (alphaClipMode)
            {
                if (material.shader != lit || !material.HasProperty("_AlphaClip")) continue;
                if (detectTextureAlpha)
                {
                    string reason;
                    bool strong;
                    if (!InspectAlpha(material, out reason, out strong)) continue;
                    detectionReasons[assetPath] = reason;
                    if (!strong && !includeAmbiguousAlpha) excludedPaths.Add(assetPath);
                }
                else if (tokens.Length == 0 ||
                    !tokens.Any(token => Path.GetFileNameWithoutExtension(assetPath).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
            }
            else if (!includeAlreadyLit && material.shader == lit) continue;
            candidates.Add(assetPath);
        }
        candidates.Sort(StringComparer.Ordinal);
        report = candidates.Count + " materials previewed in " + path + ". " + excludedPaths.Count + " excluded by default. No changes made.";
    }

    // Uses a temporary GPU-readable copy, without changing texture import settings.
    // Only the main albedo texture is sampled: alpha in other maps is ambiguous.
    private static bool InspectAlpha(Material material, out string reason, out bool strong)
    {
        reason = "";
        strong = false;
        if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
        {
            reason = "Already alpha clipped";
            return false;
        }

        Texture texture = FirstTexture(material, "_BaseMap", "_MainTex", "_BaseColorMap");
        if (texture == null)
        {
            reason = "No base texture (manual review)";
            return true;
        }

        RenderTexture temporary = null;
        Texture2D sample = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            temporary = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, temporary);
            RenderTexture.active = temporary;
            sample = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            sample.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            sample.Apply(false, false);
            Color32[] pixels = sample.GetPixels32();
            int transparent = 0, semiTransparent = 0, opaque = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a <= 25) transparent++;
                else if (pixel.a >= 245) opaque++;
                else semiTransparent++;
            }
            int total = pixels.Length;
            float transparentRatio = transparent / (float)total;
            float semiRatio = semiTransparent / (float)total;
            float opaqueRatio = opaque / (float)total;
            reason = "Alpha sample: " + Mathf.RoundToInt(transparentRatio * 100f) +
                "% transparent, " + Mathf.RoundToInt(semiRatio * 100f) +
                "% semi-transparent, " + Mathf.RoundToInt(opaqueRatio * 100f) + "% opaque";
            // Clearly cut-out imagery has both nearly transparent and visible
            // pixels. Border filtering may produce some intermediate alpha.
            if (transparentRatio >= 0.01f && opaqueRatio >= 0.01f)
            {
                strong = true;
                return true;
            }
            if (transparent > 0 || semiTransparent > 0)
            {
                reason += " (ambiguous; review before applying)";
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            reason = "Texture could not be sampled: " + e.GetType().Name + " (manual review)";
            return true;
        }
        finally
        {
            RenderTexture.active = previous;
            if (sample != null) UnityEngine.Object.DestroyImmediate(sample);
            if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private void ApplyAlphaClipping()
    {
        List<string> selected = candidates.Where(path => !excludedPaths.Contains(path)).ToList();
        string root = SelectedFolder();
        if (root == null || selected.Count == 0) return;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { report = "URP/Lit shader unavailable."; return; }
        if (!EditorUtility.DisplayDialog("Enable Alpha Clipping?",
            "Enable alpha clipping on " + selected.Count + " selected URP/Lit materials? " +
            "This changes shared materials. Verify alpha textures and commit a backup first.",
            "Apply", "Cancel")) return;
        int changed = 0, skipped = 0;
        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                string path = selected[i];
                if (!path.StartsWith(root + "/", StringComparison.Ordinal)) { skipped++; continue; }
                if (EditorUtility.DisplayCancelableProgressBar("Alpha Clipping", path, (float)i / selected.Count)) break;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader != lit || !material.HasProperty("_AlphaClip") ||
                    !material.HasProperty("_Cutoff")) { skipped++; continue; }
                if (material.GetFloat("_AlphaClip") > 0.5f &&
                    Mathf.Approximately(material.GetFloat("_Cutoff"), alphaThreshold)) { skipped++; continue; }
                Undo.RecordObject(material, "Enable Alpha Clipping");
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", alphaThreshold);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                changed++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        report = "Alpha clipping updated: " + changed + "; skipped: " + skipped + ". Inspect foliage and shadows.";
        Debug.Log("[URP Lit Converter] " + report);
        candidates.Clear();
        excludedPaths.Clear();
    }

    private void ConvertPreviewed()
    {
        List<string> selectedPaths = candidates.Where(path => !excludedPaths.Contains(path)).ToList();
        if (selectedPaths.Count == 0) return;
        string folderPath = SelectedFolder();
        if (folderPath == null) { candidates.Clear(); report = "Invalid folder; preview again."; return; }
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { report = "URP/Lit shader not found."; return; }
        if (!EditorUtility.DisplayDialog("Convert materials?",
            "Change the shader of " + selectedPaths.Count + " selected material assets under " + folderPath +
            " to URP/Lit? Shared materials will change in every scene/prefab. Commit or back up the project first. Some HDRP/Shader Graph properties may not transfer.",
            "Convert", "Cancel")) return;

        int changed = 0, skipped = 0;
        try
        {
            for (int i = 0; i < selectedPaths.Count; i++)
            {
                string path = selectedPaths[i];
                if (!path.StartsWith(folderPath + "/", StringComparison.Ordinal) &&
                    path != folderPath) { skipped++; continue; }
                if (EditorUtility.DisplayCancelableProgressBar("URP/Lit conversion", path, (float)i / selectedPaths.Count))
                    break;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || (material.shader == lit && !includeAlreadyLit)) { skipped++; continue; }

                // Capture common aliases before changing shader. Unsupported properties
                // remain in serialized data but cannot be guaranteed to render identically.
                Texture albedo = FirstTexture(material, "_BaseMap", "_BaseColorMap", "_MainTex");
                Texture normal = FirstTexture(material, "_BumpMap", "_NormalMap");
                Texture emission = FirstTexture(material, "_EmissionMap", "_EmissiveColorMap");
                Color baseColor = FirstColor(material, Color.white, "_BaseColor", "_Color");
                Color emissionColor = FirstColor(material, Color.black, "_EmissionColor", "_EmissiveColor");
                float metallic = FirstFloat(material, 0f, "_Metallic");
                float smoothness = FirstFloat(material, 0.5f, "_Smoothness", "_Glossiness");

                Undo.RecordObject(material, "Convert material to URP Lit");
                material.shader = lit;
                if (albedo != null) material.SetTexture("_BaseMap", albedo);
                material.SetColor("_BaseColor", baseColor);
                if (normal != null) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
                if (emission != null) material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", emissionColor);
                if (emission != null || emissionColor.maxColorComponent > 0f)
                    material.EnableKeyword("_EMISSION");
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                changed++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        report = "Converted " + changed + " materials; skipped " + skipped + ". Inspect appearance in Unity. Undo may be available in this editor session; Git is the reliable rollback.";
        Debug.Log("[URP Lit Converter] " + report);
        candidates.Clear();
        excludedPaths.Clear();
    }

    private static Texture FirstTexture(Material m, params string[] names)
    {
        foreach (string n in names)
            if (m.HasProperty(n) && m.GetTexture(n) != null) return m.GetTexture(n);
        return null;
    }
    private static Color FirstColor(Material m, Color fallback, params string[] names)
    {
        foreach (string n in names)
            if (m.HasProperty(n)) return m.GetColor(n);
        return fallback;
    }
    private static float FirstFloat(Material m, float fallback, params string[] names)
    {
        foreach (string n in names)
            if (m.HasProperty(n)) return m.GetFloat(n);
        return fallback;
    }
}
