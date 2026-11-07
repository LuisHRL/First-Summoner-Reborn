
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Catalogue d'ennemis")]
    public EnemyDatabase enemyDatabase;

    [Header("Difficulté de la mission")]
    [Min(1)]
    public int missionDifficulty = 1;

    [Header("Configuration des apparitions")]
    public Transform spawnPoint;

    [Min(0)]
    public int enemiesInWave = 5;

    [Min(0f)]
    public float spawnRadius = 4f;

    [Header("Mode d'apparition")]
    public bool spawnAllAtOnce = true;

    [Min(0f)]
    public float timeBetweenSpawns = 1.5f;

    public bool IsWaveFinished { get; private set; }

    private List<EnemyDefinitions> availableEnemies =
        new List<EnemyDefinitions>();

    private IEnumerator Start()
    {
        IsWaveFinished = false;

        if (enemyDatabase == null || spawnPoint == null)
        {
            Debug.LogError(
                "EnemySpawner : EnemyDatabase ou SpawnPoint manquant."
            );

            yield break;
        }

        availableEnemies = GetAvailableEnemies();

        if (availableEnemies.Count == 0)
        {
            Debug.LogWarning(
                "EnemySpawner : aucun ennemi compatible avec la difficulté "
                + missionDifficulty
            );

            yield break;
        }

        for (int i = 0; i < enemiesInWave; i++)
        {
            SpawnEnemy();

            if (!spawnAllAtOnce && i < enemiesInWave - 1)
            {
                yield return new WaitForSeconds(timeBetweenSpawns);
            }
        }

        IsWaveFinished = true;
    }

    private List<EnemyDefinitions> GetAvailableEnemies()
    {
        List<EnemyDefinitions> result =
            new List<EnemyDefinitions>();

        foreach (EnemyDefinitions enemy in enemyDatabase.enemies)
        {
            if (enemy == null || enemy.prefab == null)
                continue;

            if (enemy.minimumDifficulty > missionDifficulty)
                continue;

            if (enemy.spawnWeight <= 0)
                continue;

            result.Add(enemy);
        }

        return result;
    }

    private EnemyDefinitions SelectRandomEnemy()
    {
        int totalWeight = 0;

        foreach (EnemyDefinitions enemy in availableEnemies)
        {
            totalWeight += enemy.spawnWeight;
        }

        if (totalWeight <= 0)
            return null;

        int randomValue = Random.Range(0, totalWeight);

        foreach (EnemyDefinitions enemy in availableEnemies)
        {
            randomValue -= enemy.spawnWeight;

            if (randomValue < 0)
                return enemy;
        }

        return null;
    }

    private void SpawnEnemy()
    {
        EnemyDefinitions selectedEnemy = SelectRandomEnemy();

        if (selectedEnemy == null)
            return;

        Vector2 randomCircle =
            Random.insideUnitCircle * spawnRadius;

        Vector3 spawnPosition = spawnPoint.position +
            new Vector3(randomCircle.x, 0f, randomCircle.y);

        GameObject spawnedEnemy = Instantiate(
            selectedEnemy.prefab,
            spawnPosition,
            Quaternion.identity
        );

        spawnedEnemy.name = selectedEnemy.enemyName;
    }
}
