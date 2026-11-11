using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent aggro and leash for enemies created by one spawner.</summary>
public sealed class EnemyAggroGroup : MonoBehaviour
{
    [Header("Désengagement du groupe")]
    [Min(1f)] public float maximumPursuitDistance = 14f;
    [Min(0f)] public float disengageDelay = 1.5f;

    private readonly HashSet<EnemyAI> members = new HashSet<EnemyAI>();
    private Transform sharedTarget;
    private float outsideSince = -1f;

    public bool IsEngaged => CurrentTarget != null;
    public Transform CurrentTarget => IsValid(sharedTarget) ? sharedTarget : null;

    public void Register(EnemyAI enemy)
    {
        if (enemy != null) members.Add(enemy);
    }

    public void Unregister(EnemyAI enemy)
    {
        members.Remove(enemy);
        if (members.Count == 0) ResetAggro();
    }

    public void Alert(Transform target)
    {
        if (IsValid(target))
        {
            sharedTarget = target;
            outsideSince = -1f;
        }
    }

    private void Update()
    {
        if (sharedTarget == null) return;
        if (!IsValid(sharedTarget))
        {
            ResetAggro();
            return;
        }

        bool inRange = false;
        foreach (EnemyAI enemy in members)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            Vector3 delta = sharedTarget.position - enemy.HomePosition;
            delta.y = 0f;
            if (delta.sqrMagnitude <= maximumPursuitDistance * maximumPursuitDistance)
            {
                inRange = true;
                break;
            }
        }

        if (inRange)
        {
            outsideSince = -1f;
            return;
        }

        if (outsideSince < 0f) outsideSince = Time.time;
        if (Time.time - outsideSince >= disengageDelay)
            ResetAggro();
    }

    private void ResetAggro()
    {
        sharedTarget = null;
        outsideSince = -1f;
        foreach (EnemyAI enemy in members)
            if (enemy != null) enemy.Disengage();
    }

    private static bool IsValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null) return player.CurrentHealth > 0;
        MinionHealth minion = target.GetComponent<MinionHealth>();
        return minion != null && minion.CurrentHealth > 0;
    }
}
