#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WorldHPVisualInstaller
{
    [MenuItem("Tools/First Summoner/Combat UI/Install Hero HP Adjustable Frame")]
    public static void Install()
    {
        const string scenePath = "Assets/Scenes/GameScene.unity";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        // Resolve by the actual scene hierarchy, not by PlayerHealth's location.
        // Some player prefabs keep the health component on another child.
        var roots = scene.GetRootGameObjects();
        var playerRoot = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "Player" && t.parent != null && t.parent.gameObject.scene == scene);
        if (playerRoot == null)
            playerRoot = roots.Select(root => root.transform).FirstOrDefault(t => t.name == "Player");
        var barTransform = playerRoot != null ? playerRoot.Find("WorldHealthBar") : null;
        var bar = barTransform != null ? barTransform.GetComponent<WorldHealthBar>() : null;
        var fillTransform = barTransform != null ? barTransform.Find("HealthBarBackground/HealthBarFill") : null;
        var fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        if (barTransform == null || bar == null || fill == null)
        {
            Debug.LogError("[Hero HP] Missing Player/WorldHealthBar/HealthBarBackground/HealthBarFill or Image component in GameScene. No changes.");
            return;
        }
        if (bar.healthBarFill != fill)
        {
            Undo.RecordObject(bar, "Repair hero HP fill reference");
            bar.healthBarFill = fill;
            EditorUtility.SetDirty(bar);
        }
        var parent = bar.healthBarFill.transform.parent;
        var existing = parent.Find("AdjustableHPFrame");
        GameObject go;
        if (existing == null)
        {
            go = new GameObject("AdjustableHPFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(AdjustableThreeSlice));
            Undo.RegisterCreatedObjectUndo(go, "Create adjustable hero HP frame");
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
        }
        else go = existing.gameObject;
        var frame = go.GetComponent<AdjustableThreeSlice>();
        if (frame == null) frame = Undo.AddComponent<AdjustableThreeSlice>(go);
        var layout = parent.GetComponent<WorldHealthVisualLayout>();
        bool fresh = layout == null;
        if (fresh) layout = Undo.AddComponent<WorldHealthVisualLayout>(parent.gameObject);
        layout.healthFill = bar.healthBarFill;
        layout.frame = frame;
        layout.oldBackground = parent.GetComponent<Image>();
        if (fresh)
        {
            var r = bar.healthBarFill.rectTransform;
            layout.fillSize = r.rect.size;
            layout.fillOffset = r.anchoredPosition;
            var backgroundRect = parent as RectTransform;
            layout.frameSize = backgroundRect.rect.size;
            layout.frameOffset = Vector2.zero;
            layout.originalFrameSprite = FindSprite("img_bar_hp_bg_01");
            // Preserve the hero's existing green HP sprite and tint.
            // img_bar_hp is red (enemy HP), and img_bar_hp_hit_green may be a hit overlay.
            layout.originalFillSprite = bar.healthBarFill.sprite;
            layout.hideOldBackground = true;
            layout.Apply();
        }
        EditorUtility.SetDirty(layout);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Hero HP] Adjustable frame installed; edit WorldHealthVisualLayout on HealthBarBackground, then Apply World HP Visual Layout.");
    }

    private static Sprite FindSprite(string name)
    {
        foreach (var atlasPath in new[] {
            "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_Ingame.png",
            "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_total.png" })
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().FirstOrDefault(s => s.name == name);
            if (sprite != null) return sprite;
        }
        Debug.LogWarning("[Hero HP] Missing sprite " + name + "; assign manually.");
        return null;
    }
}
#endif
