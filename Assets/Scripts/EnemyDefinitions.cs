
using UnityEngine;

public enum EnemyCategory
{
    Normal,
    Elite,
    Boss
}

[CreateAssetMenu(
    fileName = "NewEnemy",
    menuName = "First Summoner/Enemy Definition"
)]
public class EnemyDefinitions : ScriptableObject
{
    [Header("Identité")]
    public string enemyName;
    public GameObject prefab;

    [Header("Classification")]
    public EnemyCategory category = EnemyCategory.Normal;

    [Min(1)]
    public int minimumDifficulty = 1;

    [Header("Statistiques")]
    [Min(1)]
    public int maxHealth = 100;

    [Min(0)]
    public int damage = 10;

    [Header("Apparition")]
    [Min(0)]
    public int spawnWeight = 10;
}
