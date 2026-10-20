
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDropSlot : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
            return;

        CardDragHandler draggedCard =
            eventData.pointerDrag.GetComponent<CardDragHandler>();

        if (draggedCard == null)
            return;

        // Chercher une carte déjà équipée dans cet emplacement.
        CardDragHandler existingCard =
            GetComponentInChildren<CardDragHandler>();

        if (existingCard != null && existingCard != draggedCard)
        {
            // Si la nouvelle carte vient d'un autre emplacement,
            // échanger les deux cartes.
            CardDropSlot sourceSlot =
                draggedCard.OriginalParent.GetComponent<CardDropSlot>();

            if (sourceSlot != null)
            {
                existingCard.MoveTo(sourceSlot.transform);
            }
            else
            {
                // Si elle vient de la collection,
                // renvoyer l'ancienne carte dans la collection.
                existingCard.ReturnToCollection();
            }
        }

        draggedCard.SetDropTarget(transform);
    }
}
