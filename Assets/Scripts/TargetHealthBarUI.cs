using UnityEngine;
using UnityEngine.UI;

public class TargetHealthBarUI : MonoBehaviour
{
    public PlayerTargeting playerTargeting;
    public Image healthBarFill;

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

        healthBarFill.fillAmount = Mathf.Clamp01(
            (float)target.CurrentHealth /
            Mathf.Max(1, target.maxHealth)
        );
    }
}