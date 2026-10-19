
using UnityEngine;

public class HeroAnimationController : MonoBehaviour
{
    [Header("Animation")]
    public Animator heroAnimator;

    [Header("Réglages")]
    public float movementThreshold = 0.15f;
    public float animationSmoothTime = 0.1f;

    private Vector3 previousPosition;

    void Start()
    {
        previousPosition = transform.position;
    }

    void Update()
    {
        if (heroAnimator == null)
            return;

        Vector3 movement = transform.position - previousPosition;
        movement.y = 0f;

        float speed = movement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        float animationSpeed =
            speed > movementThreshold ? 1f : 0f;

        heroAnimator.SetFloat(
            "Speed",
            animationSpeed,
            animationSmoothTime,
            Time.deltaTime
        );

        previousPosition = transform.position;
    }
}
