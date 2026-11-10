#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only sampling of punctual-light coverage on terrain surfaces.
/// Computes geometric reach only; it does not claim to measure GPU pixel lighting.
/// </summary>
public static class TerrainLightSamplingAudit
{
    [MenuItem("Tools/First Summoner/Audit Terrain Light Samples")]
    public static void Run()
    {
        var lights = Resources.FindObjectsOfTypeAll<Light>()
            .Where(l => l != null && l.gameObject.scene.IsValid() && l.gameObject.scene.isLoaded
                && l.enabled && l.gameObject.activeInHierarchy && l.type != LightType.Directional)
            .OrderBy(l => PathOf(l.transform)).ToArray();
        var terrains = Resources.FindObjectsOfTypeAll<Terrain>()
            .Where(t => t != null && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded
                && t.enabled && t.gameObject.activeInHierarchy && t.terrainData != null).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("Scene,Light,Sample,Terrain,SampleX,SampleY,SampleZ,Distance,Range,InRange,ActiveOverlappingLights,Notes");
        foreach (var light in lights)
        {
            foreach (var terrain in terrains)
            {
                if (light.gameObject.scene != terrain.gameObject.scene) continue;
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;
                var lightPos = light.transform.position;
                float x = Mathf.Clamp(lightPos.x, origin.x, origin.x + size.x);
                float z = Mathf.Clamp(lightPos.z, origin.z, origin.z + size.z);
                // Sample ground under the light and at four offsets around it.
                Vector2[] offsets = { Vector2.zero, Vector2.right * 2f, Vector2.left * 2f,
                                      Vector2.up * 2f, Vector2.down * 2f };
                for (int i = 0; i < offsets.Length; i++)
                {
                    float sx = Mathf.Clamp(x + offsets[i].x, origin.x, origin.x + size.x);
                    float sz = Mathf.Clamp(z + offsets[i].y, origin.z, origin.z + size.z);
                    Vector3 sample = new Vector3(sx, 0f, sz);
                    sample.y = terrain.SampleHeight(sample) + origin.y;
                    float distance = Vector3.Distance(lightPos, sample);
                    int overlap = lights.Count(other => other.gameObject.scene == light.gameObject.scene &&
                        Vector3.Distance(other.transform.position, sample) <= other.range);
                    string[] fields = {
                        light.gameObject.scene.path, PathOf(light.transform), i.ToString(),
                        PathOf(terrain.transform), N(sx), N(sample.y), N(sz), N(distance),
                        N(light.range), (distance <= light.range).ToString(), overlap.ToString(),
                        "GeometricEstimateNotRenderedPixelMeasurement"
                    };
                    sb.AppendLine(string.Join(",", fields.Select(Csv).ToArray()));
                }
            }
        }
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Diagnostics");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "terrain_light_samples_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
        Debug.Log("[TerrainLightSamplingAudit] Sampled " + lights.Length + " lights across " + terrains.Length + " terrains. " + file);
        EditorUtility.RevealInFinder(file);
    }
    private static string N(float n) { return n.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
    private static string PathOf(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
#endif