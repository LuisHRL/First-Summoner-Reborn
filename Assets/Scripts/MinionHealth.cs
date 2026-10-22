
using UnityEngine;

public class MinionHealth : Health
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    public new void TakeDamage(int damage)
    {
        if (animator != null && CurrentHealth > damage)
        {
            animator.SetTrigger("Hit");
        }

        base.TakeDamage(damage);
    }
}
