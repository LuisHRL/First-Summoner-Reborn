using UnityEngine;
using UnityEngine.UI;

public class TargetHealthBarUI : MonoBehaviour
{
    public PlayerTargeting playerTargeting;
    public Image healthBarFill;
    public AnimatedFillEffect healthAnimation;

    void Awake()
    {
        // Prefab assets cannot serialize references to scene objects. Preserve
        // a scene-assigned reference, and resolve it only when missing.
        if (playerTargeting == null)
            playerTargeting = FindFirstObjectByType<PlayerTargeting>();
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
        if (healthAnimation != null) healthAnimation.SetValue(value);
        else healthBarFill.fillAmount = value;
    }
}