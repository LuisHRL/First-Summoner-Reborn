using System;
using UnityEngine;

[CreateAssetMenu(menuName = "First Summoner/Missions/Mission", fileName = "Mission")]
public sealed class MissionDefinition : ScriptableObject
{
    [SerializeField] private string persistentId;
    public string PersistentId => persistentId;
    public string displayName;
    [TextArea] public string description;
    public Sprite illustration;
    [Tooltip("Position on the region map, normalized from 0 to 1.")]
    public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
    [Header("Time-based stars (seconds; 0 disables timed rating)")]
    [Min(0f)] public float threeStarMaxSeconds;
    [Min(0f)] public float twoStarMaxSeconds;
    public int recommendedPower = 1;
    [Min(0f)] public float startingSafeZoneMeters = 15f;
    public EnemySpawnEntry[] enemies = Array.Empty<EnemySpawnEntry>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            persistentId = Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}

[Serializable]
public sealed class EnemySpawnEntry
{
    public GameObject prefab;
    [Min(1)] public int weight = 1;
    [Min(0)] public int minimumCount;
    [Min(0)] public int maximumCount = 3;
}
