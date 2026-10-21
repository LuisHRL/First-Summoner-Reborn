
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CollectionCardDisplay : MonoBehaviour, IPointerClickHandler
{
    [Header("Éléments visuels")]
    [SerializeField] private Image cardArtwork;
    [SerializeField] private Image rarityFrame;
    [SerializeField] private TMP_Text manaCostText;
    [SerializeField] private TMP_Text levelText;

    private SummonCardDefinition cardDefinition;

    public void Setup(SummonCardDefinition card)
    {
        if (card == null) return;

        cardDefinition = card;

        CardData cardData = GetComponent<CardData>();
        if (cardData != null)
            cardData.SetCardId(card.cardId);

        if (manaCostText != null)
            manaCostText.text = card.manaCost.ToString();

        if (levelText != null)
            levelText.text = "Niv. " + card.level;

        if (cardArtwork != null)
        {
            cardArtwork.sprite = card.cardArtwork;
            cardArtwork.enabled = card.cardArtwork != null;
        }

        // Les cadres de rareté seront configurés plus tard.
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (cardDefinition == null)
            return;

        CardDetailsUI detailsUI =
            FindAnyObjectByType<CardDetailsUI>(FindObjectsInactive.Include);

        if (detailsUI != null)
            detailsUI.Show(cardDefinition);
    }
}
