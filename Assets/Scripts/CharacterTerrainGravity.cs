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

    [Tooltip("Prevent a character from remaining below a generated Unity Terrain surface.")]
    public bool recoverBelowTerrain = true;
    [Min(0f)] public float terrainPenetrationTolerance = 0.05f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void RecoverFromTerrainPenetration()
    {
        if (!recoverBelowTerrain) return;

        Vector3 position = transform.position;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || !terrain.isActiveAndEnabled || terrain.terrainData == null)
                continue;

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (position.x < origin.x || position.x > origin.x + size.x ||
                position.z < origin.z || position.z > origin.z + size.z)
                continue;

            float surfaceY = terrain.SampleHeight(position) + origin.y;
            float bottomY = controller.bounds.min.y;
            if (bottomY >= surfaceY - terrainPenetrationTolerance)
                return;

            // Move the controller's actual capsule bottom above the terrain,
            // not the pivot (which may be at the model's feet or center).
            float correction = surfaceY - bottomY + controller.skinWidth;
            controller.enabled = false;
            transform.position += Vector3.up * correction;
            controller.enabled = true;
            verticalSpeed = 0f;
            return;
        }
    }

    private void LateUpdate()
    {
        if (controller == null || !controller.enabled) return;

        RecoverFromTerrainPenetration();

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
