
using UnityEngine;
using UnityEngine.UI;

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
    [Header("Marqueur du dernier ennemi")]
    [SerializeField] private RectTransform finalEnemyMarker;
    [SerializeField] private Sprite finalEnemySprite;
    private Image finalEnemyImage;

    private void Awake()
    {
        ResolveFinalEnemyMarker();
    }

    private void ResolveFinalEnemyMarker()
    {
        if (finalEnemyMarker == null)
        {
            RectTransform[] markers = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform marker in markers)
            {
                if (marker.name == "FinalEnemy")
                {
                    finalEnemyMarker = marker;
                    break;
                }
            }
        }
        if (finalEnemyMarker != null)
        {
            finalEnemyImage = finalEnemyMarker.GetComponent<Image>();
            if (finalEnemyImage != null && finalEnemySprite != null)
            {
                finalEnemyImage.sprite = finalEnemySprite;
                finalEnemyImage.color = Color.white;
            }
        }
    }

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

        // The top of the gauge represents the last spawned enemy, not the terrain edge.
        if (finalEnemyMarker == null)
            ResolveFinalEnemyMarker();
        if (finalEnemyMarker != null)
        {
            Vector2 endPosition = finalEnemyMarker.anchoredPosition;
            endPosition.y = progressGauge.rect.height;
            finalEnemyMarker.anchoredPosition = endPosition;
            if (finalEnemyImage != null && finalEnemySprite != null && finalEnemyImage.sprite != finalEnemySprite)
            {
                finalEnemyImage.sprite = finalEnemySprite;
                finalEnemyImage.color = Color.white;
            }
        }
    }
}
