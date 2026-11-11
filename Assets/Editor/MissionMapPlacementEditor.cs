#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Visual placement of mission markers on a region's map artwork.</summary>
public sealed class MissionMapPlacementEditor : EditorWindow
{
    private MissionWorldDefinition world;
    private MissionRegionDefinition region;
    private Vector2 scroll;
    private const float MaxMapWidth = 650f;

    [MenuItem("Tools/First Summoner/Mission Map Placement")]
    public static void Open() => GetWindow<MissionMapPlacementEditor>("Mission Placement");

    private void OnEnable()
    {
        world = AssetDatabase.LoadAssetAtPath<MissionWorldDefinition>(
            "Assets/GameData/Missions/MissionWorld.asset");
    }

    private void OnGUI()
    {
        world = (MissionWorldDefinition)EditorGUILayout.ObjectField(
            "World", world, typeof(MissionWorldDefinition), false);
        if (world == null || world.regions == null)
        {
            EditorGUILayout.HelpBox("Select a mission world with regions.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Choose region", EditorStyles.boldLabel);
        foreach (var candidate in world.regions)
        {
            if (candidate == null) continue;
            if (GUILayout.Toggle(region == candidate, candidate.displayName, "Button"))
                region = candidate;
        }
        if (region == null) return;
        var sprite = region.mapIllustration;
        Texture2D texture = sprite != null ? sprite.texture : null;
        Rect spriteRect = sprite != null ? sprite.rect : new Rect(0f, 0f, 16f, 9f);
        float aspect = spriteRect.height / spriteRect.width;
        float width = Mathf.Min(MaxMapWidth, Mathf.Max(100f, position.width - 35f));
        float height = width * aspect;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Rect area = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width));
        if (texture != null)
        {
            Rect uv = new Rect(spriteRect.x / texture.width, spriteRect.y / texture.height,
                spriteRect.width / texture.width, spriteRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(area, texture, uv, true);
        }
        else
        {
            EditorGUI.DrawRect(area, new Color(0.14f, 0.19f, 0.23f));
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.15f);
            for (int i = 1; i < 10; i++)
            {
                float x = area.x + area.width * i / 10f;
                Handles.DrawLine(new Vector3(x, area.y), new Vector3(x, area.yMax));
            }
            for (int i = 1; i < 6; i++)
            {
                float y = area.y + area.height * i / 6f;
                Handles.DrawLine(new Vector3(area.x, y), new Vector3(area.xMax, y));
            }
            Handles.EndGUI();
            GUI.Label(new Rect(area.x + 8f, area.y + 8f, area.width - 16f, 22f),
                "Temporary map - " + region.displayName);
        }

        if (region.missions != null)
        {
            for (int i = 0; i < region.missions.Length; i++)
            {
                var mission = region.missions[i];
                if (mission == null) continue;
                Vector2 pos = mission.mapPosition;
                float x = area.x + Mathf.Clamp01(pos.x) * area.width;
                float y = area.y + (1f - Mathf.Clamp01(pos.y)) * area.height;
                Rect marker = new Rect(x - 23f, y - 14f, 46f, 28f);
                GUI.Box(marker, (i + 1).ToString(), EditorStyles.miniButton);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                    marker.Contains(Event.current.mousePosition))
                {
                    dragging = mission;
                    Event.current.Use();
                }
            }
        }

        Event e = Event.current;
        if (dragging != null && e.type == EventType.MouseDrag && e.button == 0)
        {
            Undo.RecordObject(dragging, "Move mission map marker");
            dragging.mapPosition = new Vector2(
                Mathf.Clamp01((e.mousePosition.x - area.x) / area.width),
                Mathf.Clamp01(1f - (e.mousePosition.y - area.y) / area.height));
            EditorUtility.SetDirty(dragging);
            e.Use();
            Repaint();
        }
        if (e.type == EventType.MouseUp && e.button == 0 && dragging != null)
        {
            dragging = null;
            AssetDatabase.SaveAssets();
            e.Use();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("Drag the numbered markers to position missions. Positions are saved on the mission assets.", MessageType.Info);
    }

    private MissionDefinition dragging;
}
#endif