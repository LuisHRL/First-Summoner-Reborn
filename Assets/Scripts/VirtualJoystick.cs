using UnityEngine;
using UnityEngine.EventSystems;

// A poser sur l'image "fond" du joystick (le grand cercle).
// Nécessite une image enfant "Handle" (le petit cercle qui bouge).
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform background;
    public RectTransform handle;

    // Distance maximale (en pixels) que le handle peut s'éloigner du centre.
    public float handleRange = 60f;

    // Direction actuelle du joystick, de (-1,-1) à (1,1).
    // C'est cette valeur que PlayerMovement va lire.
    public Vector2 Direction { get; private set; }

    void Start()
    {
        // On affiche le joystick uniquement si le joueur a choisi
        // ce mode de contrôle. Sinon, on cache tout le GameObject
        // (donc aussi le Handle, qui est son enfant).
        gameObject.SetActive(ControlSettings.CurrentMode == ControlMode.VirtualJoystick);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        Vector2 clamped = Vector2.ClampMagnitude(localPoint, handleRange);

        handle.anchoredPosition = clamped;
        Direction = clamped / handleRange;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        handle.anchoredPosition = Vector2.zero;
        Direction = Vector2.zero;
    }
}