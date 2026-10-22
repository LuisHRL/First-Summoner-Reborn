
using UnityEngine;

public class CombatDeckManager : MonoBehaviour
{
    [Header("Les 6 cartes de combat")]
    [SerializeField] private SummonCard[] combatCards = new SummonCard[6];

    private void Start()
    {
        int activeDeck = PlayerPrefs.GetInt("ActiveDeck", 1);

        Debug.Log("Deck actif en combat : " + activeDeck);

        // Charger les fiches une seule fois.
        SummonCardDefinition[] allCards =
            Resources.LoadAll<SummonCardDefinition>("CardDefinitions");

        // Configurer les 6 emplacements.
        for (int i = 0; i < combatCards.Length; i++)
        {
            if (combatCards[i] == null)
                continue;

            string cardId = PlayerPrefs.GetString(
                "Deck_" + activeDeck + "_Slot_" + i,
                ""
            );

            Debug.Log("Emplacement " + (i + 1) + " : " + cardId);

            // Désactiver les emplacements vides.
            if (string.IsNullOrEmpty(cardId))
            {
                combatCards[i].gameObject.SetActive(false);
                continue;
            }

            SummonCardDefinition card = FindCard(cardId, allCards);

            if (card == null || card.minionPrefab == null)
            {
                Debug.LogWarning("Carte non configurée : " + cardId);
                combatCards[i].gameObject.SetActive(false);
                continue;
            }

            // Configurer automatiquement l'invocation.
            combatCards[i].minionPrefab = card.minionPrefab;
            combatCards[i].manaCost = card.manaCost;
            CollectionCardDisplay cardDisplay =
    combatCards[i].GetComponentInChildren<CollectionCardDisplay>(true);

            if (cardDisplay != null)
            {
                cardDisplay.Setup(card);
            }
            combatCards[i].gameObject.SetActive(true);

            Debug.Log("Invocation configurée : " + card.cardId);
        }
    }

    private SummonCardDefinition FindCard(
        string cardId,
        SummonCardDefinition[] allCards)
    {
        foreach (SummonCardDefinition card in allCards)
        {
            if (card.cardId == cardId)
                return card;
        }

        Debug.LogWarning("Fiche introuvable : " + cardId);
        return null;
    }
}
