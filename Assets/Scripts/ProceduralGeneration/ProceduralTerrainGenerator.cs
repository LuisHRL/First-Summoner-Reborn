
using UnityEngine;

public class ProceduralTerrainGenerator : MonoBehaviour
{
    [Header("Dimensions du terrain")]
    [Min(20f)]
    public float terrainWidth = 60f;

    [Min(20f)]
    public float terrainLength = 200f;

    [Header("Chemin principal")]
    [Min(2f)]
    public float pathWidth = 6f;

    [Header("Transition du chemin")]
    [Range(0.1f, 5f)]
    public float pathBlendWidth = 2f;


    [Header("Courbes du chemin")]
    [Range(0f, 15f)]
    public float pathCurvature = 6f;

    [Range(0.5f, 5f)]
    public float pathFrequency = 1.5f;


    [Header("Safe zone du chemin")]
    [Min(0f)] public float safeZoneLength = 15f;
    [Min(0f)] public float safeZoneTransitionLength = 12f;

    private float PathCenter(float z)
    {
        float start = Mathf.Clamp(safeZoneLength, 0f, terrainLength);
        float transition = Mathf.Max(0.001f, safeZoneTransitionLength);
        float t = Mathf.Clamp01((z - start) / transition);
        float blend = t * t * (3f - 2f * t);
        // The sine phase starts at zero at the end of the safe zone.
        float curvedDistance = Mathf.Max(0f, z - start);
        float phase = curvedDistance / Mathf.Max(1f, terrainLength) *
            Mathf.PI * 2f * pathFrequency;
        return terrainWidth * 0.5f + Mathf.Sin(phase) * pathCurvature * blend;
    }

    private float PathSlope(float z)
    {
        float step = Mathf.Max(0.01f, terrainLength / 4096f);
        return (PathCenter(Mathf.Min(terrainLength, z + step)) -
            PathCenter(Mathf.Max(0f, z - step))) /
            Mathf.Max(0.0001f, Mathf.Min(terrainLength, z + step) -
                Mathf.Max(0f, z - step));
    }

    [Header("Relief")]
    [Min(0f)]
    public float terrainHeight = 10f;

    [Header("Relief optionnel")]
    [Tooltip("Hauteur maximale du relief en metres. Zero donne un sol totalement plat, chemin compris.")]
    [Min(0f)] public float terrainReliefAmplitude = 0f;


    [Header("Zones rocheuses")]
    [Range(0f, 1f)]
    public float rockThreshold = 0.65f;

    [Range(1f, 20f)]
    public float rockNoiseScale = 7f;


    [Header("Génération")]
    public int seed = 12345;
    public bool randomizeSeed = true;

    [Header("Collision et déplacement")]
    [Tooltip("Nom du layer utilisé par les raycasts de déplacement (Click To Move > Ground Layer).")]
    public string walkableLayerName = "Ground";

    [Header("Textures du terrain")]
    public TerrainLayer[] terrainLayers;

    [Header("Forêt procédurale")]
    public GameObject[] treePrefabs;

    [Header("Végétation procédurale")]
    public GameObject[] vegetationPrefabs;

    [Min(0)]
    public int vegetationCount = 800;

    [Min(0f)]
    public float vegetationPathClearance = 1.5f;

    [Header("Densité naturelle de la végétation")]
    [Range(1f, 20f)]
    public float vegetationNoiseScale = 5f;

    [Range(0f, 1f)]
    public float vegetationDensityThreshold = 0.35f;

    [Min(0)]
    public int treeCount = 300;

    [Min(0f)]
    public float treePathClearance = 3f;

    [Header("Densité naturelle des arbres")]
    [Range(1f, 20f)]
    public float treeNoiseScale = 3f;

    [Range(0f, 1f)]
    public float treeDensityThreshold = 0.3f;


    [Header("Torches procédurales")]
    [Tooltip("Prefab de torche à placer le long du chemin. Aucun objet n'est créé si ce champ est vide.")]
    public GameObject torchPrefab;

    [Min(0)]
    public int torchCount = 12;

    [Min(0f)]
    public float torchPathOffset = 0.25f;

    [Min(0f)]
    public float torchEndMargin = 5f;

    [Tooltip("Alterner les côtés du chemin pour chaque torche.")]
    public bool alternateTorchSides = true;

