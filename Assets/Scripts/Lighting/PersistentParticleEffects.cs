using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps looping particle effects alive on procedurally instantiated objects.
/// Does not modify one-shot effects, emission rates, or light settings.
/// </summary>
[DisallowMultipleComponent]
public sealed class PersistentParticleEffects : MonoBehaviour
{
    [Min(0.1f)] public float checkInterval = 1f;
    [Tooltip("Avoid pausing persistent flames when their particle bounds leave the camera view.")]
    public bool alwaysSimulateLoopingEffects = true;

    private readonly List<ParticleSystem> loopingSystems = new List<ParticleSystem>();
    private float nextCheck;

    private void OnEnable()
    {
        Refresh();
        EnsurePlaying();
    }

    public void Refresh()
    {
        loopingSystems.Clear();
        foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particle == null || !particle.main.loop) continue;
            loopingSystems.Add(particle);
            if (alwaysSimulateLoopingEffects)
            {
                var main = particle.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }
        }
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + Mathf.Max(0.1f, checkInterval);
        EnsurePlaying();
    }

    private void EnsurePlaying()
    {
        foreach (ParticleSystem particle in loopingSystems)
        {
            if (particle == null || !particle.gameObject.activeInHierarchy) continue;
            if (!particle.emission.enabled) continue;
            if (particle.isStopped)
                particle.Play(true);
        }
    }
}
