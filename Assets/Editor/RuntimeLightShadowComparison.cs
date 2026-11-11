#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reversible runtime A/B test: removes shadows from active punctual lights
/// without changing light intensity, range, URP assets, or prefab assets.
/// </summary>
[InitializeOnLoad]
public static class RuntimeLightShadowComparison
{
    private static readonly Dictionary<Light, LightShadows> originals = new Dictionary<Light, LightShadows>();
    private static bool comparisonActive;

    static RuntimeLightShadowComparison()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
                Restore();
        };
    }

    [MenuItem("Tools/First Summoner/Halo Test/1 - Disable Punctual Shadows (Play Mode)")]
    private static void DisableShadows()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Halo Test] Enter Play Mode and generate the terrain first.");
            return;
        }

        if (comparisonActive)
            Restore();

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light == null || !light.enabled || light.type == LightType.Directional) continue;
            originals[light] = light.shadows;
            light.shadows = LightShadows.None;
        }

        comparisonActive = true;
        Debug.Log("[Halo Test] Shadows disabled on " + originals.Count +
                  " punctual lights. All lights remain enabled. Compare ground halos now.");
    }

    [MenuItem("Tools/First Summoner/Halo Test/2 - Restore Punctual Shadows")]
    private static void RestoreMenu() { Restore(); }

    private static void Restore()
    {
        foreach (var item in originals)
            if (item.Key != null) item.Key.shadows = item.Value;
        int count = originals.Count;
        originals.Clear();
        comparisonActive = false;
        if (count > 0) Debug.Log("[Halo Test] Restored shadow settings for " + count + " lights.");
    }
}
#endif