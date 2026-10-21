
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardDetailsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text cardDescriptionText;
    [SerializeField] private TMP_Text cardStatsText;
    [SerializeField] private Image cardArtworkImage;

    public void Show(SummonCardDefinition card)
    {
        if (card == null) return;

        cardNameText.text = card.cardName;
        cardDescriptionText.text = card.description;

        cardStatsText.text =
            $"Type : {card.summonType}\n" +
            $"Dégâts : {card.attackDamage}\n" +
            $"Vitesse d'attaque : {card.attackSpeed}\n" +
            $"Combat : {(card.isRanged ? "Distance" : "Mêlée")}\n" +
            $"Portée : {card.attackRange}\n" +
            $"PV : {card.health}\n" +
            $"Vitesse de déplacement : {card.movementSpeed}\n" +
            $"Nombre d'unités : {card.numberOfUnits}";

        cardArtworkImage.sprite = card.cardArtwork;
        cardArtworkImage.enabled = card.cardArtwork != null;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
