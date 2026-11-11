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
    private MissionSubzoneDefinition subzone;
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
            foreach (var item in world.regions)
                if (item != null && GUILayout.Button(item.displayName + (item == region ? "  ✓" : "")))
                {
                    region = item;
                    subzone = null;
                }
        NewNameField("New region name");
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
            if (GUILayout.Button("Add Region"))
            {
                region = CreateAsset<MissionRegionDefinition>(Root + "/Regions", newName);
                region.displayName = newName.Trim();
                Append(world, "regions", region);
                subzone = null;
                newName = "";
                EditorUtility.SetDirty(region);
                AssetDatabase.SaveAssets();
            }

        if (region != null)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Region: " + region.displayName, EditorStyles.boldLabel);
            if (GUILayout.Button("Edit Region Details")) Selection.activeObject = region;
            if (region.subzones != null)
                foreach (var item in region.subzones)
                    if (item != null && GUILayout.Button(item.displayName + (item == subzone ? "  ✓" : "")))
                        subzone = item;
            NewNameField("New subzone name");
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
                if (GUILayout.Button("Add Subzone"))
                {
                    subzone = CreateAsset<MissionSubzoneDefinition>(Root + "/Subzones", newName);
                    subzone.displayName = newName.Trim();
                    Append(region, "subzones", subzone);
                    newName = "";
                    EditorUtility.SetDirty(subzone);
                    AssetDatabase.SaveAssets();
                }
        }

        if (subzone != null)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Subzone: " + subzone.displayName, EditorStyles.boldLabel);
            if (GUILayout.Button("Edit Subzone Details")) Selection.activeObject = subzone;
            if (subzone.missions != null)
                foreach (var item in subzone.missions)
                    if (item != null && GUILayout.Button(item.displayName))
                        Selection.activeObject = item;
            NewNameField("New mission name");
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
                if (GUILayout.Button("Add Mission"))
                {
                    var mission = CreateAsset<MissionDefinition>(Root + "/Missions", newName);
                    mission.displayName = newName.Trim();
                    Append(subzone, "missions", mission);
                    newName = "";
                    EditorUtility.SetDirty(mission);
                    AssetDatabase.SaveAssets();
                    Selection.activeObject = mission;
                }
        }
        EditorGUILayout.EndScrollView();
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