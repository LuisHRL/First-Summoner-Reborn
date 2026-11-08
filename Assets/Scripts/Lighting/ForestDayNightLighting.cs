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
    [Header("Day ambient gradient")]
    [SerializeField] private Color daySkyColor = new Color(0.65f, 0.7f, 0.8f);
    [SerializeField] private Color dayEquatorColor = new Color(0.55f, 0.6f, 0.65f);
    [SerializeField] private Color dayGroundColor = new Color(0.38f, 0.4f, 0.42f);

    [Header("Night")]
    [Min(0f)] [SerializeField] private float nightDirectionalIntensity = 0.18f;
    [SerializeField] private Color nightDirectionalColor = new Color(0.67f, 0.76f, 1f);
    [Header("Night ambient gradient")]
    [Tooltip("Ambient light arriving from above.")]
    [SerializeField] private Color nightSkyColor = new Color(0.25f, 0.31f, 0.43f);
    [Tooltip("Ambient light arriving from the horizon.")]
    [SerializeField] private Color nightEquatorColor = new Color(0.17f, 0.21f, 0.29f);
    [Tooltip("Ambient light from below. Raise this gently if the ground is too dark.")]
    [SerializeField] private Color nightGroundColor = new Color(0.12f, 0.15f, 0.2f);

    [Header("Runtime controls")]
    [SerializeField] private bool applyOnEnable = true;

    private bool saved;
    private AmbientMode previousAmbientMode;
    private Color previousAmbientColor;
    private Color previousSkyColor;
    private Color previousEquatorColor;
    private Color previousGroundColor;
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
        RenderSettings.ambientSkyColor = previousSkyColor;
        RenderSettings.ambientEquatorColor = previousEquatorColor;
        RenderSettings.ambientGroundColor = previousGroundColor;
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
        previousSkyColor = RenderSettings.ambientSkyColor;
        previousEquatorColor = RenderSettings.ambientEquatorColor;
        previousGroundColor = RenderSettings.ambientGroundColor;
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
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = night ? nightSkyColor : daySkyColor;
        RenderSettings.ambientEquatorColor = night ? nightEquatorColor : dayEquatorColor;
        RenderSettings.ambientGroundColor = night ? nightGroundColor : dayGroundColor;

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
