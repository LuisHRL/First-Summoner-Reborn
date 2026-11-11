using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent combat alert state for enemies created by one spawner.</summary>
public sealed class EnemyAggroGroup : MonoBehaviour
{
    private readonly HashSet<EnemyAI> members = new HashSet<EnemyAI>();
    private Transform sharedTarget;

    public bool IsEngaged => IsValid(sharedTarget);
    public Transform CurrentTarget => IsValid(sharedTarget) ? sharedTarget : null;

    public void Register(EnemyAI enemy)
    {
        if (enemy != null) members.Add(enemy);
    }

    public void Unregister(EnemyAI enemy)
    {
        members.Remove(enemy);
        if (members.Count == 0) sharedTarget = null;
    }

    public void Alert(Transform target)
    {
        if (IsValid(target)) sharedTarget = target;
    }

    public void ClearTarget(Transform target)
    {
        if (sharedTarget == target) sharedTarget = null;
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
