using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    public int CurrentHealth => currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log("Vie du joueur : " + currentHealth);

        if (currentHealth <= 0)
        {
            Debug.Log("Game Over");
            Destroy(gameObject);
        }
    }
}