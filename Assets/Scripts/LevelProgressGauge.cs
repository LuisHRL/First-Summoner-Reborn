
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
    private Sprite idleFinalEnemySprite;
    private bool finalEnemySpriteCaptured;
    private bool finalEnemyWasSpawned;
    private GameObject watchedFinalEnemy;
    private EnemyHealth watchedHealth;
    private bool finalEnemyKilled;
    private bool loggedMissingMarker;
    private bool loggedMissingEnemy;
    private bool? previousEngaged;
    private bool? previousVisible;

    private void Awake()
    {
        ResolveFinalEnemyMarker();
    }

    private void ResolveFinalEnemyMarker()
    {
        if (finalEnemyMarker == null)
            finalEnemyMarker = transform.Find("FinalEnemy") as RectTransform;
        if (finalEnemyMarker == null)
        {
            // Fallback for nonstandard scene layouts.
            RectTransform[] markers = Resources.FindObjectsOfTypeAll<RectTransform>();
            foreach (RectTransform marker in markers)
            {
                if (marker.name == "FinalEnemy" && marker.gameObject.scene.IsValid() &&
                    marker.gameObject.scene == gameObject.scene)
                {
                    finalEnemyMarker = marker;
                    break;
                }
            }
        }

        if (finalEnemyMarker != null)
        {
            finalEnemyImage = finalEnemyMarker.GetComponent<Image>();
            if (finalEnemyImage != null && !finalEnemySpriteCaptured)
            {
                idleFinalEnemySprite = finalEnemyImage.sprite;
                finalEnemySpriteCaptured = true;
            }
        }
    }

    private void OnFinalEnemyDeath()
    {
        finalEnemyKilled = true;
        if (finalEnemyMarker != null)
            finalEnemyMarker.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (watchedHealth != null)
            watchedHealth.OnDeath.RemoveListener(OnFinalEnemyDeath);
    }

    private void Update()
    {
        // Marker lifetime is independent of gauge movement and player references.
        if (spawner == null)
        {
            EnemySpawner[] spawners = FindObjectsByType<EnemySpawner>();
            foreach (EnemySpawner candidate in spawners)
                if (spawner == null || candidate.FurthestSpawnZ > spawner.FurthestSpawnZ)
                    spawner = candidate;
        }
        if (finalEnemyMarker == null)
            ResolveFinalEnemyMarker();
        if (finalEnemyMarker == null && !loggedMissingMarker)
        {
            Debug.LogWarning("[FinalEnemy] UI marker not found under ProgressGauge or scene.", this);
            loggedMissingMarker = true;
        }

        if (spawner != null && !float.IsNegativeInfinity(spawner.FurthestSpawnZ))
            finalEnemyWasSpawned = true;

        GameObject trackedEnemy = spawner != null ? spawner.FinalEnemy : null;
        if (trackedEnemy != null && watchedFinalEnemy != trackedEnemy)
        {
            if (watchedHealth != null)
                watchedHealth.OnDeath.RemoveListener(OnFinalEnemyDeath);
            watchedFinalEnemy = trackedEnemy;
            watchedHealth = trackedEnemy.GetComponent<EnemyHealth>();
            finalEnemyKilled = false;
            if (watchedHealth != null)
                watchedHealth.OnDeath.AddListener(OnFinalEnemyDeath);
        }

        if (spawner != null && spawner.IsWaveFinished && trackedEnemy == null &&
            !finalEnemyWasSpawned && !loggedMissingEnemy)
        {
            Debug.LogWarning("[FinalEnemy] Spawner finished without a tracked final enemy.", spawner);
            loggedMissingEnemy = true;
        }

        bool enemyAlive = !finalEnemyKilled && trackedEnemy != null &&
            trackedEnemy.activeInHierarchy &&
            (watchedHealth == null || watchedHealth.CurrentHealth > 0);
        bool spawning = spawner != null && !spawner.IsWaveFinished && !finalEnemyWasSpawned;
        bool showMarker = enemyAlive || spawning;
        if (previousVisible != showMarker)
        {
            Debug.Log("[FinalEnemy] visible=" + showMarker + " enemy=" +
                (trackedEnemy != null ? trackedEnemy.name : "null") +
                " waveFinished=" + (spawner != null && spawner.IsWaveFinished), this);
            previousVisible = showMarker;
        }
        if (finalEnemyMarker != null && finalEnemyMarker.gameObject.activeSelf != showMarker)
            finalEnemyMarker.gameObject.SetActive(showMarker);

        if (!showMarker)
            return;

        if (player == null || playerProgressMarker == null ||
            progressGauge == null)
            return;

        // Progression sur l'axe du chemin (début -> fin).
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

        // Keep the final marker aligned with the last enemy's original position.
        if (finalEnemyMarker == null)
            ResolveFinalEnemyMarker();
        if (finalEnemyMarker == null)
            return;

        Vector2 endPosition = finalEnemyMarker.anchoredPosition;
        endPosition.y = progressGauge.rect.height;
        finalEnemyMarker.anchoredPosition = endPosition;

        // Default ring before aggro; dedicated red sprite during combat.
        bool engaged = enemyAlive && spawner.FinalEnemyGroup != null &&
            spawner.FinalEnemyGroup.IsEngaged;
        if (previousEngaged != engaged)
        {
            Debug.Log("[FinalEnemy] engaged=" + engaged + " group=" +
                (spawner.FinalEnemyGroup != null ? spawner.FinalEnemyGroup.name : "null") +
                " redSprite=" + (finalEnemySprite != null ? finalEnemySprite.name : "null") +
                " image=" + (finalEnemyImage != null ? finalEnemyImage.name : "null"), this);
            previousEngaged = engaged;
        }
        if (finalEnemyImage != null)
        {
            Sprite desiredSprite = engaged && finalEnemySprite != null
                ? finalEnemySprite : idleFinalEnemySprite;
            if (finalEnemyImage.sprite != desiredSprite)
                finalEnemyImage.sprite = desiredSprite;
            finalEnemyImage.color = Color.white;
        }
    }
}
