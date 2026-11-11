using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent, persistent aggro for one enemy spawner.</summary>
public sealed class EnemyAggroGroup : MonoBehaviour
{
    private readonly HashSet<EnemyAI> members = new HashSet<EnemyAI>();
    private Transform sharedTarget;

    public bool IsEngaged => CurrentTarget != null;
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

    private static bool IsValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null) return player.CurrentHealth > 0;
        MinionHealth minion = target.GetComponent<MinionHealth>();
        return minion != null && minion.CurrentHealth > 0;
    }
}
