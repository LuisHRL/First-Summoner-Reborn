#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Read-only diagnostics for all lights in the active scene, including generated torches.</summary>
public static class SceneLightAudit
{
    [MenuItem("Tools/First Summoner/Audit Scene Lights and URP")]
    public static void Run()
    {
        Light[] lights = Resources.FindObjectsOfTypeAll<Light>()
            .Where(l => l != null && l.gameObject.scene.IsValid() && l.gameObject.scene.isLoaded)
            .OrderBy(l => l.gameObject.scene.path).ThenBy(l => GetPath(l.transform)).ToArray();
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Diagnostics");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "scene_light_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        var sb = new StringBuilder();
        sb.AppendLine("Record,Scene,Hierarchy,Type,ActiveInHierarchy,Enabled,Mode,Intensity,Range,Color,Shadows,ShadowStrength,ShadowResolution,CullingMask,RenderPipeline,AdditionalLightsMode,AdditionalLightsPerObjectLimit,Notes");
        var pipeline = GraphicsSettings.currentRenderPipeline;
        string pipelineName = pipeline == null ? "BuiltIn" : pipeline.GetType().FullName;
        string additionalMode = "NotAvailable";
        string perObjectLimit = "NotAvailable";
        if (pipeline != null)
        {
            // Reflection keeps this diagnostic independent of URP package versions.
            var type = pipeline.GetType();
            var modeProperty = type.GetProperty("additionalLightsRenderingMode");
            var limitProperty = type.GetProperty("maxAdditionalLightsCount");
            if (modeProperty != null) additionalMode = Convert.ToString(modeProperty.GetValue(pipeline, null));
            if (limitProperty != null) perObjectLimit = Convert.ToString(limitProperty.GetValue(pipeline, null));
        }
        foreach (Light light in lights)
        {
            string notes = "";
            if (!light.gameObject.activeInHierarchy || !light.enabled) notes = "LightInactive";
            else if (light.type != LightType.Directional && light.range <= 0f) notes = "ZeroRange";
            sb.AppendLine(string.Join(",", new string[] {
                "Light", Csv(light.gameObject.scene.path), Csv(GetPath(light.transform)),
                Csv(light.type.ToString()), Csv(light.gameObject.activeInHierarchy.ToString()),
                Csv(light.enabled.ToString()), Csv(light.lightmapBakeType.ToString()),
                Csv(light.intensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(light.range.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(ColorUtility.ToHtmlStringRGB(light.color)), Csv(light.shadows.ToString()),
                Csv(light.shadowStrength.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(light.shadowResolution.ToString()), Csv(light.cullingMask.ToString()),
                Csv(pipelineName), Csv(additionalMode), Csv(perObjectLimit), Csv(notes)
            }));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        Debug.Log("[SceneLightAudit] " + lights.Length + " scene lights. CSV: " + path);
        EditorUtility.RevealInFinder(path);
    }

    private static string GetPath(Transform t)
    {
        string result = t.name;
        while (t.parent != null) { t = t.parent; result = t.name + "/" + result; }
        return result;
    }

    private static string Csv(string s)
    {
        return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    }
}
#endif
