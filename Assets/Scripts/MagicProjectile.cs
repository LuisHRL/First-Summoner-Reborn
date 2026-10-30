using UnityEngine;

public class MagicProjectile : MonoBehaviour
{
    public float speed = 12f;
    public int damage = 10;
    public float lifetime = 3f;
    public AudioClip hitSound;

    // Celui qui a tiré le projectile :
    // joueur ou minion.
    public Transform owner;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        rb.MovePosition(
            rb.position + transform.forward * speed * Time.fixedDeltaTime
        );
    }

    void OnTriggerEnter(Collider other)
    {
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (enemy == null)
            return;

        // Inflige les dégâts.
        enemy.TakeDamage(damage);

        // Cherche l'IA de l'ennemi touché.
        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();

        if (enemyAI != null && owner != null)
        {
            // L'ennemi mémorise qui vient de l'attaquer.
            enemyAI.AlertedBy(owner);
        }


        // Joue le son d'impact via le gestionnaire audio.
        if (hitSound != null && GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlaySFX(
                hitSound,
                transform.position
            );
        }

        Destroy(gameObject);
    }
}