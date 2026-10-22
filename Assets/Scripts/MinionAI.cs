
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
    private float nextAttackTime;

    // Animation du modèle 3D enfant
    private Animator animator;
    private Vector3 previousPosition;

    void Start()
    {
        player = FindAnyObjectByType<PlayerHealth>();

        animator = GetComponentInChildren<Animator>();
        Debug.Log("SkeletonMinion Animator trouvé : " + (animator != null));
        previousPosition = transform.position;
    }

    void Update()
    {
        EnemyHealth target = FindAnyObjectByType<EnemyHealth>();

        if (target != null)
        {
            FightEnemy(target);
        }
        else
        {
            FollowPlayer();
        }
    }

    void LateUpdate()
    {
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

        direction.y = 0;

        if (direction.magnitude > attackRange)
        {
            transform.position +=
                direction.normalized * speed * Time.deltaTime;

            transform.forward = direction.normalized;
        }
        else if (Time.time >= nextAttackTime)
        {
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

        direction.y = 0;

        if (direction.magnitude > followDistance)
        {
            transform.position +=
                direction.normalized * speed * Time.deltaTime;

            transform.forward = direction.normalized;
        }
    }
}
