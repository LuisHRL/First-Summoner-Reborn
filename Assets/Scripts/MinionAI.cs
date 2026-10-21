
using UnityEngine;

public class MinionAI : MonoBehaviour
{
    public float speed = 3f;
    public int damage = 5;
    public float attackRange = 1.3f;
    public float attackCooldown = 1f;
    public float followDistance = 2f;
    public AudioClip attackSound;

    private PlayerHealth player;
    private float nextAttackTime;

    void Start()
    {
        player = FindAnyObjectByType<PlayerHealth>();
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

    void FightEnemy(EnemyHealth target)
    {
        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0;

        if (direction.magnitude > attackRange)
        {
            transform.position += direction.normalized * speed * Time.deltaTime;
            transform.forward = direction.normalized;
        }
        else if (Time.time >= nextAttackTime)
        {
            if (attackSound != null)
            {
                AudioSource.PlayClipAtPoint(attackSound, transform.position);
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

        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0;

        if (direction.magnitude > followDistance)
        {
            transform.position += direction.normalized * speed * Time.deltaTime;
            transform.forward = direction.normalized;
        }
    }
}
