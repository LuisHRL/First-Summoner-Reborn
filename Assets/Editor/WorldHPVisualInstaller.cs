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
        var hero = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include).FirstOrDefault();
        var bar = hero != null ? hero.GetComponentInChildren<WorldHealthBar>(true) : null;
        if (bar == null || bar.healthBarFill == null)
        {
            Debug.LogError("[Hero HP] WorldHealthBar / HealthBarFill not found; no changes.");
            return;
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
            layout.originalFillSprite = FindSprite("img_bar_hp");
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
