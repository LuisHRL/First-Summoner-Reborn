using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    [Header("Barre de vie")]
    public Image healthBarFill;

    private PlayerHealth playerHealth;
    private MinionHealth minionHealth;
    private Camera mainCamera;

    private int previousHealth;
    private bool isVisible;

    void Start()
    {
        playerHealth = GetComponentInParent<PlayerHealth>();
        minionHealth = GetComponentInParent<MinionHealth>();
        mainCamera = Camera.main;

        previousHealth = GetCurrentHealth();
        isVisible = false;

        if (healthBarFill != null)
            healthBarFill.fillAmount = 1f;

        SetBarVisible(false);
    }

    void LateUpdate()
    {
        int currentHealth = GetCurrentHealth();

        // Afficher la barre dès le premier dégât.
        if (!isVisible && currentHealth < previousHealth)
        {
            isVisible = true;
            SetBarVisible(true);
        }

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = Mathf.Clamp01(
                (float)currentHealth / Mathf.Max(1, GetMaxHealth())
            );
        }

        previousHealth = currentHealth;

        // Garder la barre face à la caméra.
        if (mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }

    int GetCurrentHealth()
    {
        if (playerHealth != null)
            return playerHealth.CurrentHealth;

        if (minionHealth != null)
            return minionHealth.CurrentHealth;

        return 0;
    }

    int GetMaxHealth()
    {
        if (playerHealth != null)
            return playerHealth.maxHealth;

        if (minionHealth != null)
            return minionHealth.maxHealth;

        return 1;
    }

    void SetBarVisible(bool visible)
    {
        if (healthBarFill != null)
            healthBarFill.transform.parent.gameObject.SetActive(visible);
    }
}