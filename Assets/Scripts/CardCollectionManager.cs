using UnityEngine;

public class CardCollectionManager : MonoBehaviour
{
    [Header("Collection")]
    [SerializeField] private Transform cardGrid;
    [SerializeField] private GameObject collectionCardPrefab;

    private void Start()
    {
        if (cardGrid == null || collectionCardPrefab == null)
        {
            Debug.LogError("CardCollectionManager : références manquantes !");
            return;
        }

        SummonCardDefinition[] cards =
            Resources.LoadAll<SummonCardDefinition>("CardDefinitions");

        foreach (SummonCardDefinition card in cards)
        {
            GameObject newCard = Instantiate(
                collectionCardPrefab,
                cardGrid
            );

            newCard.name = "Collection_" + card.cardId;

            CollectionCardDisplay display =
                newCard.GetComponent<CollectionCardDisplay>();

            if (display != null)
                display.Setup(card);
        }

        Debug.Log("Collection générée : " + cards.Length + " cartes.");
    }
}