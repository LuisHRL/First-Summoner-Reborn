using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAttack : MonoBehaviour
{
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

    void Awake()
    {
        targeting = GetComponent<PlayerTargeting>();
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Aucun ennemi sélectionné.
        if (targeting == null ||
            targeting.CurrentTarget == null)
        {
            return;
        }

        EnemyHealth target =
            targeting.CurrentTarget;

        // L'ennemi a été détruit ou n'existe plus.
        if (target == null)
        {
            return;
        }

        // -----------------------------------------------------
        // CALCUL DE LA DIRECTION VERS L'ENNEMI
        // -----------------------------------------------------

        Vector3 direction =
            target.transform.position -
            transform.position;

        // On ignore la hauteur.
        direction.y = 0;

        float distance =
            direction.magnitude;

        // -----------------------------------------------------
        // ENNEMI HORS DE PORTÉE
        // -----------------------------------------------------

        if (distance > attackRange)
        {
            // Le joueur avance automatiquement vers l'ennemi.
            Vector3 movement =
                direction.normalized *
                moveToAttackSpeed *
                Time.deltaTime;

            controller.Move(movement);

            // Le joueur regarde l'ennemi.
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward =
                    direction.normalized;
            }

            // On ne tire pas encore.
            return;
        }

        // -----------------------------------------------------
        // ENNEMI À PORTÉE
        // -----------------------------------------------------

        // Le joueur est maintenant assez proche.
        // Il arrête automatiquement de se déplacer
        // et peut tirer.

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.forward =
                direction.normalized;
        }

        // Respect du cooldown d'attaque.
        if (Time.time < nextAttackTime)
        {
            return;
        }

        ShootAt(target);

        nextAttackTime =
            Time.time + attackCooldown;
    }

    // ---------------------------------------------------------
    // TIR
    // ---------------------------------------------------------

    void ShootAt(EnemyHealth target)
    {
        if (target == null)
        {
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                "PlayerAttack : projectilePrefab n'est pas assigné."
            );

            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning(
                "PlayerAttack : firePoint n'est pas assigné."
            );

            return;
        }

        Vector3 direction =
            target.transform.position -
            firePoint.position;

        direction.y = 0;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion projectileRotation =
            Quaternion.LookRotation(
                direction.normalized
            );

        // Son de tir.
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

        // Récupération du script du projectile.
        MagicProjectile projectile =
            projectileObject.GetComponent<MagicProjectile>();

        if (projectile != null)
        {
            // Le joueur devient le propriétaire
            // de la flèche.
            projectile.owner = transform;
        }
    }
}