    [Header("Budget des ombres des torches")]
    public bool useAdaptiveTorchShadows = false;
    [Min(0)] public int maximumTorchShadowLights = 2;
    [Min(0f)] public float torchShadowDistance = 18f;

    private GameObject generatedTerrain;

    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        // 1. Supprimer les anciens terrains générés
        // Fonctionne même après une recompilation Unity
        Transform previousTerrain;

        while ((previousTerrain = transform.Find("GeneratedTerrain")) != null)
        {
            if (Application.isPlaying)
            {
                previousTerrain.SetParent(null);
                previousTerrain.gameObject.SetActive(false);
                Destroy(previousTerrain.gameObject);
            }
            else
            {
                DestroyImmediate(previousTerrain.gameObject);
            }
        }

        generatedTerrain = null;

        // 2. Créer les données du terrain
        TerrainData terrainData = new TerrainData();

        terrainData.heightmapResolution = 513;

        terrainData.size = new Vector3(
            terrainWidth,
            Mathf.Max(1f, terrainHeight),
            terrainLength
        );

        // 3. Initialiser la génération aléatoire
        int currentSeed = randomizeSeed
            ? Random.Range(0, int.MaxValue)
            : seed;

        System.Random rng = new System.Random(currentSeed);

        float offsetX = rng.Next(0, 100000);
        float offsetZ = rng.Next(0, 100000);

        // 4. Générer le relief procédural
        int resolution = terrainData.heightmapResolution;

        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (float)x / (resolution - 1);
                float normalizedZ = (float)z / (resolution - 1);

                // Relief naturel avec Perlin Noise
                float noise = Mathf.PerlinNoise(
                    normalizedX * 5f + offsetX,
                    normalizedZ * 12f + offsetZ
                );


                // Déplacer progressivement le centre du chemin
                // pour créer des courbes naturelles
                float pathCenterX = PathCenter(normalizedZ * terrainLength);

                // Distance par rapport au chemin sinueux
                float worldX = normalizedX * terrainWidth;

                float distanceFromCenter =
                    Mathf.Abs(worldX - pathCenterX);


