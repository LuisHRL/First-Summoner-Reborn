
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider uiVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private const string MasterVolumeParameter = "MasterVolume";
    private const string MasterVolumePref = "Audio_MasterVolume";
    private const string MusicVolumeParameter = "MusicVolume";
    private const string MusicVolumePref = "Audio_MusicVolume";
    private const string UIVolumeParameter = "UIVolume";
    private const string UIVolumePref = "Audio_UIVolume";
    private const string SFXVolumeParameter = "SFXVolume";
    private const string SFXVolumePref = "Audio_SFXVolume";

    private void Start()
    {
        if (audioMixer == null || masterVolumeSlider == null)
        {
            Debug.LogWarning("AudioSettingsManager : Mixer ou Slider non assigné.");
            return;
        }

        float savedVolume = PlayerPrefs.GetFloat(MasterVolumePref, 1f);

        masterVolumeSlider.SetValueWithoutNotify(savedVolume);
        SetMasterVolume(savedVolume);

        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null)
        {
            float savedMusicVolume = PlayerPrefs.GetFloat(MusicVolumePref, 1f);

            musicVolumeSlider.SetValueWithoutNotify(savedMusicVolume);
            SetMusicVolume(savedMusicVolume);

            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (uiVolumeSlider != null)
        {
            float savedUIVolume = PlayerPrefs.GetFloat(UIVolumePref, 1f);

            uiVolumeSlider.SetValueWithoutNotify(savedUIVolume);
            SetUIVolume(savedUIVolume);

            uiVolumeSlider.onValueChanged.AddListener(SetUIVolume);
        }

        if (sfxVolumeSlider != null)
        {
            float savedSFXVolume = PlayerPrefs.GetFloat(SFXVolumePref, 1f);

            sfxVolumeSlider.SetValueWithoutNotify(savedSFXVolume);
            SetSFXVolume(savedSFXVolume);

            sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        }

    }

    public void SetMasterVolume(float volume)
    {
        if (audioMixer == null)
            return;

        // Convertit une valeur entre 0 et 1 en décibels.
        float decibels = volume <= 0.0001f
            ? -80f
            : Mathf.Log10(volume) * 20f;

        audioMixer.SetFloat(MasterVolumeParameter, decibels);

        PlayerPrefs.SetFloat(MasterVolumePref, volume);
    }

    public void SetMusicVolume(float volume)
    {
        if (audioMixer == null)
            return;

        // Convertit le volume (0 à 1) en décibels.
        float decibels = volume <= 0.0001f
            ? -80f
            : Mathf.Log10(volume) * 20f;

        audioMixer.SetFloat(MusicVolumeParameter, decibels);

        // Sauvegarde le volume choisi.
        PlayerPrefs.SetFloat(MusicVolumePref, volume);
    }

    public void SetUIVolume(float volume)
    {
        if (audioMixer == null)
            return;

        // Convertit le volume (0 à 1) en décibels.
        float decibels = volume <= 0.0001f
            ? -80f
            : Mathf.Log10(volume) * 20f;

        // Modifie uniquement le volume de l'interface.
        audioMixer.SetFloat(UIVolumeParameter, decibels);

        // Sauvegarde la préférence du joueur.
        PlayerPrefs.SetFloat(UIVolumePref, volume);
    }

    public void SetSFXVolume(float volume)
    {
        if (audioMixer == null)
            return;

        // Convertit le volume (0 à 1) en décibels.
        float decibels = volume <= 0.0001f
            ? -80f
            : Mathf.Log10(volume) * 20f;

        // Modifie uniquement le volume des effets sonores.
        audioMixer.SetFloat(SFXVolumeParameter, decibels);

        // Sauvegarde la préférence du joueur.
        PlayerPrefs.SetFloat(SFXVolumePref, volume);
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
    }
}
