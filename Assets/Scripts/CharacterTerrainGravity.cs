using UnityEngine;

/// <summary>
/// Applies gravity once per frame to a CharacterController, independently of
/// horizontal movement and combat AI. Works with uneven Unity terrains.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class CharacterTerrainGravity : MonoBehaviour
{
    [Min(0f)] public float gravity = 25f;
    [Min(0f)] public float groundedDownwardSpeed = 2f;
    [Min(0f)] public float terminalFallSpeed = 50f;

    private CharacterController controller;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        if (controller == null || !controller.enabled) return;

        if (controller.isGrounded && verticalSpeed <= 0f)
            verticalSpeed = -groundedDownwardSpeed;
        else
            verticalSpeed = Mathf.Max(verticalSpeed - gravity * Time.deltaTime, -terminalFallSpeed);

        CollisionFlags flags = controller.Move(Vector3.up * (verticalSpeed * Time.deltaTime));
        if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
            verticalSpeed = -groundedDownwardSpeed;
        else if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            verticalSpeed = 0f;
    }
}