                // Zone centrale plate pour le futur chemin
                float pathInfluence = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        pathWidth * 0.5f,
                        pathWidth * 1.5f,
                        distanceFromCenter
                    )
                );

                // A zero amplitude keeps the path and the surrounding forest on the same level.
                // Terrain heights are normalized by the TerrainData vertical size.
                heights[z, x] = Mathf.Clamp01(
                    noise * terrainReliefAmplitude * pathInfluence / terrainData.size.y);
            }
        }

        terrainData.SetHeights(0, 0, heights);

        // Préparer le placement procédural des arbres
        System.Random treeRandom = new System.Random(
            currentSeed + 1000
        );

        // Vérifier que des prefabs sont disponibles
        bool canGenerateTrees =
            treePrefabs != null &&
            treePrefabs.Length > 0 &&
            treeCount > 0;


        // Appliquer les couches de textures du pack MedievalRuins
        if (terrainLayers != null && terrainLayers.Length >= 2 && terrainLayers[0] != null && terrainLayers[1] != null)
        {
            // Remove null optional layers; indices 0 and 1 remain forest and path.
            var validLayers = new System.Collections.Generic.List<TerrainLayer>();
            foreach (TerrainLayer layer in terrainLayers)
                if (layer != null) validLayers.Add(layer);
            terrainData.terrainLayers = validLayers.ToArray();

            // Préparer la carte de répartition des textures
            terrainData.alphamapResolution = 512;

            int alphaResolution = terrainData.alphamapResolution;
            int layerCount = terrainData.terrainLayers.Length;

            float[,,] splatmap = new float[
                alphaResolution,
                alphaResolution,
                layerCount
            ];

            // Répartir les textures en bandes pour le premier test
            for (int z = 0; z < alphaResolution; z++)
            {
                for (int x = 0; x < alphaResolution; x++)
                {
                    float normalizedX = (float)x / (alphaResolution - 1);


                    // Position le long du terrain
                    float normalizedZ =
                        (float)z / (alphaResolution - 1);

                    // Retrouver le centre du chemin sinueux
                    float pathCenterX = PathCenter(normalizedZ * terrainLength);

                    float worldX = normalizedX * terrainWidth;

                    float distanceToPath =
                        Mathf.Abs(worldX - pathCenterX);

                    // Texture 1 : chemin
                    // Texture 0 : sol forestier

                    // Définir la transition entre le chemin et la forêt
                    float halfPathWidth = pathWidth * 0.5f;

                    float blendStart = Mathf.Max(
                        0f,
                        halfPathWidth - pathBlendWidth * 0.5f
                    );

                    float blendEnd =
                        halfPathWidth + pathBlendWidth * 0.5f;

                    // 1 au centre du chemin, 0 dans la forêt
                    float pathWeight = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(
                            blendStart,
                            blendEnd,
                            distanceToPath
                        )
                    );


                    // Générer des zones rocheuses irrégulières
                    float rockNoise = Mathf.PerlinNoise(
                        normalizedX * rockNoiseScale + offsetX,
                        normalizedZ * rockNoiseScale + offsetZ
                    );

                    // Intensité de la texture rocheuse
                    float rockWeight = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(
                            rockThreshold,
                            Mathf.Min(1f, rockThreshold + 0.15f),
                            rockNoise
                        )
                    );

                    // Keep all splat weights in [0, 1] and normalized to 1.
                    // Optional rock layer is ignored if only two layers are configured.
                    rockWeight = layerCount >= 3
                        ? Mathf.Clamp01(rockWeight) * (1f - pathWeight)
                        : 0f;
                    splatmap[z, x, 0] = Mathf.Max(0f, 1f - pathWeight - rockWeight);
                    splatmap[z, x, 1] = Mathf.Clamp01(pathWeight);
                    if (layerCount >= 3) splatmap[z, x, 2] = rockWeight;



                }
            }

            terrainData.SetAlphamaps(0, 0, splatmap);

        }


        else
        {
            Debug.LogWarning("[ProceduralTerrainGenerator] Assign at least two TerrainLayers: forest ground and path. Optional third layer: rock.", this);
        }

        // 5. Créer le terrain Unity
        generatedTerrain =
            Terrain.CreateTerrainGameObject(terrainData);

        generatedTerrain.name = "GeneratedTerrain";

        // Terrain.CreateTerrainGameObject normally creates this collider. Ensure
        // it is present, enabled and bound to the newly generated heightmap.
        TerrainCollider terrainCollider = generatedTerrain.GetComponent<TerrainCollider>();
        if (terrainCollider == null)
            terrainCollider = generatedTerrain.AddComponent<TerrainCollider>();
        terrainCollider.terrainData = terrainData;
        terrainCollider.enabled = true;
        int walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        if (walkableLayer >= 0)
            generatedTerrain.layer = walkableLayer;
        else
        {
            generatedTerrain.layer = gameObject.layer;
            Debug.LogWarning(
                "[ProceduralTerrainGenerator] Walkable layer '" + walkableLayerName +
                "' does not exist. Using generator layer; verify Click To Move > Ground Layer.", this);
        }

        generatedTerrain.transform.SetParent(
            transform,
            false
        );

        // Place torches deterministically along the existing curved path.
        // Their visual effects and lights are provided by the assigned prefab.
        if (torchPrefab != null && torchCount > 0)
        {
            Transform torchesParent = new GameObject("GeneratedTorches").transform;
            torchesParent.SetParent(generatedTerrain.transform, false);
            PersistentParticleEffects torchParticles = torchesParent.gameObject.AddComponent<PersistentParticleEffects>();
            if (useAdaptiveTorchShadows)
            {
                AdaptiveLightShadows shadowBudget = torchesParent.gameObject.AddComponent<AdaptiveLightShadows>();
                shadowBudget.maximumShadowCastingLights = maximumTorchShadowLights;
                shadowBudget.maximumShadowDistance = torchShadowDistance;
            }

            float usableLength = Mathf.Max(0f, terrainLength - 2f * torchEndMargin);
            if (usableLength <= 0f)
            {
                Debug.LogWarning("[ProceduralTerrainGenerator] Torch end margin leaves no usable path.", this);
            }
            else
            {
                System.Random torchRandom = new System.Random(currentSeed ^ 975318);
                for (int i = 0; i < torchCount; i++)
                {
                    float pathZ = torchEndMargin + usableLength * (i + 0.5f) / torchCount;
                    float normalizedZ = pathZ / terrainLength;
                    float centerX = PathCenter(pathZ);

                    // Normal to the curved path, not just a horizontal X offset.
                    // This places the torch at the actual edge on tight bends.
                    float slope = PathSlope(pathZ);
                    float normalLength = Mathf.Sqrt(1f + slope * slope);
                    int side = alternateTorchSides ? (i % 2 == 0 ? -1 : 1)
                        : (torchRandom.Next(2) == 0 ? -1 : 1);
                    float edgeDistance = pathWidth * 0.5f + torchPathOffset;
                    float x = centerX + side * edgeDistance / normalLength;
                    float z = pathZ - side * edgeDistance * slope / normalLength;
                    if (x < 0f || x > terrainWidth || z < 0f || z > terrainLength)
                        continue;

                    float y = terrainData.GetInterpolatedHeight(x / terrainWidth, z / terrainLength);
                    GameObject torch = Instantiate(torchPrefab, torchesParent);
                    torch.name = torchPrefab.name + "_" + i;
                    torch.transform.localPosition = new Vector3(x, y, z);
                    // Face the torch toward the path, following its tangent.
                    Vector3 towardPath = new Vector3(-side / normalLength, 0f,
                        side * slope / normalLength);
                    torch.transform.localRotation = Quaternion.LookRotation(towardPath, Vector3.up);
                    // Refresh once after all instances are placed, not once per torch.

                }
                torchParticles.Refresh();
            }
        }

        // Générer les arbres sur le terrain
        if (canGenerateTrees)
        {
            Transform treesParent = new GameObject(
                "GeneratedTrees"
            ).transform;

            treesParent.SetParent(
                generatedTerrain.transform,
                false
            );

            int placedTrees = 0;
            int attempts = 0;
            int maxAttempts = treeCount * 20;

            while (placedTrees < treeCount && attempts < maxAttempts)
            {
                attempts++;

                float x = (float)treeRandom.NextDouble()
                    * terrainWidth;

                float z = (float)treeRandom.NextDouble()
                    * terrainLength;

                // Calculer le centre du chemin à cette position
                float normalizedZ = z / terrainLength;

                float pathCenterX = PathCenter(normalizedZ * terrainLength);

                // Ne pas placer d'arbres sur le chemin
                float distanceToPath = Mathf.Abs(x - pathCenterX);

                // Définir les zones boisées avec Perlin Noise
                float treeNoise = Mathf.PerlinNoise(
                    (x / terrainWidth) * treeNoiseScale + offsetX,
                    (z / terrainLength) * treeNoiseScale + offsetZ
                );

                // Préserver des clairières naturelles
                if (treeNoise < treeDensityThreshold)
                {
                    continue;
                }

                if (distanceToPath <
                    pathWidth * 0.5f + treePathClearance)
                {
                    continue;
                }

                // Choisir un prefab aléatoirement
                GameObject prefab = treePrefabs[
                    treeRandom.Next(treePrefabs.Length)
                ];

                if (prefab == null)
                    continue;

                // Positionner l'arbre sur le relief
                float y = terrainData.GetInterpolatedHeight(
                    x / terrainWidth,
                    z / terrainLength
                );

                Vector3 position = new Vector3(x, y, z);

                GameObject tree = Instantiate(
                    prefab,
                    treesParent
                );

                tree.transform.localPosition = position;

                // Rotation et taille aléatoires
                tree.transform.localRotation = Quaternion.Euler(
                    0f,
                    (float)treeRandom.NextDouble() * 360f,
                    0f
                );

                float scale = Mathf.Lerp(
                    0.8f,
                    1.2f,
                    (float)treeRandom.NextDouble()
                );

                tree.transform.localScale =
                    prefab.transform.localScale * scale;

                placedTrees++;
            }

            Debug.Log(
                $"Forêt générée : {placedTrees} arbres placés."
            );
        }

        // Générer la végétation sur le terrain
        if (vegetationPrefabs != null &&
            vegetationPrefabs.Length > 0 &&
            vegetationCount > 0)
        {
            System.Random vegetationRandom =
                new System.Random(currentSeed ^ 24681357);

            Transform vegetationParent = new GameObject(
                "GeneratedVegetation"
            ).transform;

            vegetationParent.SetParent(
                generatedTerrain.transform,
                false
            );

            int placedVegetation = 0;
            int attempts = 0;
            int maxAttempts = vegetationCount * 20;

            while (placedVegetation < vegetationCount &&
                   attempts < maxAttempts)
            {
                attempts++;

                float x = (float)vegetationRandom.NextDouble()
                    * terrainWidth;

                float z = (float)vegetationRandom.NextDouble()
                    * terrainLength;

                float normalizedZ = z / terrainLength;

                float pathCenterX = PathCenter(z);

                float distanceToPath =
                    Mathf.Abs(x - pathCenterX);

                // Créer des zones de végétation naturelle
                float vegetationNoise = Mathf.PerlinNoise(
                    (x / terrainWidth) * vegetationNoiseScale
                        + offsetX,
                    (z / terrainLength) * vegetationNoiseScale
                        + offsetZ
                );

                // Éviter les zones peu végétalisées
                if (vegetationNoise < vegetationDensityThreshold)
                {
                    continue;
                }

                // Conserver le chemin dégagé
                if (distanceToPath <
                    pathWidth * 0.5f + vegetationPathClearance)
                {
                    continue;
                }

                GameObject prefab = vegetationPrefabs[
                    vegetationRandom.Next(vegetationPrefabs.Length)
                ];

                if (prefab == null)
                    continue;

                float y = terrainData.GetInterpolatedHeight(
                    x / terrainWidth,
                    z / terrainLength
                );

                GameObject vegetation = Instantiate(
                    prefab,
                    vegetationParent
                );

                vegetation.transform.localPosition =
                    new Vector3(x, y, z);

                vegetation.transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        (float)vegetationRandom.NextDouble() * 360f,
                        0f
                    );

                float scale = Mathf.Lerp(
                    0.7f,
                    1.3f,
                    (float)vegetationRandom.NextDouble()
                );

                vegetation.transform.localScale =
                    prefab.transform.localScale * scale;

                placedVegetation++;
            }

            Debug.Log(
                $"Végétation générée : {placedVegetation} éléments."
            );
        }

        // 6. Afficher le résultat
        Debug.Log(
            $"Terrain généré : {terrainWidth} x {terrainLength} m " +
            $"| Seed : {currentSeed}"
        );
    }

