
using UnityEngine;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    [Header("Emplacements des cartes")]
    [SerializeField] private Transform[] cardSlots = new Transform[6];

    [Header("Deck actif")]
    [SerializeField, Range(1, 3)] private int activeDeck = 1;

    [Header("Boutons des decks")]
    [SerializeField] private Button[] deckButtons = new Button[3];

    public int ActiveDeck => activeDeck;

    private void Awake()
    {
        // Retrouver le dernier deck sélectionné.
        activeDeck = Mathf.Clamp(
            PlayerPrefs.GetInt("ActiveDeck", 1),
            1,
            3
        );

        UpdateDeckButtons();
    }

    // Sauvegarder les six cartes du deck actif.
    public void SaveDeck()
    {
        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null)
                continue;

            CardData card =
                cardSlots[i].GetComponentInChildren<CardData>();

            string cardId = card != null ? card.CardId : "";

            PlayerPrefs.SetString(
                "Deck_" + activeDeck + "_Slot_" + i,
                cardId
            );
        }

        PlayerPrefs.Save();

        Debug.Log("Deck " + activeDeck + " sauvegardé !");
    }

    // Charger les cartes sauvegardées du deck actif.
    public void LoadDeck()
    {
        CardData[] allCards = FindObjectsByType<CardData>(
            FindObjectsInactive.Include
        );

        // Vider les emplacements avant de charger le deck.
        ClearDisplayedDeck();

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null)
                continue;

            string savedId = PlayerPrefs.GetString(
                "Deck_" + activeDeck + "_Slot_" + i,
                ""
            );

            if (string.IsNullOrEmpty(savedId))
                continue;

            foreach (CardData card in allCards)
            {
                if (card.CardId == savedId)
                {
                    CardDragHandler dragHandler =
                        card.GetComponent<CardDragHandler>();

                    if (dragHandler != null)
                    {
                        dragHandler.MoveTo(cardSlots[i]);
                    }

                    break;
                }
            }
        }

        UpdateDeckButtons();

        Debug.Log("Deck " + activeDeck + " chargé !");
    }

    // Replacer les cartes affichées dans la collection.
    private void ClearDisplayedDeck()
    {
        foreach (Transform slot in cardSlots)
        {
            if (slot == null)
                continue;

            CardDragHandler card =
                slot.GetComponentInChildren<CardDragHandler>();

            if (card != null)
            {
                card.ReturnToCollection();
            }
        }
    }

    // Changer de deck avec les boutons 1, 2 et 3.
    public void SelectDeck(int deckNumber)
    {
        if (deckNumber < 1 || deckNumber > 3)
            return;

        if (deckNumber == activeDeck)
            return;

        // Sauvegarder le deck actuel.
        SaveDeck();

        // Changer de deck.
        activeDeck = deckNumber;

        // Mémoriser le deck sélectionné pour GameScene.
        PlayerPrefs.SetInt("ActiveDeck", activeDeck);
        PlayerPrefs.Save();

        // Charger le nouveau deck.
        // LoadDeck remet déjà les cartes dans la collection.
        LoadDeck();

        Debug.Log("Deck actif : " + activeDeck);
    }

    // Mettre en évidence le bouton du deck sélectionné.
    private void UpdateDeckButtons()
    {
        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null)
                continue;

            bool isActive = (i + 1) == activeDeck;

            deckButtons[i].image.color = isActive
                ? new Color(0.85f, 0.65f, 0.25f)
                : new Color(0.50f, 0.50f, 0.50f);
        }
    }
}
