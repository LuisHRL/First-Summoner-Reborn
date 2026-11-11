
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

    [Header("Progression par difficulté")]
    public bool scaleWaveWithSelectedDifficulty = true;
    [Min(0)] public int additionalEnemiesPerDifficultyLevel = 2;

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

    [Header("Zone protégée")]
    public bool respectStartingSafeZone = true;
    [Min(0f)] public float safeZoneMargin = 1f;
    [Min(1)] public int spawnPositionAttempts = 24;

    public bool IsWaveFinished { get; private set; }
    private EnemyAggroGroup aggroGroup;

    private void Awake()
    {
        aggroGroup = GetComponent<EnemyAggroGroup>();
        if (aggroGroup == null)
            aggroGroup = gameObject.AddComponent<EnemyAggroGroup>();
    }

    private List<EnemyDefinitions> availableEnemies =
        new List<EnemyDefinitions>();

    private IEnumerator Start()
    {
        IsWaveFinished = false;

        MissionDefinition mission = GameSession.SelectedMission;
        bool missionConfigured = mission != null && mission.enemies != null && mission.enemies.Length > 0;
        if (spawnPoint == null || (!missionConfigured && enemyDatabase == null))
        {
            Debug.LogError(
                "EnemySpawner : EnemyDatabase ou SpawnPoint manquant."
            );

            yield break;
        }

        if (missionConfigured)
        {
            yield return SpawnMissionEnemies(mission);
            IsWaveFinished = true;
            yield break;
        }

        missionDifficulty = Mathf.Max(1, GameSession.SelectedDifficulty);
        availableEnemies = GetAvailableEnemies();

        if (availableEnemies.Count == 0)
        {
            Debug.LogWarning(
                "EnemySpawner : aucun ennemi compatible avec la difficulté "
                + missionDifficulty
            );

            yield break;
        }

        int waveCount = GetScaledWaveCount();
        for (int i = 0; i < waveCount; i++)
        {
            SpawnEnemy();

            if (!spawnAllAtOnce && i < waveCount - 1)
            {
                yield return new WaitForSeconds(timeBetweenSpawns);
            }
        }

        IsWaveFinished = true;
    }

    private int GetScaledWaveCount()
    {
        int level = Mathf.Max(1, GameSession.SelectedDifficulty);
        return Mathf.Max(0, enemiesInWave +
            (scaleWaveWithSelectedDifficulty ? (level - 1) * additionalEnemiesPerDifficultyLevel : 0));
    }

    private IEnumerator SpawnMissionEnemies(MissionDefinition mission)
    {
        // Each entry is an explicit allowed prefab, with independent min/max limits.
        List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>();
        List<int> counts = new List<int>();
        int minimumTotal = 0;
        foreach (EnemySpawnEntry entry in mission.enemies)
        {
            if (entry == null || entry.prefab == null || entry.weight <= 0)
                continue;
            int minimum = Mathf.Clamp(entry.minimumCount, 0, Mathf.Max(0, entry.maximumCount));
            entries.Add(entry);
            counts.Add(minimum);
            minimumTotal += minimum;
        }

        int total = Mathf.Max(minimumTotal, GetScaledWaveCount());
        int remaining = total - minimumTotal;
        while (remaining > 0)
        {
            int weightSum = 0;
            for (int i = 0; i < entries.Count; i++)
                if (counts[i] < Mathf.Max(0, entries[i].maximumCount))
                    weightSum += entries[i].weight;
            if (weightSum <= 0) break;

            int roll = Random.Range(0, weightSum);
            for (int i = 0; i < entries.Count; i++)
            {
                if (counts[i] >= Mathf.Max(0, entries[i].maximumCount)) continue;
                roll -= entries[i].weight;
                if (roll >= 0) continue;
                counts[i]++;
                remaining--;
                break;
            }
        }

        List<GameObject> planned = new List<GameObject>();
        for (int i = 0; i < entries.Count; i++)
            for (int n = 0; n < counts[i]; n++)
                planned.Add(entries[i].prefab);

        // Shuffle so minimum quotas do not force a fixed spawn order.
        for (int i = planned.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            GameObject temp = planned[i];
            planned[i] = planned[j];
            planned[j] = temp;
        }

        for (int i = 0; i < planned.Count; i++)
        {
            SpawnPrefab(planned[i], planned[i].name);
            if (!spawnAllAtOnce && i < planned.Count - 1)
                yield return new WaitForSeconds(timeBetweenSpawns);
        }
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

        SpawnPrefab(selectedEnemy.prefab, selectedEnemy.enemyName);
    }

    private void SpawnPrefab(GameObject prefab, string displayName)
    {
        if (prefab == null) return;
        Vector3 spawnPosition;
        if (!TryFindSpawnPosition(out spawnPosition))
        {
            Debug.LogWarning("EnemySpawner : aucun emplacement valide hors de la zone protégée.", this);
            return;
        }

        GameObject spawnedEnemy = Instantiate(
            prefab,
            spawnPosition,
            Quaternion.identity
        );

        spawnedEnemy.name = displayName;
        EnemyAI ai = spawnedEnemy.GetComponent<EnemyAI>();
        if (ai != null) ai.AssignAggroGroup(aggroGroup);
    }
    private bool TryFindSpawnPosition(out Vector3 position)
    {
        position = spawnPoint.position;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("EnemySpawner : aucun terrain actif pour vérifier la zone protégée.", this);
            return !respectStartingSafeZone;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float safeLength = 0f;
        if (respectStartingSafeZone)
        {
            var mission = GameSession.SelectedMission;
            if (mission != null)
                safeLength = mission.startingSafeZoneMeters;
            else
            {
                var generator = FindAnyObjectByType<ProceduralTerrainGenerator>();
                if (generator != null) safeLength = generator.safeZoneLength;
            }
        }

        float minimumZ = origin.z + safeLength + (respectStartingSafeZone ? safeZoneMargin : 0f);
        float maximumZ = origin.z + size.z;
        if (minimumZ > maximumZ) return false;

        for (int attempt = 0; attempt < Mathf.Max(1, spawnPositionAttempts); attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = spawnPoint.position + new Vector3(offset.x, 0f, offset.y);
            if (candidate.x < origin.x || candidate.x > origin.x + size.x ||
                candidate.z < minimumZ || candidate.z > maximumZ)
                continue;
            candidate.y = terrain.SampleHeight(candidate) + origin.y;
            position = candidate;
            return true;
        }

        position.x = Mathf.Clamp(spawnPoint.position.x, origin.x, origin.x + size.x);
        position.z = Mathf.Clamp(spawnPoint.position.z, minimumZ, maximumZ);
        position.y = terrain.SampleHeight(position) + origin.y;
        return true;
    }

}