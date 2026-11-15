#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Converts the existing HUD target bar into its own prefab without recreating
/// its tuned RectTransforms, animation objects or visual settings.
/// The enemy world-bar prefab is never read or modified.
/// </summary>
public static class TargetHealthPrefabMigration
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string PrefabPath = "Assets/Prefabs/UI/TargetHealthBar.prefab";

    [MenuItem("Tools/First Summoner/Combat UI/Create Independent Target HP Prefab")]
    public static void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Target HP] Exit Play Mode before migration.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var controllers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TargetHealthBarUI>(true))
            .ToArray();
        if (controllers.Length != 1)
        {
            Debug.LogError("[Target HP] Expected one TargetHealthBarUI in GameScene, found " + controllers.Length);
            return;
        }
        var controller = controllers[0];
        var hudRoot = controller.transform;
        var fill = controller.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(image => image.name == "TargetHealthFill");
        if (fill == null || !fill.transform.IsChildOf(hudRoot))
        {
            Debug.LogError("[Target HP] Local TargetHealthFill missing; scene unchanged.");
            return;
        }
        var background = fill.transform.parent;
        var layout = background.GetComponent<WorldHealthVisualLayout>();
        var frame = background.Find("AdjustableHPFrame")?.GetComponent<AdjustableThreeSlice>();
        if (layout == null || frame == null)
        {
            Debug.LogError("[Target HP] HUD visual layout/frame missing; scene unchanged.");
            return;
        }
        var animation = controller.healthAnimation;
        if (animation != null && !animation.transform.IsChildOf(hudRoot))
        {
            Debug.LogError("[Target HP] Animation is external to HUD; refusing migration.");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogWarning("[Target HP] Target prefab already exists; no overwrite. Scene unchanged.");
            return;
        }

        // Fix references only within the HUD. Do not call layout.Apply(), since
        // doing so could overwrite carefully adjusted child geometry and light FX.
        Undo.RecordObject(controller, "Isolate target health HUD");
        Undo.RecordObject(layout, "Isolate target health HUD");
        controller.healthBarFill = fill;
        layout.healthFill = fill;
        layout.frame = frame;
        layout.oldBackground = background.GetComponent<Image>();
        var trail = background.Find("HPDamageTrail")?.GetComponent<Image>();
        layout.hitOverlay = trail;
        if (animation != null)
        {
            Undo.RecordObject(animation, "Isolate target HP animation");
            animation.fill = fill;
            if (trail != null) animation.delayedFill = trail;
            if (animation.transitionLight != null &&
                !animation.transitionLight.transform.IsChildOf(hudRoot))
            {
                Debug.LogError("[Target HP] Light FX reference is external to HUD; refusing migration.");
                return;
            }
            EditorUtility.SetDirty(animation);
        }
        EditorUtility.SetDirty(layout);
        EditorUtility.SetDirty(controller);

        // SaveAndConnect preserves the existing scene GameObject and its links
        // from other scene components while creating a separate prefab asset.
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            hudRoot.gameObject, PrefabPath, InteractionMode.UserAction, out bool success);
        if (!success || prefab == null)
        {
            Debug.LogError("[Target HP] Failed to create independent target prefab.");
            return;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Target HP] Created independent " + PrefabPath +
            " and connected existing HUD object. Enemy prefab unchanged; HP source remains PlayerTargeting.CurrentTarget.");
        Selection.activeGameObject = hudRoot.gameObject;
    }
}
#endif
