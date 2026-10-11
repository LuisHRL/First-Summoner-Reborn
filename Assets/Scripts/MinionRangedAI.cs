using UnityEngine;

public class MinionRangedAI : MonoBehaviour
{
    [Header("Déplacement")]
    public float speed = 3f;
    public float lifetime = 20f;
    public float followDistance = 2f;

    [Header("Attaque à distance")]
    public float attackRange = 6f;
    public float attackCooldown = 1.2f;

    public GameObject projectilePrefab;
    public Transform firePoint;

    public AudioClip launchSound;

    private PlayerHealth player;
    private float nextAttackTime;

    void Start()
    {
        player =
            FindAnyObjectByType<PlayerHealth>();

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        EnemyHealth target =
            FindClosestEnemy();

        if (target != null)
        {
            FightEnemy(target);
        }
        else
        {
            FollowPlayer();
        }
    }

    // ---------------------------------------------------------
    // RECHERCHE DE L'ENNEMI LE PLUS PROCHE
    // ---------------------------------------------------------

    EnemyHealth FindClosestEnemy()
    {
        EnemyHealth[] enemies =
            FindObjectsByType<EnemyHealth>();

        EnemyHealth closest = null;

        float closestDistance =
            Mathf.Infinity;

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null)
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    enemy.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy;
            }
        }

        return closest;
    }

    // ---------------------------------------------------------
    // COMBAT
    // ---------------------------------------------------------

    void FightEnemy(EnemyHealth target)
    {
        if (target == null)
            return;

        Vector3 direction =
            target.transform.position -
            transform.position;

        direction.y = 0;

        float distance =
            direction.magnitude;

        // L'ennemi est trop loin.
        if (distance > attackRange)
        {
            transform.position +=
                direction.normalized *
                speed *
                Time.deltaTime;

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward =
                    direction.normalized;
            }

            return;
        }

        // L'ennemi est à portée.
        if (Time.time >= nextAttackTime)
        {
            ShootAt(target);

            nextAttackTime =
                Time.time + attackCooldown;
        }
    }

    // ---------------------------------------------------------
    // TIR
    // ---------------------------------------------------------

    void ShootAt(EnemyHealth target)
    {
        if (target == null)
            return;

        if (firePoint == null)
            return;

        Vector3 direction =
            target.transform.position -
            firePoint.position;

        direction.y = 0;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion projectileRotation =
            Quaternion.LookRotation(
                direction.normalized
            );

        if (launchSound != null)
        {
            AudioSource.PlayClipAtPoint(
                launchSound,
                firePoint.position
            );
        }

        // Création du projectile.
        GameObject projectileObject =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                projectileRotation
            );

        MagicProjectile projectile =
            projectileObject.GetComponent<MagicProjectile>();

        if (projectile != null)
        {
            // L'archer est le propriétaire
            // de cette flèche.
            projectile.owner = transform;
        }
    }

    // ---------------------------------------------------------
    // SUIVI DU JOUEUR
    // ---------------------------------------------------------

    void FollowPlayer()
    {
        if (player == null)
        {
            player =
                FindAnyObjectByType<PlayerHealth>();

            if (player == null)
                return;
        }

        Vector3 direction =
            player.transform.position -
            transform.position;

        direction.y = 0;

        if (direction.magnitude > followDistance)
        {
            transform.position +=
                direction.normalized *
                speed *
                Time.deltaTime;

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward =
                    direction.normalized;
            }
        }
    }
}
