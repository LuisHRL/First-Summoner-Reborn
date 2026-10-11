using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Déplacement")]
    public float speed = 2.5f;

    [Header("Combat")]
    public float attackRange = 1.4f;
    public int damage = 5;
    public float attackCooldown = 1f;

    [Header("Aggro joueur")]
    public float playerAggroRange = 1.6f;
    public float playerLoseRange = 8f;

    [Header("Aggro minion")]
    public float minionAggroRange = 5f;

    [Header("Mémoire de l'attaquant")]
    public float attackerMemoryDuration = 10f;

    private PlayerHealth player;

    // La cible actuelle de l'ennemi.
    private Transform currentTarget;

    // Temps jusqu'auquel l'ennemi se souvient
    // de son attaquant à distance.
    private float attackerMemoryEndTime;

    private float nextAttackTime;

    void Start()
    {
        player = FindAnyObjectByType<PlayerHealth>();
    }

    void Update()
    {
        // On récupère le joueur s'il n'a pas encore été trouvé.
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerHealth>();

            if (player == null)
                return;
        }

        UpdateTarget();

        if (currentTarget == null)
            return;

        MoveAndAttack();
    }

    // ---------------------------------------------------------
    // GESTION DE LA CIBLE
    // ---------------------------------------------------------

    void UpdateTarget()
    {
        // Si la cible actuelle existe toujours et est encore valide,
        // on la conserve.
        if (IsTargetValid())
        {
            // Si nous sommes actuellement en mémoire d'un attaquant,
            // nous vérifions si cette mémoire est expirée.
            if (Time.time >= attackerMemoryEndTime)
            {
                // La mémoire est terminée.
                // On abandonne la cible et on repart sur
                // le comportement normal.
                currentTarget = null;
            }
            else
            {
                return;
            }
        }

        // -----------------------------------------------------
        // COMPORTEMENT NORMAL
        // -----------------------------------------------------

        // 1. Cherche d'abord un minion proche.
        MinionHealth closestMinion = FindClosestMinion();

        if (closestMinion != null)
        {
            currentTarget = closestMinion.transform;
            return;
        }

        // 2. Sinon, regarde si le joueur est assez proche.
        if (player != null)
        {
            float playerDistance = Vector3.Distance(
                transform.position,
                player.transform.position
            );

            if (playerDistance <= playerAggroRange)
            {
                currentTarget = player.transform;
                return;
            }
        }

        // Aucune cible.
        currentTarget = null;
    }

    // ---------------------------------------------------------
    // MÉMOIRE DE L'ATTAQUANT
    // ---------------------------------------------------------

    public void AlertedBy(Transform attacker)
    {
        if (attacker == null)
            return;

        // On mémorise l'attaquant.
        currentTarget = attacker;

        // L'ennemi se souviendra de cette cible pendant
        // attackerMemoryDuration secondes.
        attackerMemoryEndTime =
            Time.time + attackerMemoryDuration;
    }

    // ---------------------------------------------------------
    // VALIDATION DE LA CIBLE
    // ---------------------------------------------------------

    bool IsTargetValid()
    {
        if (currentTarget == null)
            return false;

        // Vérifie si la cible est toujours présente dans la scène.
        if (!currentTarget.gameObject.activeInHierarchy)
            return false;

        // Vérifie si la cible possède encore une santé valide.

        PlayerHealth targetPlayer =
            currentTarget.GetComponent<PlayerHealth>();

        if (targetPlayer != null)
        {
            return targetPlayer.CurrentHealth > 0;
        }

        MinionHealth targetMinion =
            currentTarget.GetComponent<MinionHealth>();

        if (targetMinion != null)
        {
            return targetMinion.CurrentHealth > 0;
        }

        return false;
    }

    // ---------------------------------------------------------
    // RECHERCHE DU MINION LE PLUS PROCHE
    // ---------------------------------------------------------

    MinionHealth FindClosestMinion()
    {
        MinionHealth[] minions =
            FindObjectsByType<MinionHealth>();

        MinionHealth closest = null;

        float closestDistance = minionAggroRange;

        foreach (MinionHealth minion in minions)
        {
            if (minion == null)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                minion.transform.position
            );

            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closest = minion;
            }
        }

        return closest;
    }

    // ---------------------------------------------------------
    // DÉPLACEMENT ET ATTAQUE
    // ---------------------------------------------------------

    void MoveAndAttack()
    {
        if (currentTarget == null)
            return;

        Vector3 direction =
            currentTarget.position - transform.position;

        direction.y = 0;

        float distance = direction.magnitude;

        // L'ennemi est trop loin.
        if (distance > attackRange)
        {
            Vector3 movement =
                direction.normalized *
                speed *
                Time.deltaTime;

            transform.position += movement;

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward =
                    direction.normalized;
            }

            return;
        }

        // L'ennemi est à portée d'attaque.
        if (Time.time >= nextAttackTime)
        {
            AttackTarget();

            nextAttackTime =
                Time.time + attackCooldown;
        }
    }

    // ---------------------------------------------------------
    // ATTAQUE
    // ---------------------------------------------------------

    void AttackTarget()
    {
        if (currentTarget == null)
            return;

        PlayerHealth targetPlayer =
            currentTarget.GetComponent<PlayerHealth>();

        if (targetPlayer != null)
        {
            targetPlayer.TakeDamage(damage);
            return;
        }

        MinionHealth targetMinion =
            currentTarget.GetComponent<MinionHealth>();

        if (targetMinion != null)
        {
            targetMinion.TakeDamage(damage);
        }
    }
}