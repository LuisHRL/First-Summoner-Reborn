
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterTerrainGravity))]
public class EnemyAI : MonoBehaviour
{
    [Header("Déplacement")]
    public float speed = 2.5f;

    [Header("Combat")]
    public float attackRange = 1.4f;
    public int damage = 5;
    public float attackCooldown = 1f;

    [Header("Aggro joueur")]
    public float playerAggroRange = 1.6f;
    public float playerLoseRange = 8f;

    [Header("Aggro minion")]
    public float minionAggroRange = 5f;

    [Header("Mémoire de l'attaquant")]
    public float attackerMemoryDuration = 10f;

    private CharacterController controller;
    private PlayerHealth player;
    private Transform currentTarget;
    private float nextAttackTime;
    private Vector3 homePosition;
    private bool returningHome;
    public Vector3 HomePosition => homePosition;

    public void Disengage()
    {
        currentTarget = null;
        returningHome = true;
    }

    // Cible globale commune au camp ennemi.
    private EnemyAggroGroup aggroGroup;
    public void AssignAggroGroup(EnemyAggroGroup group)
    {
        if (aggroGroup != null) aggroGroup.Unregister(this);
        aggroGroup = group;
        if (group != null) group.Register(this);
    }
    private void OnDestroy()
    {
        if (aggroGroup != null) aggroGroup.Unregister(this);
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null) controller = gameObject.AddComponent<CharacterController>();
        if (GetComponent<CharacterTerrainGravity>() == null)
            gameObject.AddComponent<CharacterTerrainGravity>();
        homePosition = transform.position;
        player = FindAnyObjectByType<PlayerHealth>();
    }

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerHealth>();
        }

        if (returningHome)
        {
            ReturnHome();
            return;
        }

        UpdateTarget();

        if (currentTarget != null)
        {
            MoveAndAttack();
        }
    }

    // --------------------------------------------------
    // AGGRO PAR GROUPE
    // --------------------------------------------------

    public void AlertedBy(Transform attacker)
    {
        if (returningHome || !IsTargetValid(attacker))
            return;

        // Une cible collective vivante reste prioritaire.
        if (IsTargetValid(aggroGroup != null ? aggroGroup.CurrentTarget : null))
            return;

        if (aggroGroup != null) aggroGroup.Alert(attacker);
        currentTarget = attacker;
    }

    // --------------------------------------------------
    // VALIDATION
    // --------------------------------------------------

    bool IsTargetValid(Transform target)
    {
        if (target == null ||
            !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        PlayerHealth targetPlayer =
            target.GetComponent<PlayerHealth>();

        if (targetPlayer != null)
        {
            return targetPlayer.CurrentHealth > 0;
        }

        MinionHealth targetMinion =
            target.GetComponent<MinionHealth>();

        if (targetMinion != null)
        {
            return targetMinion.CurrentHealth > 0;
        }

        return false;
    }

    // --------------------------------------------------
    // CHOIX DE CIBLE
    // --------------------------------------------------

    void UpdateTarget()
    {
        if (aggroGroup != null && aggroGroup.CurrentTarget != null)
        {
            currentTarget = aggroGroup.CurrentTarget;
            return;
        }

        // Un ennemi isolé perd aussi sa cible lorsqu'elle est trop éloignée.
        if (aggroGroup == null && IsTargetValid(currentTarget) &&
            Vector3.Distance(homePosition, currentTarget.position) > playerLoseRange)
            Disengage();
        if (returningHome) return;

        // Conserver l'adversaire engagé.
        if (IsTargetValid(currentTarget))
        {
            if (aggroGroup != null) aggroGroup.Alert(currentTarget);
            return;
        }

        currentTarget = null;

        // Priorité aux invocations proches.
        MinionHealth minion = FindClosestMinion();

        if (minion != null)
        {
            currentTarget = minion.transform;
            if (aggroGroup != null) aggroGroup.Alert(currentTarget);
            return;
        }

        // Sinon, détecter le héros.
        if (player != null && player.CurrentHealth > 0)
        {
            float distance = Vector3.Distance(
                transform.position,
                player.transform.position
            );

            if (distance <= playerAggroRange)
            {
                currentTarget = player.transform;
                if (aggroGroup != null) aggroGroup.Alert(currentTarget);
            }
        }
    }

    // --------------------------------------------------
    // DÉTECTION DES INVOCATIONS
    // --------------------------------------------------

    MinionHealth FindClosestMinion()
    {
        MinionHealth[] minions =
    FindObjectsByType<MinionHealth>();

        MinionHealth closest = null;
        float closestDistance = minionAggroRange;

        foreach (MinionHealth minion in minions)
        {
            if (minion == null ||
                !minion.gameObject.activeInHierarchy ||
                minion.CurrentHealth <= 0)
            {
                continue;
            }

            float distance = Vector3.Distance(
                transform.position,
                minion.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = minion;
            }
        }

        return closest;
    }

    // --------------------------------------------------
    // DÉPLACEMENT ET COMBAT
    // --------------------------------------------------

    void MoveAndAttack()
    {
        if (!IsTargetValid(currentTarget))
            return;

        Vector3 direction =
            currentTarget.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance > attackRange)
        {
            Vector3 movement =
                direction.normalized *
                speed *
                Time.deltaTime;

            controller.Move(movement);

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward = direction.normalized;
            }

            return;
        }

        if (Time.time < nextAttackTime)
            return;

        AttackTarget();

        nextAttackTime = Time.time + attackCooldown;
    }

    private void ReturnHome()
    {
        Vector3 delta = homePosition - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= 0.16f)
        {
            returningHome = false;
            return;
        }

        Vector3 direction = delta.normalized;
        controller.Move(direction * Mathf.Min(speed * Time.deltaTime, delta.magnitude));
        transform.forward = direction;
    }

    // --------------------------------------------------
    // ATTAQUE
    // --------------------------------------------------

    void AttackTarget()
    {
        if (!IsTargetValid(currentTarget))
            return;

        PlayerHealth targetPlayer =
            currentTarget.GetComponent<PlayerHealth>();

        if (targetPlayer != null)
        {
            targetPlayer.TakeDamage(damage);
            return;
        }

        MinionHealth targetMinion =
            currentTarget.GetComponent<MinionHealth>();

        if (targetMinion != null)
        {
            // Déclencher l'animation Hit existante.
            targetMinion.TakeDamage(damage);

            // Alerter toutes les invocations.
            EnemyHealth enemyHealth =
                GetComponent<EnemyHealth>();

            if (enemyHealth != null &&
                enemyHealth.CurrentHealth > 0)
            {
                MinionAI.AlertAllMinions(enemyHealth);
            }
        }
    }
}
