using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    public int maxHealth = 30;
    [SerializeField]
    private int currentHealth;

    public int CurrentHealth => currentHealth;

    // Se déclenche quand cette entité meurt.
    // Utile plus tard pour : XP, drop de carte, effet visuel, etc.
    public UnityEvent OnDeath;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }
}