
using UnityEngine;

public class MinionAI : MonoBehaviour
{
    [Header("Déplacement")]
    public float speed = 3f;
    public float followDistance = 2f;

    [Header("Combat")]
    public int damage = 5;
    public float attackRange = 1.3f;
    public float attackCooldown = 1f;
    public AudioClip attackSound;

    private PlayerHealth player;
    private PlayerTargeting playerTargeting;

    private float nextAttackTime;

    private Animator animator;
    private CharacterController controller;
    private Vector3 previousPosition;

    void Start()
    {
        player = FindAnyObjectByType<PlayerHealth>();
        playerTargeting = FindAnyObjectByType<PlayerTargeting>();

        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        Debug.Log(
    "MINION ANIMATOR UTILISÉ : " +
    (animator != null ? animator.gameObject.name : "AUCUN")
);
        previousPosition = transform.position;
    }

    void Update()
    {
        // Retrouver le système de ciblage si nécessaire
        if (playerTargeting == null)
        {
            playerTargeting =
                FindAnyObjectByType<PlayerTargeting>();
        }

        // Utiliser uniquement la cible du héros
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

        // Animation de déplacement
        if (animator != null)
        {
            bool isMoving =
                (transform.position - previousPosition).sqrMagnitude
                > 0.000001f;

            animator.SetBool("IsMoving", isMoving);
        }

        previousPosition = transform.position;
    }

    void FightEnemy(EnemyHealth target)
    {
        Vector3 direction =
            target.transform.position - transform.position;

        direction.y = 0f;

        if (direction.magnitude > attackRange)
        {
            controller.Move(
            direction.normalized * speed * Time.deltaTime
            );

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward = direction.normalized;
            }
        }
        else if (Time.time >= nextAttackTime)
        {
            if (animator != null)
                animator.SetTrigger("Attack");
            if (attackSound != null)
            {
                AudioSource.PlayClipAtPoint(
                    attackSound,
                    transform.position
                );
            }

            target.TakeDamage(damage);

            nextAttackTime = Time.time + attackCooldown;
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