#if UNITY_EDITOR
    [ContextMenu("Scan Tree Prefabs")]
    public void ScanTreePrefabs()
    {
        string folder = "Assets/Environment/URP/Prefabs/Trees";

        string[] guids = UnityEditor.AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { folder }
        );

        System.Collections.Generic.List<GameObject> trees =
            new System.Collections.Generic.List<GameObject>();

        foreach (string guid in guids)
        {
            string path =
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

            string prefabName =
                System.IO.Path.GetFileNameWithoutExtension(path);

            // Sélectionner uniquement les arbres
            if (prefabName.StartsWith("SM_Tree_") ||
                prefabName.StartsWith("SM_TreeDead_") ||
                prefabName.StartsWith("SM_TreeSmall_") ||
                prefabName.StartsWith("SM_TreeSamll_") ||
                prefabName.StartsWith("SM_BrokenTree_"))
            {
                GameObject prefab =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                        path
                    );

                if (prefab != null)
                    trees.Add(prefab);
            }
        }

        treePrefabs = trees.ToArray();

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log(
            $"Scan terminé : {treePrefabs.Length} arbres trouvés."
        );
    }
#endif

#if UNITY_EDITOR
    [ContextMenu("Scan Vegetation Prefabs")]
    public void ScanVegetationPrefabs()
    {
        string folder =
            "Assets/Environment/URP/Prefabs/GrassAndBushes";

        string[] guids = UnityEditor.AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { folder }
        );

        System.Collections.Generic.List<GameObject> vegetation =
            new System.Collections.Generic.List<GameObject>();

        foreach (string guid in guids)
        {
            string path =
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab =
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    path
                );

            if (prefab != null)
                vegetation.Add(prefab);
        }

        vegetationPrefabs = vegetation.ToArray();

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log(
            $"Scan terminé : {vegetationPrefabs.Length} " +
            "prefabs de végétation trouvés."
        );
    }
#endif

}
