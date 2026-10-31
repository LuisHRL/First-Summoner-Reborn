using System;
using UnityEditor;
using UnityEngine;

// Complementary configuration tool. Does not modify existing migration scripts or assets.
public sealed class AssetCleanerFolderConfiguration : EditorWindow
{
    public const string SourceKey = "AssetCleaner.Universal.SourceFolder";
    public const string TargetKey = "AssetCleaner.Universal.TargetFolder";

    private DefaultAsset sourceFolder;
    private DefaultAsset targetFolder;

    [MenuItem("Tools/Asset Cleaner/Configure Folders")]
    public static void Open()
    {
        var window = GetWindow<AssetCleanerFolderConfiguration>("Asset Cleaner Folders");
        window.minSize = new Vector2(440, 170);
        window.LoadSavedFolders();
        window.Show();
    }

    private void OnEnable() { LoadSavedFolders(); }

    private void LoadSavedFolders()
    {
        sourceFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(EditorPrefs.GetString(SourceKey, ""));
        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(EditorPrefs.GetString(TargetKey, ""));
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Reusable folder migration settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Choose two existing folders inside Assets. This only saves folder paths in Editor preferences; it does not change any assets or the existing SkeletonArcher tools.", MessageType.Info);
        sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Original folder", sourceFolder, typeof(DefaultAsset), false);
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Duplicated folder", targetFolder, typeof(DefaultAsset), false);

        string sourcePath = sourceFolder ? AssetDatabase.GetAssetPath(sourceFolder) : "";
        string targetPath = targetFolder ? AssetDatabase.GetAssetPath(targetFolder) : "";
        bool valid = IsAssetFolder(sourcePath) && IsAssetFolder(targetPath)
            && sourcePath != targetPath
            && !targetPath.StartsWith(sourcePath + "/", StringComparison.Ordinal)
            && !sourcePath.StartsWith(targetPath + "/", StringComparison.Ordinal);

        using (new EditorGUI.DisabledScope(!valid))
        {
            if (GUILayout.Button("Save folder configuration"))
            {
                EditorPrefs.SetString(SourceKey, sourcePath);
                EditorPrefs.SetString(TargetKey, targetPath);
                Debug.Log("[Asset Cleaner] Folder configuration saved. Source: " + sourcePath + " | Target: " + targetPath + ". No assets modified.");
            }
        }
        if (!valid) EditorGUILayout.HelpBox("Select two different, non-nested folders inside Assets.", MessageType.Warning);
    }

    private static bool IsAssetFolder(string path)
    {
        return path.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(path);
    }
}
