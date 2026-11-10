#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only diagnostic of scene light positions relative to Unity Terrains.</summary>
public static class TerrainLightCoverageAudit
{
    [MenuItem("Tools/First Summoner/Audit Terrain Light Coverage")]
    public static void Run()
    {
        var lights = Resources.FindObjectsOfTypeAll<Light>()
            .Where(l => l != null && l.gameObject.scene.IsValid() && l.gameObject.scene.isLoaded)
            .OrderBy(l => l.gameObject.scene.path).ThenBy(l => Hierarchy(l.transform)).ToArray();
        var terrains = Resources.FindObjectsOfTypeAll<Terrain>()
            .Where(t => t != null && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded && t.terrainData != null).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("Scene,LightHierarchy,LightType,LightActive,LightEnabled,LightWorldX,LightWorldY,LightWorldZ,Range,Intensity,TerrainHierarchy,TerrainActive,TerrainEnabled,TerrainWorldX,TerrainWorldY,TerrainWorldZ,TerrainSurfaceY,LightAboveSurface,HorizontalInsideTerrain,DistanceToTerrainSurface,WithinLightRange,TerrainDrawInstanced,TerrainMaterial,Notes");
        foreach (var light in lights)
        {
            var p = light.transform.position;
            bool found = false;
            foreach (var terrain in terrains)
            {
                if (terrain.gameObject.scene != light.gameObject.scene) continue;
                var o = terrain.transform.position;
                var size = terrain.terrainData.size;
                bool inside = p.x >= o.x && p.x <= o.x + size.x && p.z >= o.z && p.z <= o.z + size.z;
                if (!inside) continue;
                found = true;
                float y = terrain.SampleHeight(p) + o.y;
                float height = p.y - y;
                float distance = Mathf.Abs(height);
                string note = height < 0f ? "LightBelowSurface" : (distance > light.range ? "SurfaceOutsideRange" : "");
                WriteRow(sb, light, terrain, y, height, true, distance, distance <= light.range, note);
            }
            if (!found) WriteRow(sb, light, null, float.NaN, float.NaN, false, float.NaN, false, "NoTerrainBelowLight");
        }
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Diagnostics");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "terrain_light_coverage_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
        Debug.Log("[TerrainLightCoverageAudit] " + lights.Length + " lights, " + terrains.Length + " terrains. " + file);
        EditorUtility.RevealInFinder(file);
    }

    private static void WriteRow(StringBuilder sb, Light l, Terrain t, float y, float height, bool inside, float distance, bool inRange, string note)
    {
        var p = l.transform.position;
        var o = t != null ? t.transform.position : Vector3.zero;
        string material = t != null && t.materialTemplate != null ? AssetDatabase.GetAssetPath(t.materialTemplate) : "";
        string[] fields = {
            l.gameObject.scene.path, Hierarchy(l.transform), l.type.ToString(),
            l.gameObject.activeInHierarchy.ToString(), l.enabled.ToString(),
            Num(p.x), Num(p.y), Num(p.z), Num(l.range), Num(l.intensity),
            t != null ? Hierarchy(t.transform) : "",
            t != null ? t.gameObject.activeInHierarchy.ToString() : "",
            t != null ? t.enabled.ToString() : "",
            t != null ? Num(o.x) : "", t != null ? Num(o.y) : "", t != null ? Num(o.z) : "",
            t != null ? Num(y) : "", t != null ? Num(height) : "",
            inside.ToString(), t != null ? Num(distance) : "", inRange.ToString(),
            t != null ? t.drawInstanced.ToString() : "", material, note
        };
        sb.AppendLine(string.Join(",", fields.Select(Csv).ToArray()));
    }
    private static string Num(float n) { return float.IsNaN(n) ? "" : n.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
    private static string Hierarchy(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
#endif