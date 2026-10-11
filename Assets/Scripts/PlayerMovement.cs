using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;

    // Glisse ton objet VirtualJoystick ici dans l'Inspector.
    public VirtualJoystick joystick;

    private CharacterController controller;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Ce script ne fait rien si le joueur a choisi le tap-to-move.
        if (ControlSettings.CurrentMode != ControlMode.VirtualJoystick)
            return;

        if (joystick == null)
            return;

        Vector2 input = joystick.Direction;
        Vector3 movement = new Vector3(input.x, 0, input.y);

        controller.Move(movement * speed * Time.deltaTime);

        if (movement.sqrMagnitude > 0.01f)
        {
            transform.forward = movement;
        }
    }
}