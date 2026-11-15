using UnityEngine;
using UnityEngine.UI;

public class TargetHealthBarUI : MonoBehaviour
{
    public PlayerTargeting playerTargeting;
    public Image healthBarFill;
    public AnimatedFillEffect healthAnimation;
    public TargetHPDamageParticles damageParticles;

    [Header("Target HUD light (independent of enemy bars)")]
    public Color targetLightColor = Color.white;
    [Tooltip("Keep the HUD transition light at its original visual size when the health bar is enlarged.")]
    public bool compensateHudLightScale = true;

    void Awake()
    {
        // Prefab assets cannot serialize references to scene objects. Preserve
        // a scene-assigned reference, and resolve it only when missing.
        if (playerTargeting == null)
            playerTargeting = FindFirstObjectByType<PlayerTargeting>();

        // Preserve animation-owned alpha; only neutralize the HUD light tint.
        if (healthAnimation != null && healthAnimation.transitionLight != null)
        {
            var light = healthAnimation.transitionLight;
            var tint = targetLightColor;
            tint.a = light.color.a;
            light.color = tint;
            if (compensateHudLightScale && healthBarFill != null)
            {
                // The HUD visual parent is scaled to fit its 600x50 slot.
                // Its light should follow that scaled position, but not inherit
                // the same enlargement of its glow sprite.
                var visualRoot = healthBarFill.transform.parent;
                if (visualRoot != null && light.transform.IsChildOf(visualRoot))
                {
                    Vector3 visualScale = visualRoot.localScale;
                    if (Mathf.Abs(visualScale.x) > 0.0001f && Mathf.Abs(visualScale.y) > 0.0001f)
                    {
                        Vector3 original = light.rectTransform.localScale;
                        light.rectTransform.localScale = new Vector3(
                            original.x / Mathf.Abs(visualScale.x),
                            original.y / Mathf.Abs(visualScale.y), original.z);
                    }
                }
            }
        }
    }

    void Update()
    {
        if (playerTargeting == null || healthBarFill == null)
            return;

        EnemyHealth target = playerTargeting.CurrentTarget;

        bool hasTarget = target != null;

        // Masquer le fond et le remplissage si aucune cible.
        healthBarFill.transform.parent.gameObject.SetActive(hasTarget);

        if (!hasTarget)
            return;

        float value = Mathf.Clamp01((float)target.CurrentHealth / Mathf.Max(1, target.maxHealth));
        if (damageParticles != null) damageParticles.SetHealth(value);
        if (healthAnimation != null) healthAnimation.SetValue(value);
        else healthBarFill.fillAmount = value;
    }
}