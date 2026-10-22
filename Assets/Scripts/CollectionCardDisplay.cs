
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CollectionCardDisplay : MonoBehaviour, IPointerClickHandler
{
    [Header("Éléments visuels")]
    [SerializeField] private Image cardArtwork;
    [SerializeField] private Image rarityFrame;
    [Header("Cadres de rareté")]
    [SerializeField] private Sprite commonFrame;
    [SerializeField] private Sprite atypicalFrame;
    [SerializeField] private Sprite rareFrame;
    [SerializeField] private Sprite epicFrame;
    [SerializeField] private Sprite legendaryFrame;
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

        if (rarityFrame != null)
        {
            switch (card.rarity)
            {
                case "Common":
                    rarityFrame.sprite = commonFrame;
                    break;
                case "Atypical":
                    rarityFrame.sprite = atypicalFrame;
                    break;
                case "Rare":
                    rarityFrame.sprite = rareFrame;
                    break;
                case "Epic":
                    rarityFrame.sprite = epicFrame;
                    break;
                case "Legendary":
                    rarityFrame.sprite = legendaryFrame;
                    break;
            }
        }
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
