using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime-only day/night lighting controller. Uses existing scene lights and skybox.
/// Does not create lights, skyboxes, volumes, or modify project assets.
/// </summary>
[DisallowMultipleComponent]
public sealed class ForestDayNightLighting : MonoBehaviour
{
    public enum LightingTime { Day, Night }

    [Header("Existing scene light")]
    [Tooltip("Assign the existing Directional Light. No new light is created.")]
    [SerializeField] private Light directionalLight;

    [Header("Initial state")]
    [SerializeField] private LightingTime initialTime = LightingTime.Night;

    [Header("Day")]
    [Min(0f)] [SerializeField] private float dayDirectionalIntensity = 1f;
    [SerializeField] private Color dayDirectionalColor = Color.white;
    [SerializeField] private Color dayAmbientColor = new Color(0.65f, 0.67f, 0.7f);

    [Header("Night")]
    [Min(0f)] [SerializeField] private float nightDirectionalIntensity = 0.18f;
    [SerializeField] private Color nightDirectionalColor = new Color(0.67f, 0.76f, 1f);
    [SerializeField] private Color nightAmbientColor = new Color(0.12f, 0.16f, 0.22f);

    [Header("Runtime controls")]
    [SerializeField] private bool applyOnEnable = true;

    private bool saved;
    private AmbientMode previousAmbientMode;
    private Color previousAmbientColor;
    private float previousIntensity;
    private Color previousLightColor;
    private bool previousLightEnabled;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        SaveOriginalState();
        if (applyOnEnable) Apply(initialTime);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || !saved) return;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientLight = previousAmbientColor;
        if (directionalLight != null)
        {
            directionalLight.intensity = previousIntensity;
            directionalLight.color = previousLightColor;
            directionalLight.enabled = previousLightEnabled;
        }
        DynamicGI.UpdateEnvironment();
        saved = false;
    }

    private void SaveOriginalState()
    {
        if (saved) return;
        previousAmbientMode = RenderSettings.ambientMode;
        previousAmbientColor = RenderSettings.ambientLight;
        if (directionalLight != null)
        {
            previousIntensity = directionalLight.intensity;
            previousLightColor = directionalLight.color;
            previousLightEnabled = directionalLight.enabled;
        }
        saved = true;
    }

    public void SetDay() => Apply(LightingTime.Day);
    public void SetNight() => Apply(LightingTime.Night);

    public void Apply(LightingTime time)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ForestDayNightLighting] Enter Play Mode to preview lighting without changing scene settings.", this);
            return;
        }
        if (!saved) SaveOriginalState();

        bool night = time == LightingTime.Night;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = night ? nightAmbientColor : dayAmbientColor;

        if (directionalLight != null)
        {
            directionalLight.enabled = true;
            directionalLight.intensity = night ? nightDirectionalIntensity : dayDirectionalIntensity;
            directionalLight.color = night ? nightDirectionalColor : dayDirectionalColor;
        }
        else
        {
            Debug.LogWarning("[ForestDayNightLighting] Assign an existing Directional Light to control moon/sun lighting.", this);
        }

        // Keep the existing skybox and volume unchanged.
        DynamicGI.UpdateEnvironment();
    }

    [ContextMenu("Switch to Day (Play Mode)")]
    private void PreviewDay() => SetDay();

    [ContextMenu("Switch to Night (Play Mode)")]
    private void PreviewNight() => SetNight();
}
