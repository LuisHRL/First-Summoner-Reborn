
using UnityEngine;

public class LevelProgressGauge : MonoBehaviour
{
    [Header("Progression du niveau")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform levelStart;
    [SerializeField] private Transform levelEnd;

    [Header("Interface")]
    [SerializeField] private RectTransform playerProgressMarker;
    [SerializeField] private RectTransform progressGauge;

    [Header("Synchronisation avec les groupes")]
    [SerializeField] private bool useLastEnemyAsLevelEnd = true;
    private EnemySpawner spawner;

    private void Update()
    {
        if (player == null || playerProgressMarker == null ||
            progressGauge == null)
            return;

        // Progression sur l'axe du chemin (début -> fin).
        if (spawner == null)
            spawner = FindAnyObjectByType<EnemySpawner>();

        Terrain terrain = Terrain.activeTerrain;
        float startZ = levelStart != null ? levelStart.position.z :
            (terrain != null ? terrain.transform.position.z : player.position.z);
        float endZ = levelEnd != null ? levelEnd.position.z :
            (terrain != null ? terrain.transform.position.z + terrain.terrainData.size.z : startZ);
        if (useLastEnemyAsLevelEnd && spawner != null &&
            !float.IsNegativeInfinity(spawner.FurthestSpawnZ))
            endZ = spawner.FurthestSpawnZ;

        Vector3 direction = Vector3.forward * (endZ - startZ);
        float lengthSquared = direction.sqrMagnitude;

        if (lengthSquared < 0.001f)
            return;

        float progress = Vector3.Dot(
            Vector3.forward * (player.position.z - startZ),
            direction
        ) / lengthSquared;

        progress = Mathf.Clamp01(progress);

        // Le triangle est ancré en bas de l'échelle.
        Vector2 position = playerProgressMarker.anchoredPosition;
        position.y = progress * progressGauge.rect.height;

        playerProgressMarker.anchoredPosition = position;
    }
}
