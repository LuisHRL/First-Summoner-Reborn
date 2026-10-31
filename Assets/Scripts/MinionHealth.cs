
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

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == "Hit" &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger("Hit");
                    break;
                }
            }

        }

        base.TakeDamage(damage);
    }
}
