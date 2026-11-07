
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyDatabase",
    menuName = "First Summoner/Enemy Database"
)]
public class EnemyDatabase : ScriptableObject
{
    [Header("Collection d'ennemis")]
    public List<EnemyDefinitions> enemies = new List<EnemyDefinitions>();
}
