using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps illumination on all managed lights but grants realtime shadows only to
/// the nearest lights. Attach to a parent containing the lights to manage.
/// </summary>
[DisallowMultipleComponent]
public sealed class AdaptiveLightShadows : MonoBehaviour
{
    [Tooltip("Usually the player. If empty, the active main camera is used.")]
    public Transform observer;
    [Min(0)] public int maximumShadowCastingLights = 2;
    [Min(0f)] public float maximumShadowDistance = 18f;
    [Min(0.05f)] public float refreshInterval = 0.25f;
    public LightShadows nearbyShadowMode = LightShadows.Hard;
    public UnityEngine.Rendering.LightShadowResolution nearbyShadowResolution =
        UnityEngine.Rendering.LightShadowResolution.Low;

    private readonly List<Light> managedLights = new List<Light>();
    private readonly List<Light> candidates = new List<Light>();
    private readonly Dictionary<Light, LightShadows> originalModes = new Dictionary<Light, LightShadows>();
    private readonly Dictionary<Light, UnityEngine.Rendering.LightShadowResolution> originalResolutions =
        new Dictionary<Light, UnityEngine.Rendering.LightShadowResolution>();
    private float nextRefresh;

    private void OnEnable()
    {
        RefreshLights();
        Apply();
    }

    public void RefreshLights()
    {
        foreach (Light light in GetComponentsInChildren<Light>(true))
        {
            if (light == null || light.type == LightType.Directional) continue;
            if (!originalModes.ContainsKey(light))
            {
                originalModes.Add(light, light.shadows);
                originalResolutions.Add(light, light.shadowResolution);
            }
            if (!managedLights.Contains(light)) managedLights.Add(light);
        }
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        Apply();
    }

    private void Apply()
    {
        Transform view = observer;
        if (view == null && Camera.main != null) view = Camera.main.transform;
        if (view == null) return;
        Vector3 origin = view.position;
        float maxSqr = maximumShadowDistance * maximumShadowDistance;
        candidates.Clear();
        for (int i = managedLights.Count - 1; i >= 0; i--)
        {
            Light light = managedLights[i];
            if (light == null) { managedLights.RemoveAt(i); continue; }
            if (!light.enabled || !light.gameObject.activeInHierarchy) continue;
            if (originalModes[light] == LightShadows.None) continue;
            if ((light.transform.position - origin).sqrMagnitude <= maxSqr)
                candidates.Add(light);
        }
        candidates.Sort((a, b) =>
            (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
        var selected = new HashSet<Light>();
        for (int i = 0; i < candidates.Count && i < maximumShadowCastingLights; i++)
            selected.Add(candidates[i]);
        foreach (Light light in managedLights)
        {
            if (light == null) continue;
            bool cast = selected.Contains(light);
            light.shadows = cast ? nearbyShadowMode : LightShadows.None;
            if (cast) light.shadowResolution = nearbyShadowResolution;
        }
    }

    private void OnDisable()
    {
        foreach (var entry in originalModes)
        {
            if (entry.Key == null) continue;
            entry.Key.shadows = entry.Value;
            entry.Key.shadowResolution = originalResolutions[entry.Key];
        }
    }
}
