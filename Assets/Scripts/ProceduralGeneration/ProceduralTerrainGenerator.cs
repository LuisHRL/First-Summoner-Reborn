
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
    public float pathWidth = 8f;

    [Header("Transition du chemin")]
    [Range(0.1f, 5f)]
    public float pathBlendWidth = 2f;


    [Header("Courbes du chemin")]
    [Range(0f, 15f)]
    public float pathCurvature = 6f;

    [Range(0.5f, 5f)]
    public float pathFrequency = 1.5f;


    [Header("Relief")]
    [Min(0f)]
    public float terrainHeight = 10f;


    [Header("Zones rocheuses")]
    [Range(0f, 1f)]
    public float rockThreshold = 0.65f;

    [Range(1f, 20f)]
    public float rockNoiseScale = 7f;


    [Header("Génération")]
    public int seed = 12345;
    public bool randomizeSeed = true;

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
                float pathCenterX =
                    terrainWidth * 0.5f
                    + Mathf.Sin(
                        normalizedZ * Mathf.PI * 2f * pathFrequency
                    ) * pathCurvature;

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

                // Relief progressif en dehors du chemin
                heights[z, x] = noise * 0.35f * pathInfluence;
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
        if (terrainLayers != null && terrainLayers.Length > 0)
        {
            terrainData.terrainLayers = terrainLayers;

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
                    float pathCenterX =
                        terrainWidth * 0.5f
                        + Mathf.Sin(
                            normalizedZ * Mathf.PI * 2f * pathFrequency
                        ) * pathCurvature;

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

                    // Préserver le chemin
                    rockWeight *= 1f - pathWeight;

                    // Texture 0 : sol forestier
                    splatmap[z, x, 0] =
                        (1f - pathWeight) - rockWeight;

                    // Texture 1 : chemin
                    splatmap[z, x, 1] = pathWeight;

                    // Texture 2 : zones rocheuses
                    if (layerCount >= 3)
                    {
                        splatmap[z, x, 2] = rockWeight;
                    }
                    else
                    {
                        splatmap[z, x, 0] += rockWeight;
                    }



                }
            }

            terrainData.SetAlphamaps(0, 0, splatmap);

        }


        // 5. Créer le terrain Unity
        generatedTerrain =
            Terrain.CreateTerrainGameObject(terrainData);

        generatedTerrain.name = "GeneratedTerrain";

        generatedTerrain.transform.SetParent(
            transform,
            false
        );

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

                float pathCenterX =
                    terrainWidth * 0.5f
                    + Mathf.Sin(
                        normalizedZ * Mathf.PI * 2f * pathFrequency
                    ) * pathCurvature;

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

                float pathCenterX =
                    terrainWidth * 0.5f +
                    Mathf.Sin(
                        normalizedZ * Mathf.PI * 2f * pathFrequency
                    ) * pathCurvature;

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
