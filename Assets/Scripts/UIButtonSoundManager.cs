
using UnityEngine;

public class UIButtonSoundManager : MonoBehaviour
{
    public static UIButtonSoundManager Instance { get; private set; }

    [Header("Button Sounds")]
    public AudioClip decisionSound;
    public AudioClip selectSound;
    public AudioClip veryDecisionSound;
    public AudioClip popupSound;
    public AudioClip systemMessageSound;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public void PlayDecision() => PlaySound(decisionSound);
    public void PlaySelect() => PlaySound(selectSound);
    public void PlayVeryDecision() => PlaySound(veryDecisionSound);
    public void PlayPopup() => PlaySound(popupSound);
    public void PlaySystemMessage() => PlaySound(systemMessageSound);

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}
