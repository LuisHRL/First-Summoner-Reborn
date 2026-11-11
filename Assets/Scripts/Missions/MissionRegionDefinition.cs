using System;
using UnityEngine;

[CreateAssetMenu(menuName = "First Summoner/Missions/Region", fileName = "Region")]
public sealed class MissionRegionDefinition : ScriptableObject
{
    [SerializeField] private string persistentId;
    public string PersistentId => persistentId;
    public string displayName;
    public Sprite mapIllustration;
    [Tooltip("Normalized position on the world map (0 to 1).")]
    public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
    public MissionSubzoneDefinition[] subzones = Array.Empty<MissionSubzoneDefinition>();

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
