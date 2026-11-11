using System;
using UnityEngine;

[CreateAssetMenu(menuName = "First Summoner/Missions/Subzone", fileName = "Subzone")]
public sealed class MissionSubzoneDefinition : ScriptableObject
{
    [SerializeField] private string persistentId;
    public string PersistentId => persistentId;
    public string displayName;
    public Sprite illustration;
    [Tooltip("Normalized position within the region map (0 to 1).")]
    public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
    public MissionDefinition[] missions = Array.Empty<MissionDefinition>();

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
