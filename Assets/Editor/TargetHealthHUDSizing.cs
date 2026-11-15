#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fits the target HUD's complete visual subtree to its own RectTransform.
/// Keeps the validated frame/fill/cap numbers unchanged, scales FX together, and
/// never touches the enemy world-space health-bar prefab.</summary>
public static class TargetHealthHUDSizing
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string PrefabPath = "Assets/Prefabs/UI/TargetHealthBar.prefab";

    [MenuItem("Tools/First Summoner/Combat UI/Fit Target HP To HUD")]
    public static void Fit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Target HP] Exit Play Mode before resizing.");
            return;
        }
        // Edit the independent target prefab asset, not an instance override.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Target HP] Independent TargetHealthBar.prefab not found. Create it first.");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var rootRect = root.GetComponent<RectTransform>();
            var layout = root.GetComponentsInChildren<WorldHealthVisualLayout>(true)
                .FirstOrDefault(l => l.healthFill != null &&
                                     l.healthFill.name == "TargetHealthFill");
            if (rootRect == null || layout == null || layout.frame == null)
            {
                Debug.LogError("[Target HP] Missing target HUD RectTransform/layout/frame. No changes.");
                return;
            }
            var visualRect = layout.GetComponent<RectTransform>();
            if (visualRect == null || visualRect == rootRect)
            {
                Debug.LogError("[Target HP] Missing independent visual container. No changes.");
                return;
            }
            // Use the original HUD slot dimensions. The 85.5 x 9.5 values
            // remain the baseline so all local FX retain their proportions.
            float slotWidth = rootRect.rect.width;
            float slotHeight = rootRect.rect.height;
            float frameWidth = Mathf.Max(1f, layout.frameSize.x);
            float frameHeight = Mathf.Max(1f, layout.frameSize.y);
            if (slotWidth <= 0 || slotHeight <= 0)
            {
                Debug.LogError("[Target HP] Invalid HUD slot dimensions. No changes.");
                return;
            }
            float factor = Mathf.Min(slotWidth / frameWidth, slotHeight / frameHeight);
            if (!float.IsFinite(factor) || factor <= 0f)
            {
                Debug.LogError("[Target HP] Invalid calculated scale. No changes.");
                return;
            }
            // This scales frame, fill, trail and light FX together without
            // altering their individual Inspector sizes or animation settings.
            visualRect.localScale = new Vector3(factor, factor, 1f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Target HP] HUD visuals fitted to " + slotWidth + " x " +
                slotHeight + " with scale " + factor.ToString("F3") +
                ". Frame/fill/FX Inspector sizes unchanged. Enemy prefab untouched.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        // Existing connected instances inherit the prefab scale unless they
        // have a local scale override. Clear only that specific override.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool modified = false;
        foreach (var controller in scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<TargetHealthBarUI>(true)))
        {
            var visual = controller.GetComponentsInChildren<WorldHealthVisualLayout>(true)
                .FirstOrDefault(l => l.healthFill != null &&
                                     l.healthFill.name == "TargetHealthFill");
            if (visual == null) continue;
            var instance = visual.GetComponent<RectTransform>();
            if (instance == null) continue;
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (prefabSource == null) continue;
            // Match the new prefab value and persist it in this scene too,
            // regardless of old instance scale overrides.
            Undo.RecordObject(instance, "Fit target HUD visual scale");
            instance.localScale = prefabSource.localScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            modified = true;
        }
        if (modified)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
