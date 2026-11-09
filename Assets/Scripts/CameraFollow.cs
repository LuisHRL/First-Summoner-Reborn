using UnityEngine;

/// <summary>
/// Smooth portrait camera: vertical screen dead zone, gentle path centering,
/// and viewport-aware bounds on the generated terrain.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 11f, -14f);

    [Header("Vertical screen dead zone")]
    [Range(0.05f, 0.95f)] public float lowerScreenThreshold = 0.30f;
    [Range(0.05f, 0.95f)] public float upperScreenThreshold = 0.70f;
    [Min(0.01f)] public float verticalSmoothTime = 0.8f;

    [Header("Gentle horizontal path framing")]
    public ProceduralTerrainGenerator terrainGenerator;
    [Min(0f)] public float pathLookAhead = 6f;
    [Min(0f)] public float horizontalTolerance = 0.7f;
    [Min(0.01f)] public float horizontalSmoothTime = 2.5f;
    [Min(0.01f)] public float maximumCameraSpeed = 12f;

    [Header("Terrain viewport limits")]
    public bool keepTerrainEndsInView = true;

    private Camera cameraComponent;
    private float verticalVelocity;
    private float horizontalVelocity;
    private Terrain terrain;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        ResolveTerrain();

        Vector3 current = transform.position;
        Vector3 desired = current;
        float low = Mathf.Min(lowerScreenThreshold, upperScreenThreshold);
        float high = Mathf.Max(lowerScreenThreshold, upperScreenThreshold);

        // The player's viewport position triggers movement only outside the dead zone.
        Vector3 screen = cameraComponent.WorldToViewportPoint(target.position);
        if (screen.z > 0f && (screen.y < low || screen.y > high))
        {
            float boundary = Mathf.Clamp(screen.y, low, high);
            // Measure screen movement per world-Z unit, respecting perspective and camera tilt.
            Vector3 next = cameraComponent.WorldToViewportPoint(target.position + Vector3.forward);
            float sensitivity = next.y - screen.y;
            if (Mathf.Abs(sensitivity) > 0.0001f)
                desired.z += (screen.y - boundary) / sensitivity;
        }

        // Follow the path center, not the player's lateral dodges.
        if (terrainGenerator != null && terrain != null)
        {
            Vector3 terrainOrigin = terrain.transform.position;
            float length = terrain.terrainData.size.z;
            float sampleZ = Mathf.Clamp(target.position.z + pathLookAhead,
                terrainOrigin.z, terrainOrigin.z + length);
            float normalized = Mathf.Clamp01((sampleZ - terrainOrigin.z) / Mathf.Max(length, 0.001f));
            float pathCenterX = terrainOrigin.x + terrainGenerator.terrainWidth * 0.5f
                + Mathf.Sin(normalized * Mathf.PI * 2f * terrainGenerator.pathFrequency)
                * terrainGenerator.pathCurvature;
            float delta = pathCenterX - (current.x - offset.x);
            if (Mathf.Abs(delta) > horizontalTolerance)
                desired.x = pathCenterX + offset.x - Mathf.Sign(delta) * horizontalTolerance;
        }
        else
        {
            desired.x = target.position.x + offset.x;
        }

        float dt = Time.deltaTime;
        Vector3 result = current;
        result.x = Mathf.SmoothDamp(current.x, desired.x, ref horizontalVelocity,
            horizontalSmoothTime, maximumCameraSpeed, dt);
        result.z = Mathf.SmoothDamp(current.z, desired.z, ref verticalVelocity,
            verticalSmoothTime, maximumCameraSpeed, dt);
        result.y = target.position.y + offset.y;

        if (keepTerrainEndsInView && terrain != null)
            ClampToTerrainEnds(ref result);

        transform.position = result;
    }

    private void ResolveTerrain()
    {
        if (terrainGenerator == null)
            terrainGenerator = FindFirstObjectByType<ProceduralTerrainGenerator>();

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

        float minZ = terrain.transform.position.z;
        float maxZ = minZ + data.size.z;

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
