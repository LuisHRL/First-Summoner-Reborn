#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time visual preset for the selected enemy's screen-top HP bar.
/// Never changes spawned enemy world bars or the hero bar.</summary>
public static class TargetHPVisualInstaller
{
    [MenuItem("Tools/First Summoner/Combat UI/Install Target HP Adjustable Frame")]
    public static void Install()
    {
        const string scenePath = "Assets/Scenes/GameScene.unity";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var targets = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<TargetHealthBarUI>(true)).ToArray();
        if (targets.Length != 1)
        {
            Debug.LogError("[Target HP] Expected exactly one TargetHealthBarUI in GameScene; found " + targets.Length + ". Nothing changed.");
            return;
        }
        var target = targets[0];
        Image fill = target.healthBarFill;
        if (fill == null || fill.transform.parent == null)
        {
            Debug.LogError("[Target HP] TargetHealthBarUI has no assigned healthBarFill. Nothing changed.");
            return;
        }
        Transform parent = fill.transform.parent;
        var layout = parent.GetComponent<WorldHealthVisualLayout>();
        if (layout != null)
        {
            Debug.Log("[Target HP] Visual layout already exists; preserving all Inspector settings.");
            return;
        }
        var frameTransform = parent.Find("AdjustableHPFrame");
        AdjustableThreeSlice frame;
        if (frameTransform == null)
        {
            var go = new GameObject("AdjustableHPFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(AdjustableThreeSlice));
            Undo.RegisterCreatedObjectUndo(go, "Create target HP frame");
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            frame = go.GetComponent<AdjustableThreeSlice>();
        }
        else
        {
            frame = frameTransform.GetComponent<AdjustableThreeSlice>();
            if (frame == null) frame = Undo.AddComponent<AdjustableThreeSlice>(frameTransform.gameObject);
        }

        layout = Undo.AddComponent<WorldHealthVisualLayout>(parent.gameObject);
        layout.healthFill = fill;
        layout.frame = frame;
        layout.oldBackground = parent.GetComponent<Image>();
        layout.originalFrameSprite = FindSprite("img_bar_hp_bg_01");
        layout.originalFillSprite = FindSprite("img_bar_hp") ?? fill.sprite;
        if (layout.originalFrameSprite == null)
        {
            Debug.LogError("[Target HP] Original frame sprite not found. Remove the new layout or assign the sprite manually.");
            return;
        }

        // Copy the user's last validated inspector values. These are local
        // UI dimensions; never alter Canvas scale, anchors or screen position.
        layout.frameSize = new Vector2(85.5f, 9.5f);
        layout.frameOffset = Vector2.zero;
        layout.fillSize = new Vector2(73.8f, 4.953129f);
        layout.fillOffset = new Vector2(0f, 0.3f);
        layout.sourceLeftPixels = 40f;
        layout.sourceRightPixels = 40f;
        layout.displayedLeftWidth = 12f;
        layout.displayedRightWidth = 12f;
        layout.applyFillTint = false;
        layout.hideOldBackground = true;

        // Preserve target health animation; the overlay is not a new effect.
        if (target.healthAnimation != null)
        {
            var overlay = target.healthAnimation.delayedFill;
            if (overlay != null && overlay != fill)
                layout.hitOverlay = overlay;
        }
        layout.Apply();
        // With tint disabled, the existing Image color might still be red.
        fill.color = Color.white;
        EditorUtility.SetDirty(fill);
        EditorUtility.SetDirty(layout);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Target HP] Screen-top target frame installed. Edit World Health Visual Layout in the Inspector; enemy world bars unchanged.");
    }

    [MenuItem("Tools/First Summoner/Combat UI/Separate Target HP Visual References")]
    public static void SeparateTargetVisualReferences()
    {
        const string scenePath = "Assets/Scenes/GameScene.unity";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var targets = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<TargetHealthBarUI>(true)).ToArray();
        if (targets.Length != 1)
        {
            Debug.LogError("[Target HP] Expected one TargetHealthBarUI, found " + targets.Length + ". No changes.");
            return;
        }
        var target = targets[0];
        // Use the screen HUD's own fill, not a similarly named fill in a world prefab.
        var localFill = target.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(image => image.name == "TargetHealthFill");
        if (localFill == null)
        {
            Debug.LogError("[Target HP] No local TargetHealthFill under TargetHealthBarUI. Nothing changed.");
            return;
        }
        var background = localFill.transform.parent;
        var layout = background.GetComponent<WorldHealthVisualLayout>();
        var frame = background.Find("AdjustableHPFrame")?.GetComponent<AdjustableThreeSlice>();
        if (layout == null || frame == null)
        {
            Debug.LogError("[Target HP] TargetHealthBackground lacks local layout/frame. Run initial installer first.");
            return;
        }
        // Verify every reference belongs to the same HUD subtree before repair.
        var trail = background.Find("HPDamageTrail")?.GetComponent<Image>();
        Undo.RecordObject(target, "Isolate target HP references");
        Undo.RecordObject(layout, "Isolate target HP visual references");
        target.healthBarFill = localFill;
        layout.healthFill = localFill;
        layout.frame = frame;
        layout.oldBackground = background.GetComponent<Image>();
        layout.hitOverlay = trail;
        if (target.healthAnimation != null)
        {
            var animation = target.healthAnimation;
            if (!animation.transform.IsChildOf(target.transform))
            {
                Debug.LogError("[Target HP] Animation component is outside target HUD. Refusing to modify cross-hierarchy animation.");
                return;
            }
            Undo.RecordObject(animation, "Isolate target HP animation");
            animation.fill = localFill;
            if (trail != null) animation.delayedFill = trail;
            if (animation.transitionLight != null &&
                !animation.transitionLight.transform.IsChildOf(target.transform))
            {
                animation.transitionLight = null;
                Debug.LogWarning("[Target HP] Removed cross-hierarchy light reference; assign a local HUD light.");
            }
            EditorUtility.SetDirty(animation);
        }
        // Do not call Apply: existing dimensions, colors, UV cuts and light FX
        // remain exactly as the user tuned them.
        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(layout);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Target HP] References isolated to target HUD. No visual sizes or colors changed.");
    }

    private static Sprite FindSprite(string name)
    {
        foreach (string path in new[] {
            "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_Ingame.png",
            "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_total.png" })
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .FirstOrDefault(s => s.name == name);
            if (sprite != null) return sprite;
        }
        return null;
    }
}
#endif
