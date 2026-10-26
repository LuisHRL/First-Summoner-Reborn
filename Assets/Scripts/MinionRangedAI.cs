
using UnityEngine;

public class MinionRangedAI : MonoBehaviour
{
    [Header("Déplacement")]
    public float speed = 3f;
    public float followDistance = 2f;

    [Header("Attaque à distance")]
    public float attackRange = 6f;
    public float attackCooldown = 1.2f;

    public GameObject projectilePrefab;
    public Transform firePoint;
    public AudioClip launchSound;

    private PlayerHealth player;
    private PlayerTargeting playerTargeting;
    private CharacterController controller;
    private float nextAttackTime;

    void Start()
    {
        player = FindAnyObjectByType<PlayerHealth>();
        playerTargeting = FindAnyObjectByType<PlayerTargeting>();
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (playerTargeting == null)
        {
            playerTargeting =
                FindAnyObjectByType<PlayerTargeting>();
        }

        EnemyHealth target = null;

        if (playerTargeting != null)
        {
            target = playerTargeting.CurrentTarget;
        }

        if (target != null)
        {
            FightEnemy(target);
        }
        else
        {
            FollowPlayer();
        }
    }

    void FightEnemy(EnemyHealth target)
    {
        Vector3 direction =
            target.transform.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance > attackRange)
        {
            controller.Move(
                direction.normalized * speed * Time.deltaTime
            );

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward = direction.normalized;
            }

            return;
        }

        if (Time.time >= nextAttackTime)
        {
            ShootAt(target);
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void ShootAt(EnemyHealth target)
    {
        if (target == null || firePoint == null)
            return;

        // Viser le centre du collider de l'ennemi.
        Collider targetCollider =
            target.GetComponentInChildren<Collider>();

        Vector3 targetPosition = targetCollider != null
            ? targetCollider.bounds.center
            : target.transform.position;

        // Conserver la différence de hauteur.
        Vector3 direction =
            targetPosition - firePoint.position;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion projectileRotation =
            Quaternion.LookRotation(direction.normalized);

        if (launchSound != null)
        {
            AudioSource.PlayClipAtPoint(
                launchSound,
                firePoint.position
            );
        }

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            firePoint.position,
            projectileRotation
        );

        MagicProjectile projectile =
            projectileObject.GetComponent<MagicProjectile>();

        if (projectile != null)
        {
            projectile.owner = transform;
        }
    }

    void FollowPlayer()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerHealth>();

            if (player == null)
                return;
        }

        Vector3 direction =
            player.transform.position - transform.position;

        direction.y = 0f;

        if (direction.magnitude > followDistance)
        {
            controller.Move(
                direction.normalized * speed * Time.deltaTime
            );

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward = direction.normalized;
            }
        }
    }
}
