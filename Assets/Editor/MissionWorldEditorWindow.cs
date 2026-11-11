#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates mission hierarchy assets without requiring manual array wiring.
/// Existing definitions are never overwritten or deleted.
/// </summary>
public sealed class MissionWorldEditorWindow : EditorWindow
{
    private const string Root = "Assets/GameData/Missions";
    private MissionWorldDefinition world;
    private MissionRegionDefinition region;
    private string newName = "";
    private Vector2 scroll;

    [MenuItem("Tools/First Summoner/Mission World Editor")]
    private static void Open()
    {
        GetWindow<MissionWorldEditorWindow>("Mission World");
    }

    private void OnEnable()
    {
        world = AssetDatabase.LoadAssetAtPath<MissionWorldDefinition>(Root + "/MissionWorld.asset");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mission World Configuration", EditorStyles.boldLabel);
        world = (MissionWorldDefinition)EditorGUILayout.ObjectField("World", world, typeof(MissionWorldDefinition), false);
        if (world == null)
        {
            if (GUILayout.Button("Create Mission World"))
            {
                EnsureFolder("Assets", "GameData");
                EnsureFolder("Assets/GameData", "Missions");
                world = CreateAsset<MissionWorldDefinition>(Root, "MissionWorld");
            }
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Regions", EditorStyles.boldLabel);
        if (world.regions != null)
            for (int i = 0; i < world.regions.Length; i++)
            {
                var item = world.regions[i];
                if (item == null) continue;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(item.displayName + (item == region ? "  ✓" : ""), GUILayout.ExpandWidth(true)))
                    region = item;
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(32)))
                    {
                        MoveRegion(i, i - 1);
                        GUIUtility.ExitGUI();
                    }
                using (new EditorGUI.DisabledScope(i == world.regions.Length - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(32)))
                    {
                        MoveRegion(i, i + 1);
                        GUIUtility.ExitGUI();
                    }
                EditorGUILayout.EndHorizontal();
            }
        NewNameField("New region name");
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
            if (GUILayout.Button("Add Region"))
            {
                region = CreateAsset<MissionRegionDefinition>(Root + "/Regions", newName);
                region.displayName = newName.Trim();
                Append(world, "regions", region);
                newName = "";
                EditorUtility.SetDirty(region);
                AssetDatabase.SaveAssets();
            }

        if (region != null)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Region: " + region.displayName, EditorStyles.boldLabel);
            if (GUILayout.Button("Edit Region Details")) Selection.activeObject = region;
            if (region.missions != null)
                foreach (var item in region.missions)
                    if (item != null && GUILayout.Button(item.displayName))
                        Selection.activeObject = item;
            NewNameField("New mission name");
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
                if (GUILayout.Button("Add Mission"))
                {
                    var mission = CreateAsset<MissionDefinition>(Root + "/Missions", newName);
                    mission.displayName = newName.Trim();
                    Append(region, "missions", mission);
                    newName = "";
                    EditorUtility.SetDirty(mission);
                    AssetDatabase.SaveAssets();
                    Selection.activeObject = mission;
                }
        }
        EditorGUILayout.EndScrollView();
    }

    private void MoveRegion(int from, int to)
    {
        if (world == null || world.regions == null || from < 0 || to < 0 ||
            from >= world.regions.Length || to >= world.regions.Length) return;
        Undo.RecordObject(world, "Reorder mission regions");
        var temp = world.regions[from];
        world.regions[from] = world.regions[to];
        world.regions[to] = temp;
        EditorUtility.SetDirty(world);
        AssetDatabase.SaveAssets();
    }

    private void NewNameField(string label) => newName = EditorGUILayout.TextField(label, newName);

    private static void Append(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        Undo.RecordObject(target, "Add mission hierarchy entry");
        var so = new SerializedObject(target);
        var property = so.FindProperty(propertyName);
        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).objectReferenceValue = value;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }

    private static T CreateAsset<T>(string folder, string name) where T : ScriptableObject
    {
        EnsureNestedFolder(folder);
        var asset = CreateInstance<T>();
        string safeName = string.IsNullOrWhiteSpace(name) ? typeof(T).Name : name.Trim();
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + ".asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void EnsureNestedFolder(string path)
    {
        string[] parts = path.Split('/');
        string parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            EnsureFolder(parent, parts[i]);
            parent += "/" + parts[i];
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif