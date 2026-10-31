
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MinionAI : MonoBehaviour
{
    public enum AttackType
    {
        Melee,
        Ranged
    }

    [Header("Type d'attaque")]
    public AttackType attackType = AttackType.Melee;

    [Header("Déplacement")]
    public float speed = 3f;
    public float followDistance = 2f;

    [Header("Détection")]
    public float detectionRange = 5f;

    [Header("Combat")]
    public int damage = 5;
    public float attackRange = 1.3f;
    public float attackCooldown = 1f;
    public AudioClip attackSound;
    public AudioClip enemyHitSound;

    [Header("Attaque à distance")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    private PlayerHealth player;
    private PlayerTargeting playerTargeting;

    private CharacterController controller;
    private Animator animator;

    private Vector3 previousPosition;
    private float nextAttackTime;

    private EnemyHealth currentTarget;
    private EnemyHealth pendingShotTarget;

    // Cible partagée entre toutes les invocations.
    private static EnemyHealth sharedTarget;

    // --------------------------------------------------
    // INITIALISATION UNITY
    // --------------------------------------------------

    void Start()
    {
        player = FindAnyObjectByType<PlayerHealth>();
        playerTargeting = FindAnyObjectByType<PlayerTargeting>();

        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        previousPosition = transform.position;
    }

    // --------------------------------------------------
    // INITIALISATION DEPUIS LA CARTE
    // --------------------------------------------------

    public void Initialize(SummonCardDefinition card)
    {
        if (card == null)
        {
            Debug.LogWarning(
                "MinionAI : définition de carte manquante.",
                this
            );
            return;
        }

        // Type d'attaque automatique.
        attackType = card.isRanged
            ? AttackType.Ranged
            : AttackType.Melee;

        // Statistiques de déplacement et de combat.
        speed = card.movementSpeed;
        damage = card.attackDamage;
        attackRange = card.attackRange;

        // Récupérer le son d'attaque défini par la carte.
        attackSound = card.attackSound;
        enemyHitSound = card.enemyHitSound;


        // Nombre d'attaques par seconde.
        attackCooldown = 1f / Mathf.Max(
            0.01f,
            card.attackSpeed
        );

        // Points de vie.
        MinionHealth health = GetComponent<MinionHealth>();

        if (health != null)
        {
            health.maxHealth = card.health;
        }
    }

    // --------------------------------------------------
    // AGGRO GLOBAL
    // --------------------------------------------------

    public static void AlertAllMinions(EnemyHealth enemy)
    {
        if (!IsValidEnemy(enemy))
            return;

        if (IsValidEnemy(sharedTarget))
            return;

        sharedTarget = enemy;
    }

    private static bool IsValidEnemy(EnemyHealth enemy)
    {
        return enemy != null &&
               enemy.gameObject.activeInHierarchy &&
               enemy.CurrentHealth > 0;
    }

    // --------------------------------------------------
    // UPDATE
    // --------------------------------------------------

    void Update()
    {
        if (playerTargeting == null)
        {
            playerTargeting =
                FindAnyObjectByType<PlayerTargeting>();
        }

        UpdateTarget();

        if (IsValidEnemy(currentTarget))
        {
            FightEnemy(currentTarget);
        }
        else
        {
            FollowPlayer();
        }

        UpdateAnimation();

        previousPosition = transform.position;
    }

    // --------------------------------------------------
    // CIBLAGE
    // --------------------------------------------------

    void UpdateTarget()
    {
        // Vérifier la cible collective.
        if (!IsValidEnemy(sharedTarget))
        {
            sharedTarget = null;
        }

        // Priorité à l'alerte collective.
        if (sharedTarget != null)
        {
            currentTarget = sharedTarget;
            return;
        }

        // Conserver la cible engagée jusqu'à sa mort.
        if (IsValidEnemy(currentTarget))
            return;

        currentTarget = null;

        // Ciblage du héros.
        if (playerTargeting != null)
        {
            EnemyHealth heroTarget =
                playerTargeting.CurrentTarget;

            if (IsValidEnemy(heroTarget))
            {
                currentTarget = heroTarget;
                return;
            }
        }

        // Recherche automatique.
        currentTarget = FindClosestEnemy();
    }

    // --------------------------------------------------
    // DÉTECTION DES ENNEMIS
    // --------------------------------------------------

    EnemyHealth FindClosestEnemy()
    {
        EnemyHealth[] enemies =
    FindObjectsByType<EnemyHealth>();

        EnemyHealth closest = null;
        float closestDistance = detectionRange;

        foreach (EnemyHealth enemy in enemies)
        {
            if (!IsValidEnemy(enemy))
                continue;

            float distance = Vector3.Distance(
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

    // --------------------------------------------------
    // COMBAT
    // --------------------------------------------------

    void FightEnemy(EnemyHealth target)
    {
        if (!IsValidEnemy(target))
            return;

        Vector3 direction =
            target.transform.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        // Pour les attaques de mêlée, calculer la
        // distance jusqu'à la surface du collider.
        if (attackType == AttackType.Melee)
        {
            Collider enemyCollider =
                target.GetComponentInChildren<Collider>();

            if (enemyCollider != null)
            {
                Vector3 closestPoint =
                    enemyCollider.ClosestPoint(
                        transform.position
                    );

                Vector3 offset =
                    closestPoint - transform.position;

                offset.y = 0f;

                distance = offset.magnitude;
            }
        }

        // Avancer jusqu'à portée.
        if (distance > attackRange)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                controller.Move(
                    direction.normalized *
                    speed *
                    Time.deltaTime
                );

                transform.forward = direction.normalized;
            }

            return;
        }

        // Regarder l'ennemi.
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.forward = direction.normalized;
        }

        // Respecter le délai entre les attaques.
        if (Time.time < nextAttackTime)
            return;

        // Déclencher l'animation d'attaque.
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        // Choisir le type d'attaque.
        if (attackType == AttackType.Melee)
        {
            MeleeAttack(target);
        }
        else
        {
            RangedAttack(target);
        }

        nextAttackTime = Time.time + attackCooldown;
    }

    // --------------------------------------------------
    // ATTAQUE DE MÊLÉE
    // --------------------------------------------------

    void MeleeAttack(EnemyHealth target)
    {
        if (!IsValidEnemy(target))
            return;

        PlayAttackSound();

        // Prévenir l'ennemi de son attaquant.
        EnemyAI enemyAI =
            target.GetComponent<EnemyAI>();

        if (enemyAI != null)
        {
            enemyAI.AlertedBy(transform);
        }

        // Appliquer les dégâts définis par la carte.
        target.TakeDamage(damage);

        if (enemyHitSound != null && GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlaySFX(
                enemyHitSound,
                target.transform.position
            );
        }

    }

    // --------------------------------------------------
    // ATTAQUE À DISTANCE
    // --------------------------------------------------

    void RangedAttack(EnemyHealth target)
    {
        if (!IsValidEnemy(target))
            return;

        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogWarning(
                "MinionAI : ProjectilePrefab ou FirePoint manquant.",
                this
            );
            return;
        }

        // Mémoriser la cible jusqu'au relâchement de la corde.
        pendingShotTarget = target;
    }

    // --------------------------------------------------
    // SON D'ATTAQUE
    // --------------------------------------------------


    void PlayAttackSound()
    {


        if (attackSound == null)
            return;

        if (GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlaySFX(
                attackSound,
                transform.position
            );
        }
    }


    // --------------------------------------------------
    // SUIVI DU HÉROS
    // --------------------------------------------------

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
            if (direction.sqrMagnitude > 0.001f)
            {
                controller.Move(
                    direction.normalized *
                    speed *
                    Time.deltaTime
                );

                transform.forward = direction.normalized;
            }
        }
    }

    // --------------------------------------------------
    // ANIMATIONS
    // --------------------------------------------------

    void UpdateAnimation()
    {
        if (animator == null)
            return;

        bool isMoving =
            (transform.position - previousPosition)
            .sqrMagnitude > 0.000001f;


        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == "IsMoving" &&
                parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool("IsMoving", isMoving);
            }
            else if (parameter.name == "Speed" &&
                     parameter.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat("Speed", isMoving ? speed : 0f);
            }
        }

    }
    public void ReleaseArrow()
    {
        EnemyHealth target = pendingShotTarget;
        pendingShotTarget = null;

        if (!IsValidEnemy(target))
            return;

        if (projectilePrefab == null || firePoint == null)
            return;

        // Viser le centre du collider de l'ennemi.
        Collider targetCollider =
            target.GetComponentInChildren<Collider>();

        Vector3 targetPosition =
            targetCollider != null
                ? targetCollider.bounds.center
                : target.transform.position;

        Vector3 direction = targetPosition - firePoint.position;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion rotation =
            Quaternion.LookRotation(direction.normalized);

        // Jouer le son au moment du tir.
        PlayAttackSound();

        // Créer la flèche.
        GameObject projectileObject = Instantiate(
            projectilePrefab,
            firePoint.position,
            rotation
        );

        MagicProjectile projectile =
            projectileObject.GetComponent<MagicProjectile>();

        if (projectile != null)
        {
            projectile.owner = transform;
            projectile.damage = damage;
            projectile.hitSound = enemyHitSound;
        }
    }
}
