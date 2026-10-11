using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Classe "statique" = pas besoin de l'attacher à un GameObject.
// On l'appelle directement depuis n'importe quel script avec
// PointerInput.TryGetPointerDown(...) etc.
public static class PointerInput
{
    // Renvoie true si l'écran vient d'être touché/cliqué ce frame,
    // et donne la position (screenPosition) de ce clic/touch.
    public static bool TryGetPointerDown(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition =
                Touchscreen.current.primaryTouch.position.ReadValue();

            return true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        return false;
    }

    // Renvoie true si le pointeur (doigt ou souris) est actuellement
    // au-dessus d'un élément d'UI (bouton, panel, etc.)
    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        if (Touchscreen.current != null)
        {
            int touchId =
                Touchscreen.current.primaryTouch.touchId.ReadValue();

            return EventSystem.current.IsPointerOverGameObject(touchId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}