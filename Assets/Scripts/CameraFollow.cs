using UnityEngine;

/// <summary>
/// Smooth portrait camera: vertical screen dead zone, gentle path centering,
/// and viewport-aware bounds on the generated terrain.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 8f, -9f);

    [Header("Field of view")]
    [Tooltip("A wider perspective reveals more trees on both sides; it also changes vertical framing.")]
    public bool overrideFieldOfView = true;
    [Range(35f, 90f)] public float verticalFieldOfView = 66f;

    [Header("View angle")]
    [Range(25f, 75f)] public float downwardAngle = 40f;

    [Header("Caméra de combat")]
    public bool tiltOnEnemyAggro = true;
    [Range(0f, 20f)] public float combatAdditionalDownwardAngle = 7f;
    [Min(0.05f)] public float combatTiltSmoothTime = 0.55f;
    private float currentCameraAngle;
    private float cameraAngleVelocity;

    [Header("Vertical screen dead zone")]
    [Range(0.05f, 0.95f)] public float lowerScreenThreshold = 0.42f;
    [Range(0.05f, 0.95f)] public float upperScreenThreshold = 0.70f;
    [Min(0.01f)] public float verticalSmoothTime = 0.30f;
    [Min(0.01f)] public float backwardSmoothTime = 0.18f;
    [Range(0.05f, 0.95f)] public float backwardReturnScreenPosition = 0.58f;

    [Header("Distance de suivi")]
    [Min(0f)] public float maximumForwardSeparation = 1.5f;
    [Min(0f)] public float maximumBackwardSeparation = 1.5f;

    [Header("Gentle horizontal path framing")]
    public ProceduralTerrainGenerator terrainGenerator;
    [Min(0f)] public float pathLookAhead = 6f;
    [Min(0f)] public float horizontalTolerance = 0.35f;
    [Range(0f, 1f)] public float playerHorizontalInfluence = 0.65f;
    [Min(0.01f)] public float horizontalSmoothTime = 0.45f;
    [Min(0.01f)] public float maximumCameraSpeed = 25f;

    [Header("Terrain viewport limits")]
    public bool keepTerrainEndsInView = true;
    [Min(0f)] public float terrainEdgePadding = 2f;

    [Header("Position initiale du héros")]
    public bool correctHeroSpawnNearTerrainEdge = true;
    [Min(0f)] public float heroStartDistanceFromEdge = 8f;
    [Min(0f)] public float heroSpawnGroundOffset = 0.1f;
    private bool heroSpawnChecked;

    [Header("Introduction de mission")]
    public bool playMissionIntro = true;
    [Min(0f)] public float introHoldSeconds = 0.8f;
    [Min(0.1f)] public float introTransitionSeconds = 2f;
    [Min(1f)] public float introCameraDistance = 3.5f;
    [Min(0f)] public float introLookHeight = 1.2f;
    [Range(20f, 90f)] public float introFieldOfView = 48f;

    private bool introStarted;
    private bool introFinished;
    private float introStartTime;
    private Vector3 introPosition;
    private Quaternion introRotation;
    private Vector3 gameplayStartPosition;
    private Quaternion gameplayStartRotation;

    [Header("Obstacle visibility")]
    public bool hideObstructingTrees = true;
    [Min(0.1f)] public float obstacleCheckRadius = 0.35f;
    private readonly System.Collections.Generic.HashSet<Renderer> hiddenRenderers = new System.Collections.Generic.HashSet<Renderer>();
    private readonly System.Collections.Generic.HashSet<Renderer> currentlyObstructing = new System.Collections.Generic.HashSet<Renderer>();

    private Camera cameraComponent;
    private float verticalVelocity;
    private float horizontalVelocity;
    private Terrain terrain;
    private bool framingInitialized;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        ApplyFieldOfView();
        currentCameraAngle = downwardAngle;
        transform.rotation = Quaternion.Euler(downwardAngle, transform.eulerAngles.y, 0f);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        ResolveTerrain();
        CorrectHeroSpawnIfNeeded();
        if (playMissionIntro && !introFinished)
        {
            UpdateMissionIntro();
            return;
        }

        ApplyFieldOfView();
        bool inCombat = false;
        if (tiltOnEnemyAggro)
        {
            EnemyAggroGroup[] groups = FindObjectsByType<EnemyAggroGroup>();
            foreach (EnemyAggroGroup group in groups)
            {
                if (group != null && group.isActiveAndEnabled && group.IsEngaged)
                {
                    inCombat = true;
                    break;
                }
            }
        }
        float targetAngle = downwardAngle + (inCombat ? combatAdditionalDownwardAngle : 0f);
        currentCameraAngle = Mathf.SmoothDampAngle(currentCameraAngle, targetAngle,
            ref cameraAngleVelocity, combatTiltSmoothTime, Mathf.Infinity, Time.deltaTime);
        transform.rotation = Quaternion.Euler(currentCameraAngle, transform.eulerAngles.y, 0f);

        // The scene camera may start at an arbitrary editor position.
        // Establish the intended distance before applying the screen dead zone.
        if (!framingInitialized)
        {
            Vector3 initial = target.position + offset;
            if (keepTerrainEndsInView && terrain != null)
                ClampToTerrainEnds(ref initial);
            transform.position = initial;
            framingInitialized = true;
        }

        Vector3 current = transform.position;
        Vector3 desired = current;
        float low = Mathf.Min(lowerScreenThreshold, upperScreenThreshold);
        float high = Mathf.Max(lowerScreenThreshold, upperScreenThreshold);

        // The player's viewport position triggers movement only outside the dead zone.
        Vector3 screen = cameraComponent.WorldToViewportPoint(target.position);
        if (screen.z > 0f && (screen.y < low || screen.y > high))
        {
            // When returning toward the bottom, recover farther into the safe zone.
            float boundary = screen.y < low
                ? Mathf.Clamp(backwardReturnScreenPosition, low, high)
                : high;
            // Measure screen movement per world-Z unit, respecting perspective and camera tilt.
            Vector3 next = cameraComponent.WorldToViewportPoint(target.position + Vector3.forward);
            float sensitivity = next.y - screen.y;
            if (Mathf.Abs(sensitivity) > 0.0001f)
                desired.z += (screen.y - boundary) / sensitivity;
        }

        // Blend path centering with player movement, avoiding excessive lateral camera drift.
        if (terrainGenerator != null && terrain != null)
        {
            Vector3 terrainOrigin = terrain.transform.position;
            float length = terrain.terrainData.size.z;
            float sampleZ = Mathf.Clamp(target.position.z + pathLookAhead,
                terrainOrigin.z, terrainOrigin.z + length);
            // Use the same path definition as terrain generation.
            // Do not anticipate a curve while the hero is still in the safe zone.
            float localHeroZ = target.position.z - terrainOrigin.z;
            if (localHeroZ <= terrainGenerator.safeZoneLength)
                sampleZ = Mathf.Min(sampleZ, terrainOrigin.z + terrainGenerator.safeZoneLength);
            float pathCenterX = terrainOrigin.x +
                terrainGenerator.PathCenter(sampleZ - terrainOrigin.z);
            float desiredCenterX = Mathf.Lerp(pathCenterX, target.position.x, playerHorizontalInfluence);
            float delta = desiredCenterX - (current.x - offset.x);
            if (Mathf.Abs(delta) > horizontalTolerance)
                desired.x = desiredCenterX + offset.x - Mathf.Sign(delta) * horizontalTolerance;
        }
        else
        {
            desired.x = target.position.x + offset.x;
        }

        float dt = Time.deltaTime;
        Vector3 result = current;
        result.x = Mathf.SmoothDamp(current.x, desired.x, ref horizontalVelocity,
            horizontalSmoothTime, maximumCameraSpeed, dt);
        float zSmoothTime = desired.z < current.z ? backwardSmoothTime : verticalSmoothTime;
        result.z = Mathf.SmoothDamp(current.z, desired.z, ref verticalVelocity,
            zSmoothTime, maximumCameraSpeed, dt);
        result.y = target.position.y + offset.y;

        float idealZ = target.position.z + offset.z;
        float boundedZ = Mathf.Clamp(result.z,
            idealZ - maximumBackwardSeparation,
            idealZ + maximumForwardSeparation);
        if (!Mathf.Approximately(boundedZ, result.z))
        {
            result.z = boundedZ;
            verticalVelocity = 0f;
        }

        if (keepTerrainEndsInView && terrain != null)
            ClampToTerrainEnds(ref result);

        transform.position = result;
        UpdateObstructions();
    }

    private void CorrectHeroSpawnIfNeeded()
    {
        if (heroSpawnChecked || terrain == null || terrain.terrainData == null)
            return;

        heroSpawnChecked = true;
        if (!correctHeroSpawnNearTerrainEdge)
            return;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float localZ = target.position.z - origin.z;
        float desiredZ = Mathf.Clamp(heroStartDistanceFromEdge, 0f, size.z * 0.5f);
        if (localZ >= desiredZ && localZ <= size.z)
            return;

        // Only move a hero at the beginning of the terrain; do not teleport
        // an already progressing player or one near the far end.
        if (localZ > desiredZ)
            return;

        Vector3 position = target.position;
        position.z = origin.z + desiredZ;
        float pathX = terrainGenerator != null
            ? terrainGenerator.PathCenter(desiredZ)
            : size.x * 0.5f;
        position.x = origin.x + Mathf.Clamp(pathX, 0f, size.x);
        position.y = terrain.SampleHeight(position) + origin.y + heroSpawnGroundOffset;

        CharacterController characterController = target.GetComponent<CharacterController>();
        bool wasEnabled = characterController != null && characterController.enabled;
        if (wasEnabled) characterController.enabled = false;
        target.position = position;
        if (wasEnabled) characterController.enabled = true;
    }

    private void UpdateMissionIntro()
    {
        if (!introStarted)
        {
            introStarted = true;
            introStartTime = Time.time;
            gameplayStartRotation = Quaternion.Euler(downwardAngle, transform.eulerAngles.y, 0f);
            gameplayStartPosition = target.position + offset;
            transform.rotation = gameplayStartRotation;
            if (keepTerrainEndsInView && terrain != null)
                ClampToTerrainEnds(ref gameplayStartPosition);

            Vector3 focus = target.position + Vector3.up * introLookHeight;
            // Start in front of the hero, on the same side as the gameplay camera.
            Vector3 backward = gameplayStartRotation * Vector3.back;
            introPosition = focus + backward * introCameraDistance;
            introRotation = Quaternion.LookRotation(focus - introPosition, Vector3.up);
        }

        float elapsed = Time.time - introStartTime;
        float blend = Mathf.Clamp01((elapsed - introHoldSeconds) /
            Mathf.Max(0.1f, introTransitionSeconds));
        blend = blend * blend * (3f - 2f * blend);
        Vector3 endPosition = target.position + offset;
        // Frame the hero near the middle of the gameplay dead zone before handing
        // control back to the regular camera follow logic.
        transform.rotation = gameplayStartRotation;
        transform.position = endPosition;
        float desiredViewportY = Mathf.Clamp(
            (lowerScreenThreshold + upperScreenThreshold) * 0.5f, 0.05f, 0.95f);
        Vector3 heroViewport = cameraComponent.WorldToViewportPoint(target.position);
        Vector3 forwardViewport = cameraComponent.WorldToViewportPoint(
            target.position + Vector3.forward);
        float sensitivity = forwardViewport.y - heroViewport.y;
        if (heroViewport.z > 0f && Mathf.Abs(sensitivity) > 0.0001f)
            endPosition.z += (heroViewport.y - desiredViewportY) / sensitivity;
        if (keepTerrainEndsInView && terrain != null)
            ClampToTerrainEnds(ref endPosition);

        transform.position = Vector3.Lerp(introPosition, endPosition, blend);
        transform.rotation = Quaternion.Slerp(introRotation, gameplayStartRotation, blend);
        if (cameraComponent != null && !cameraComponent.orthographic)
        {
            float normalFov = overrideFieldOfView ? verticalFieldOfView : cameraComponent.fieldOfView;
            cameraComponent.fieldOfView = Mathf.Lerp(introFieldOfView, normalFov, blend);
        }

        if (elapsed >= introHoldSeconds + introTransitionSeconds)
        {
            introFinished = true;
            currentCameraAngle = downwardAngle;
            cameraAngleVelocity = 0f;
            framingInitialized = true;
            verticalVelocity = 0f;
            horizontalVelocity = 0f;
            ApplyFieldOfView();
        }
        UpdateObstructions();
    }

    private void ApplyFieldOfView()
    {
        if (cameraComponent != null && overrideFieldOfView && cameraComponent.orthographic)
            return;
        if (cameraComponent != null && overrideFieldOfView)
            cameraComponent.fieldOfView = verticalFieldOfView;
    }

    private void OnDisable()
    {
        RestoreHiddenRenderers();
    }

    private void UpdateObstructions()
    {
        if (!hideObstructingTrees || target == null)
        {
            RestoreHiddenRenderers();
            return;
        }

        currentlyObstructing.Clear();
        Vector3 start = transform.position;
        Vector3 end = target.position + Vector3.up * 1.0f;
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance > 0.01f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(start, obstacleCheckRadius,
                direction / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(target))
                    continue;
                if (terrain != null && hit.collider.GetComponent<Terrain>() == terrain)
                    continue;
                Renderer renderer = hit.collider.GetComponent<Renderer>();
                if (renderer == null)
                    renderer = hit.collider.GetComponentInParent<Renderer>();
                if (renderer != null && IsVegetation(hit.collider.transform))
                    currentlyObstructing.Add(renderer);
            }
        }

        foreach (Renderer renderer in hiddenRenderers)
            if (renderer != null && !currentlyObstructing.Contains(renderer))
                renderer.enabled = true;

        hiddenRenderers.RemoveWhere(r => r == null || !currentlyObstructing.Contains(r));
        foreach (Renderer renderer in currentlyObstructing)
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }
    }

    private static bool IsVegetation(Transform t)
    {
        while (t != null)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("tree") || n.Contains("bush") || n.Contains("foliage"))
                return true;
            t = t.parent;
        }
        return false;
    }

    private void RestoreHiddenRenderers()
    {
        foreach (Renderer renderer in hiddenRenderers)
            if (renderer != null)
                renderer.enabled = true;
        hiddenRenderers.Clear();
        currentlyObstructing.Clear();
    }

    private void ResolveTerrain()
    {
        if (terrainGenerator == null)
            terrainGenerator = FindAnyObjectByType<ProceduralTerrainGenerator>();

        if (terrainGenerator != null)
        {
            Transform generated = terrainGenerator.transform.Find("GeneratedTerrain");
            terrain = generated != null ? generated.GetComponent<Terrain>() : null;
        }
        else
        {
            terrain = Terrain.activeTerrain;
        }
    }

    private void ClampToTerrainEnds(ref Vector3 position)
    {
        TerrainData data = terrain.terrainData;
        if (data == null)
            return;

        float padding = Mathf.Min(terrainEdgePadding, data.size.z * 0.25f);
        float minZ = terrain.transform.position.z + padding;
        float maxZ = terrain.transform.position.z + data.size.z - padding;

        // Intersect the top and bottom viewport rays with the terrain's base plane.
        // Translating the camera in Z shifts both intersections by the same amount.
        Vector3 oldPosition = transform.position;
        transform.position = position;
        bool bottomValid = ViewportGroundZ(0f, terrain.transform.position.y, out float bottomZ);
        bool topValid = ViewportGroundZ(1f, terrain.transform.position.y, out float topZ);
        transform.position = oldPosition;

        if (!bottomValid || !topValid)
            return;

        float bottomCorrection = minZ - bottomZ;
        float topCorrection = maxZ - topZ;
        if (bottomCorrection > topCorrection)
        {
            // View is taller than the terrain: prioritize the bottom boundary.
            position.z += bottomCorrection;
            verticalVelocity = 0f;
            return;
        }

        float correction = Mathf.Clamp(0f, bottomCorrection, topCorrection);
        if (!Mathf.Approximately(correction, 0f))
        {
            position.z += correction;
            verticalVelocity = 0f;
        }
    }

    private bool ViewportGroundZ(float viewportY, float planeY, out float z)
    {
        Ray ray = cameraComponent.ViewportPointToRay(new Vector3(0.5f, viewportY, 0f));
        Plane ground = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (ground.Raycast(ray, out float distance))
        {
            z = ray.GetPoint(distance).z;
            return true;
        }
        z = 0f;
        return false;
    }
}
