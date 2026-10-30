
using UnityEngine;
using UnityEngine.Audio;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
            return;

        // Créer une source audio temporaire.
        GameObject soundObject = new GameObject("SFX_" + clip.name);
        soundObject.transform.position = position;

        AudioSource source = soundObject.AddComponent<AudioSource>();

        // Configurer le son.
        source.clip = clip;
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.volume = Mathf.Clamp01(volume);

        source.spatialBlend = 0f;

        source.playOnAwake = false;

        // Jouer le son.
        source.Play();

        // Supprimer l'objet après la lecture.
        Destroy(soundObject, clip.length + 0.1f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
