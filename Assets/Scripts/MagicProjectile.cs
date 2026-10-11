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

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
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

        // Joue le son d'impact.
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(
                hitSound,
                transform.position
            );
        }

        Destroy(gameObject);
    }
}