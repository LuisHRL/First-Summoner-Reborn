using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform spawnPoint;
    public int enemiesInWave = 5;
    public float spawnRadius = 4f;

    [Header("Mode d'apparition")]
    public bool spawnAllAtOnce = true;
    public float timeBetweenSpawns = 1.5f;

    public bool IsWaveFinished { get; private set; }

    IEnumerator Start()
    {
        IsWaveFinished = false;

        if (spawnAllAtOnce)
        {
            for (int i = 0; i < enemiesInWave; i++)
            {
                SpawnEnemy();
            }
        }
        else
        {
            for (int i = 0; i < enemiesInWave; i++)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(timeBetweenSpawns);
            }
        }

        IsWaveFinished = true;
    }

    void SpawnEnemy()
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;

        Vector3 spawnPosition = spawnPoint.position + new Vector3(
            randomCircle.x,
            0,
            randomCircle.y
        );

        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
    }
}