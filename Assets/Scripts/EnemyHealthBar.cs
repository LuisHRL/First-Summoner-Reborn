
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("Références")]
    public EnemyHealth enemyHealth;
    public Image healthBarFill;

    [Header("Caméra")]
    public Camera mainCamera;

    void Start()
    {
        if (enemyHealth == null)
            enemyHealth = GetComponentInParent<EnemyHealth>();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (enemyHealth != null && healthBarFill != null)
        {
            float healthPercent = Mathf.Clamp01(
                (float)enemyHealth.CurrentHealth /
                Mathf.Max(1, enemyHealth.maxHealth)
            );

            healthBarFill.fillAmount = healthPercent;
        }

        // Garder la barre face à la caméra.
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
