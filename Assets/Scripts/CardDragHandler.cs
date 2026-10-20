
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragHandler : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;

    private Transform collectionParent;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Transform dropTarget;

    // Permet à CardDropSlot de connaître l'emplacement de départ.
    public Transform OriginalParent => originalParent;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>().rootCanvas;

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Mémoriser la collection d'origine.
        collectionParent = transform.parent;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("CLIC DÉTECTÉ : " + gameObject.name);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        dropTarget = null;

        // Sortir la vraie carte de sa grille.
        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();

        // Permettre aux emplacements de recevoir le dépôt.
        canvasGroup.blocksRaycasts = false;

        Debug.Log("DRAG DÉTECTÉ : " + gameObject.name);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // La carte suit la souris ou le doigt.
        rectTransform.position += (Vector3)eventData.delta;
    }

    public void SetDropTarget(Transform target)
    {
        dropTarget = target;
    }

    // Placer une carte dans un emplacement du deck.
    public void MoveTo(Transform target)
    {
        transform.SetParent(target, false);

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        rectTransform.sizeDelta = new Vector2(200f, 280f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    // Renvoyer une carte dans la collection.
    public void ReturnToCollection()
    {
        transform.SetParent(collectionParent, false);

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dropTarget != null)
        {
            MoveTo(dropTarget);
            Debug.Log("CARTE ÉQUIPÉE : " + gameObject.name);
        }
        else
        {
            ReturnToCollection();
            Debug.Log("CARTE RETOURNÉE : " + gameObject.name);
        }

        // Réactiver la détection des clics.
        canvasGroup.blocksRaycasts = true;

        // Sauvegarder automatiquement le deck actif.
        DeckManager deckManager = FindAnyObjectByType<DeckManager>();

        if (deckManager != null)
        {
            deckManager.SaveDeck();
        }
    }
}
