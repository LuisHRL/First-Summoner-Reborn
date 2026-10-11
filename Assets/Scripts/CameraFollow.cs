using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    // Décalage entre la caméra et le joueur (garde les mêmes
    // valeurs que la position actuelle de ta caméra dans la Hierarchy).
    public Vector3 offset = new Vector3(0, 11, -14);

    void LateUpdate()
    {
        if (target == null)
            return;

        // On copie uniquement la position, jamais la rotation :
        // la caméra garde donc toujours le même angle, quoi que
        // fasse le joueur (rotation, joystick, tap-to-move...).
        transform.position = target.position + offset;
    }
}