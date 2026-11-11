
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
    [SerializeField] private Sprite idleFinalEnemySprite;
    private Sprite generatedIdleSprite;
    private Texture2D generatedIdleTexture;
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
        if (finalEnemyMarker == null)
            CreateFinalEnemyMarker();
    }

    private void CreateFinalEnemyMarker()
    {
        if (finalEnemyMarker != null) return;
        RectTransform parent = progressGauge != null ? progressGauge : transform as RectTransform;
        if (parent == null) return;

        GameObject marker = new GameObject("FinalEnemy", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(parent, false);
        finalEnemyMarker = marker.GetComponent<RectTransform>();
        finalEnemyMarker.anchorMin = new Vector2(0.5f, 1f);
        finalEnemyMarker.anchorMax = new Vector2(0.5f, 1f);
        finalEnemyMarker.pivot = new Vector2(0.5f, 0.5f);
        finalEnemyMarker.sizeDelta = new Vector2(38f, 38f);
        finalEnemyImage = marker.GetComponent<Image>();
        finalEnemyImage.raycastTarget = false;
        if (idleFinalEnemySprite == null)
            idleFinalEnemySprite = CreateWhiteRingSprite();
        finalEnemyImage.sprite = idleFinalEnemySprite;
        finalEnemyImage.color = Color.white;
        finalEnemySpriteCaptured = true;
        marker.SetActive(false);
        Debug.Log("[FinalEnemy] UI marker created under " + GetTransformPath(parent), this);
    }

    private Sprite CreateWhiteRingSprite()
    {
        const int size = 64;
        generatedIdleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedIdleTexture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            float outer = Mathf.Clamp01((29f - distance) * 1.5f);
            float inner = Mathf.Clamp01((distance - 23f) * 1.5f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, outer * inner);
        }
        generatedIdleTexture.SetPixels(pixels);
        generatedIdleTexture.Apply();
        generatedIdleSprite = Sprite.Create(generatedIdleTexture,
            new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedIdleSprite;
    }

    private void ResolveFinalEnemyMarker()
    {
        if (finalEnemyMarker == null && progressGauge != null)
            finalEnemyMarker = progressGauge.Find("FinalEnemy") as RectTransform;

        if (finalEnemyMarker == null && playerProgressMarker != null &&
            playerProgressMarker.parent != null)
            finalEnemyMarker = playerProgressMarker.parent.Find("FinalEnemy") as RectTransform;

        if (finalEnemyMarker == null)
            finalEnemyMarker = transform.Find("FinalEnemy") as RectTransform;

        if (finalEnemyMarker == null)
        {
            // Search the active scene hierarchy, including inactive UI objects.
            var roots = gameObject.scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                RectTransform[] markers = root.GetComponentsInChildren<RectTransform>(true);
                foreach (RectTransform marker in markers)
                {
                    if (marker.name != "FinalEnemy") continue;
                    finalEnemyMarker = marker;
                    break;
                }
                if (finalEnemyMarker != null) break;
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

    private static string GetTransformPath(Transform item)
    {
        if (item == null) return "null";
        string path = item.name;
        while (item.parent != null)
        {
            item = item.parent;
            path = item.name + "/" + path;
        }
        return path;
    }

    private void OnFinalEnemyDeath()
    {
        finalEnemyKilled = true;
        if (finalEnemyMarker != null)
            finalEnemyMarker.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (generatedIdleSprite != null) Destroy(generatedIdleSprite);
        if (generatedIdleTexture != null) Destroy(generatedIdleTexture);
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
        {
            ResolveFinalEnemyMarker();
            if (finalEnemyMarker == null)
                CreateFinalEnemyMarker();
        }
        if (finalEnemyMarker == null && !loggedMissingMarker)
        {
            Debug.LogWarning("[FinalEnemy] UI marker absent. gauge=" +
                (progressGauge != null ? GetTransformPath(progressGauge) : "null") +
                " playerMarker=" +
                (playerProgressMarker != null ? GetTransformPath(playerProgressMarker) : "null") +
                " script=" + GetTransformPath(transform), this);
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

        // Track the enemy's current world position, not its original spawn position.
        // Use the gauge's own local coordinates to avoid top-anchor offsets.
        if (finalEnemyMarker == null)
            ResolveFinalEnemyMarker();
        if (finalEnemyMarker == null)
            return;

        // The player triangle already uses the correct gauge coordinate system.
        // Match its anchors instead of using the gauge RectTransform pivot.
        RectTransform markerParent = playerProgressMarker.parent as RectTransform;
        if (markerParent != null && finalEnemyMarker.parent != markerParent)
            finalEnemyMarker.SetParent(markerParent, false);

        finalEnemyMarker.anchorMin = playerProgressMarker.anchorMin;
        finalEnemyMarker.anchorMax = playerProgressMarker.anchorMax;
        finalEnemyMarker.pivot = new Vector2(0.5f, 0.5f);

        float enemyZ = trackedEnemy != null ? trackedEnemy.transform.position.z : endZ;
        float enemyProgress = Mathf.Clamp01((enemyZ - startZ) / (endZ - startZ));
        float markerY = enemyProgress * progressGauge.rect.height;
        // Keep the marker centered on the gauge's vertical axis.
        Vector2 gaugeCenterInParent = Vector2.zero;
        if (markerParent != null)
            gaugeCenterInParent = markerParent.InverseTransformPoint(progressGauge.TransformPoint(progressGauge.rect.center));
        Vector2 anchorReference = Vector2.zero;
        if (markerParent != null)
        {
            Rect parentRect = markerParent.rect;
            anchorReference = new Vector2(
                parentRect.xMin + parentRect.width * finalEnemyMarker.anchorMin.x,
                parentRect.yMin + parentRect.height * finalEnemyMarker.anchorMin.y);
        }
        finalEnemyMarker.anchoredPosition = new Vector2(
            gaugeCenterInParent.x - anchorReference.x, markerY);

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
