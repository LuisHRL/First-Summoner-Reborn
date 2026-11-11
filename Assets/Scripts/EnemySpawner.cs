
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
    [Header("Groupes repartis sur le parcours")]
    public bool distributeAlongTerrain = true;
    [Min(1)] public int desiredGroupCount = 4;
    [Min(0f)] public float firstGroupDistanceAfterSafeZone = 10f;
    [Min(0f)] public float endOfTerrainMargin = 12f;
    [Min(0f)] public float groupLateralRadius = 3f;

    public bool spawnAllAtOnce = true;

    [Min(0f)]
    public float timeBetweenSpawns = 1.5f;

    [Header("Zone protégée")]
    public bool respectStartingSafeZone = true;
    [Min(0f)] public float safeZoneMargin = 1f;
    [Min(1)] public int spawnPositionAttempts = 24;

    public bool IsWaveFinished { get; private set; }
    public float FurthestSpawnZ { get; private set; } = float.NegativeInfinity;
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
        if ((!distributeAlongTerrain && spawnPoint == null) || (!missionConfigured && enemyDatabase == null))
        {
            Debug.LogError(
                "EnemySpawner : EnemyDatabase ou SpawnPoint manquant."
            );

            yield break;
        }

        List<GameObject> planned = missionConfigured
            ? BuildMissionPlan(mission)
            : BuildDatabasePlan();

        if (planned.Count == 0)
        {
            Debug.LogWarning("EnemySpawner : aucun ennemi compatible à générer.", this);
            IsWaveFinished = true;
            yield break;
        }

        Terrain terrain = Terrain.activeTerrain;
        ProceduralTerrainGenerator generator = FindAnyObjectByType<ProceduralTerrainGenerator>();
        bool useGroups = distributeAlongTerrain && terrain != null && terrain.terrainData != null;
        if (distributeAlongTerrain && !useGroups)
            Debug.LogWarning("EnemySpawner : terrain absent, retour au SpawnPoint.", this);

        int groupCount = useGroups ? Mathf.Min(Mathf.Max(1, desiredGroupCount), planned.Count) : 1;
        float safeLength = mission != null ? mission.startingSafeZoneMeters :
            (generator != null ? generator.safeZoneLength : 0f);
        float startZ = 0f;
        float endZ = 0f;
        if (useGroups)
        {
            startZ = Mathf.Max(0f, safeLength + safeZoneMargin + firstGroupDistanceAfterSafeZone);
            endZ = terrain.terrainData.size.z - endOfTerrainMargin;
            if (startZ > endZ)
            {
                Debug.LogWarning("EnemySpawner : terrain trop court pour répartir les groupes.", this);
                IsWaveFinished = true;
                yield break;
            }
        }

        // Distribute round-robin to avoid putting all enemies of one type together.
        List<GameObject>[] groups = new List<GameObject>[groupCount];
        for (int i = 0; i < groupCount; i++)
            groups[i] = new List<GameObject>();
        for (int i = 0; i < planned.Count; i++)
            groups[i % groupCount].Add(planned[i]);

        for (int i = 0; i < groupCount; i++)
        {
            float centerZ = groupCount == 1 ? (startZ + endZ) * 0.5f :
                Mathf.Lerp(startZ, endZ, (float)i / (groupCount - 1));
            EnemyAggroGroup group = aggroGroup;
            if (useGroups)
            {
                GameObject groupObject = new GameObject("EnemyGroup_" + (i + 1));
                groupObject.transform.SetParent(transform, false);
                group = groupObject.AddComponent<EnemyAggroGroup>();
            }

            foreach (GameObject prefab in groups[i])
            {
                Vector3 position;
                bool valid = useGroups
                    ? TryFindGroupPosition(terrain, generator, centerZ, safeLength, out position)
                    : TryFindSpawnPosition(out position);
                if (valid)
                    SpawnPrefabAt(prefab, prefab.name, position, group);
                else
                    Debug.LogWarning("EnemySpawner : position de groupe invalide.", this);
                if (!spawnAllAtOnce)
                    yield return new WaitForSeconds(timeBetweenSpawns);
            }
        }

        IsWaveFinished = true;
    }

    private List<GameObject> BuildDatabasePlan()
    {
        missionDifficulty = Mathf.Max(1, GameSession.SelectedDifficulty);
        availableEnemies = GetAvailableEnemies();
        List<GameObject> result = new List<GameObject>();
        for (int i = 0; i < GetScaledWaveCount(); i++)
        {
            EnemyDefinitions enemy = SelectRandomEnemy();
            if (enemy != null) result.Add(enemy.prefab);
        }
        return result;
    }

    private int GetScaledWaveCount()
    {
        int level = Mathf.Max(1, GameSession.SelectedDifficulty);
        return Mathf.Max(0, enemiesInWave +
            (scaleWaveWithSelectedDifficulty ? (level - 1) * additionalEnemiesPerDifficultyLevel : 0));
    }

    private List<GameObject> BuildMissionPlan(MissionDefinition mission)
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

        return planned;
    }

    private bool TryFindGroupPosition(Terrain terrain, ProceduralTerrainGenerator generator,
        float centerZ, float safeLength, out Vector3 position)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float minZ = origin.z + (respectStartingSafeZone ? safeLength + safeZoneMargin : 0f);
        float maxZ = origin.z + size.z - Mathf.Max(0f, endOfTerrainMargin);
        if (minZ > maxZ || size.x <= 1f)
        {
            position = default;
            return false;
        }

        float localZ = Mathf.Clamp(centerZ + Random.Range(-groupLateralRadius, groupLateralRadius),
            minZ - origin.z, maxZ - origin.z);
        float pathX = generator != null ? generator.PathCenter(localZ) : size.x * 0.5f;
        float localX = Mathf.Clamp(pathX + Random.Range(-groupLateralRadius, groupLateralRadius),
            0.5f, size.x - 0.5f);
        position = new Vector3(origin.x + localX, 0f, origin.z + localZ);
        position.y = terrain.SampleHeight(position) + origin.y;
        return true;
    }

    private void SpawnPrefabAt(GameObject prefab, string displayName,
        Vector3 position, EnemyAggroGroup group)
    {
        if (prefab == null) return;
        GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
        spawned.name = displayName;
        FurthestSpawnZ = Mathf.Max(FurthestSpawnZ, position.z);
        EnemyAI ai = spawned.GetComponent<EnemyAI>();
        if (ai != null) ai.AssignAggroGroup(group);
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