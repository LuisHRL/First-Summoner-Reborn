using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ClickToMove : MonoBehaviour
{
    public float speed = 5f;
    public LayerMask groundLayer;

    private CharacterController controller;
    private Vector3 targetPosition;
    private bool isMoving;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Ce script ne fait rien si le joueur a choisi le joystick virtuel.
        if (ControlSettings.CurrentMode != ControlMode.TapToMove)
            return;

        if (PointerInput.TryGetPointerDown(out Vector2 screenPosition) &&
            !PointerInput.IsPointerOverUI())
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null)
            {
                Debug.LogWarning("[ClickToMove] No camera tagged MainCamera.", this);
                return;
            }

            Ray ray = activeCamera.ScreenPointToRay(screenPosition);
            float rayDistance = Mathf.Max(1000f, activeCamera.farClipPlane);

            if (Physics.Raycast(ray, out RaycastHit objectHit, rayDistance))
            {
                EnemyHealth enemy =
                    objectHit.collider.GetComponentInParent<EnemyHealth>();

                if (enemy != null)
                    return;
            }

            RaycastHit groundHit;
            bool foundGround = Physics.Raycast(ray, out groundHit, rayDistance, groundLayer);
            if (!foundGround)
            {
                // Generated Unity terrains can be on a different layer than the
                // serialized ground mask. Only fall back to TerrainCollider,
                // never to arbitrary colliders (enemies, trees or props).
                float nearestDistance = float.PositiveInfinity;
                foreach (Terrain terrain in Terrain.activeTerrains)
                {
                    if (terrain == null || !terrain.gameObject.activeInHierarchy)
                        continue;

                    TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
                    if (terrainCollider == null || !terrainCollider.enabled)
                        continue;

                    RaycastHit terrainHit;
                    if (terrainCollider.Raycast(ray, out terrainHit, rayDistance) &&
                        terrainHit.distance < nearestDistance)
                    {
                        groundHit = terrainHit;
                        nearestDistance = terrainHit.distance;
                        foundGround = true;
                    }
                }
            }

            if (foundGround)
            {
                targetPosition = groundHit.point;
                targetPosition.y = transform.position.y;
                isMoving = true;
            }
        }

        if (!isMoving)
            return;

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0;

        if (direction.magnitude <= 0.1f)
        {
            isMoving = false;
            return;
        }

        // Orienter l'archère vers sa destination.
        transform.forward = direction.normalized;

        // Avancer vers la destination.
        controller.Move(direction.normalized * speed * Time.deltaTime);
    }
}