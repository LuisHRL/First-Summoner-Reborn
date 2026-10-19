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
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(ray, out RaycastHit objectHit, 100f))
            {
                EnemyHealth enemy =
                    objectHit.collider.GetComponentInParent<EnemyHealth>();

                if (enemy != null)
                    return;
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, groundLayer))
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