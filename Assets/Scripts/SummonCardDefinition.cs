
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewSummonCard",
    menuName = "First Summoner/Summon Card"
)]
public class SummonCardDefinition : ScriptableObject
{
    [Header("Identification")]
    public string cardId;

    [Header("Invocation")]
    public GameObject minionPrefab;
    public int manaCost = 25;

    [Header("Apparence")]
    public Sprite cardArtwork;
}
