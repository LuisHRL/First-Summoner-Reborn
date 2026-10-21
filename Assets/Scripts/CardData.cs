
using UnityEngine;

public class CardData : MonoBehaviour
{
    [SerializeField] private string cardId;

    public string CardId => cardId;

    public void SetCardId(string newCardId)
    {
        cardId = newCardId;
    }
}
