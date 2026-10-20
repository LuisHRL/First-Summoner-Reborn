
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Animation")]
    public Animator heroAnimator;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Attaque")]
    public float attackRange = 10f;
    public float attackCooldown = 0.5f;

    [Header("Déplacement automatique")]
    public float moveToAttackSpeed = 5f;

    [Header("Audio")]
    public AudioClip launchSound;

    private PlayerTargeting targeting;
    private CharacterController controller;

    private float nextAttackTime;
    private bool attackInProgress;
    private EnemyHealth pendingTarget;

    void Awake()
    {
        targeting = GetComponent<PlayerTargeting>();
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (targeting == null || targeting.CurrentTarget == null)
            return;

        EnemyHealth target = targeting.CurrentTarget;

        Vector3 direction =
            target.transform.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        // Attendre le lâcher de la flèche avant
        // de commencer une nouvelle attaque.
        if (attackInProgress)
            return;

        // Avancer vers l'ennemi s'il est hors de portée.
        if (distance > attackRange)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                controller.Move(
                    direction.normalized *
                    moveToAttackSpeed *
                    Time.deltaTime
                );

                transform.forward = direction.normalized;
            }

            return;
        }

        // Regarder l'ennemi avant de tirer.
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.forward = direction.normalized;
        }

        // Respecter le délai entre les attaques.
        if (Time.time < nextAttackTime)
            return;

        StartAttack(target);
    }

    void StartAttack(EnemyHealth target)
    {
        if (target == null)
            return;

        if (projectilePrefab == null || firePoint == null)
            return;

        if (heroAnimator == null)
        {
            Debug.LogWarning(
                "PlayerAttack : Hero Animator non assigné."
            );

            return;
        }

        pendingTarget = target;
        attackInProgress = true;

        // Joue directement l'animation de tir.
        // Cette méthode fonctionne dans notre configuration.
        heroAnimator.Play("BowAttack", 0, 0f);
    }

    // Appelée par l'Animation Event du clip BowAttack.
    public void ReleaseArrow()
    {
        if (!attackInProgress)
            return;

        attackInProgress = false;
        nextAttackTime = Time.time + attackCooldown;

        EnemyHealth target = pendingTarget;
        pendingTarget = null;

        // La cible peut avoir été détruite
        // pendant l'animation.
        if (target == null)
            return;

        if (firePoint == null || projectilePrefab == null)
            return;

        Vector3 direction =
            target.transform.position - firePoint.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion projectileRotation =
            Quaternion.LookRotation(direction.normalized);

        // Jouer le son au moment du tir.
        if (launchSound != null)
        {
            AudioSource.PlayClipAtPoint(
                launchSound,
                firePoint.position
            );
        }

        // Créer la flèche magique.
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
}
