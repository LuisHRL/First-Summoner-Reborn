
using UnityEngine;

public class HeroAnimationEvents : MonoBehaviour
{
    public PlayerAttack playerAttack;

    public void ReleaseArrow()
    {
        if (playerAttack != null)
        {
            playerAttack.ReleaseArrow();
        }
    }
}
