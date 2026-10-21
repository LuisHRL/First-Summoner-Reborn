
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewSummonCard",
    menuName = "First Summoner/Summon Card"
)]
public class SummonCardDefinition : ScriptableObject
{
    [Header("Identification")]
    public string cardId;
    public string cardName;

    [TextArea(3, 6)]
    public string description;

    [Header("Invocation")]
    public GameObject minionPrefab;
    public int manaCost = 25;

    [Header("Statistiques")]
    public string rarity = "Common";
    public int level = 1;

    public string summonType = "Ground";

    public int attackDamage = 10;
    public float attackSpeed = 2f;

    public bool isRanged = false;
    public float attackRange = 1f;

    public int health = 100;
    public float movementSpeed = 3f;

    public int numberOfUnits = 1;

    [Header("Apparence")]
    public Sprite cardArtwork;
}
