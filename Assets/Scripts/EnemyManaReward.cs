using UnityEngine;

// A poser sur le même GameObject que le composant EnemyHealth
// (donc sur ton prefab d'ennemi basique).
[RequireComponent(typeof(Health))]
public class EnemyManaReward : MonoBehaviour
{
    public float manaReward = 0.4f;

    void Start()
    {
        Health health = GetComponent<Health>();

        // On demande à Health de nous prévenir quand cet ennemi meurt.
        health.OnDeath.AddListener(RewardMana);
    }

    void RewardMana()
    {
        // On cherche le ManaSystem au moment de la mort,
        // pas à l'avance : ça marche donc peu importe la scène.
        ManaSystem manaSystem = FindAnyObjectByType<ManaSystem>();

        if (manaSystem != null)
        {
            manaSystem.GainMana(manaReward);
        }
    }
}

