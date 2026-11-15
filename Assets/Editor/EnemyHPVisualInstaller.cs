#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Installs adjustable visuals on the spawned enemy HP prefab only.
/// Re-running does not overwrite manually tuned sizes, sprites or colors.</summary>
public static class EnemyHPVisualInstaller
{
    [MenuItem("Tools/First Summoner/Combat UI/Install Enemy HP Adjustable Frame")]
    public static void Install()
    {
        const string path = "Assets/Prefabs/UI/EnemyHealthBar.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var enemyBar = prefab.GetComponent<EnemyHealthBar>();
            var fill = prefab.transform.Find("HealthBarBackground/HealthBarFill")?.GetComponent<Image>();
            if (enemyBar == null || fill == null)
            {
                Debug.LogError("[Enemy HP] Missing EnemyHealthBar or HealthBarBackground/HealthBarFill. No changes.");
                return;
            }
            var parent = fill.transform.parent;
            var frameTransform = parent.Find("AdjustableHPFrame");
            AdjustableThreeSlice frame;
            if (frameTransform == null)
            {
                var go = new GameObject("AdjustableHPFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(AdjustableThreeSlice));
                go.transform.SetParent(parent, false);
                go.transform.SetAsFirstSibling();
                frame = go.GetComponent<AdjustableThreeSlice>();
            }
            else frame = frameTransform.GetComponent<AdjustableThreeSlice>();
            if (frame == null) frame = frameTransform.gameObject.AddComponent<AdjustableThreeSlice>();

            var layout = parent.GetComponent<WorldHealthVisualLayout>();
            bool firstInstall = layout == null;
            if (firstInstall) layout = parent.gameObject.AddComponent<WorldHealthVisualLayout>();
            layout.healthFill = fill;
            layout.frame = frame;
            layout.oldBackground = parent.GetComponent<Image>();

            var hitTransform = parent.Find("HPDamageTrail");
            Image hit = hitTransform != null ? hitTransform.GetComponent<Image>() : null;
            if (hit == null)
            {
                var go = new GameObject("HPDamageTrail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                hit = go.GetComponent<Image>();
            }
            hit.transform.SetSiblingIndex(fill.transform.GetSiblingIndex() + 1);
            hit.raycastTarget = false;
            hit.type = Image.Type.Filled;
            hit.fillMethod = fill.fillMethod;
            hit.fillOrigin = fill.fillOrigin;
            hit.fillClockwise = fill.fillClockwise;
            layout.hitOverlay = hit;

            if (firstInstall)
            {
                layout.originalFrameSprite = FindSprite("img_bar_hp_bg_01") ?? FindSprite("img_bar_hp_bg");
                layout.originalFillSprite = fill.sprite;
                layout.frameSize = ((RectTransform)parent).rect.size;
                layout.fillSize = fill.rectTransform.rect.size;
                layout.fillOffset = fill.rectTransform.anchoredPosition;
                layout.normalFillTint = new Color(0.65f, 0.25f, 0.25f, 1f);
                layout.hitOverlayTint = Color.white;
                layout.hideOldBackground = true;
                layout.applyFillTint = true;
                layout.Apply();
            }
            if (hit.sprite == null) hit.sprite = FindSprite("img_bar_hp_hit") ?? fill.sprite;

            // Reuse existing enemy damage animation and its light.
            if (enemyBar.healthAnimation != null)
            {
                enemyBar.healthAnimation.fill = fill;
                enemyBar.healthAnimation.delayedFill = hit;
                enemyBar.healthAnimation.flashRemainingHealthOnDamage = true;
                enemyBar.healthAnimation.hitFlashDuration = 0.24f;
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            Debug.Log("[Enemy HP] Adjustable prefab frame ready. Edit WorldHealthVisualLayout on the prefab's HealthBarBackground; do not rerun Install Animated UI afterward.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static Sprite FindSprite(string name)
    {
        foreach (var atlasPath in new[] {
            "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_Ingame.png",
            "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_total.png" })
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>()
                .FirstOrDefault(s => s.name == name);
            if (sprite != null) return sprite;
        }
        Debug.LogWarning("[Enemy HP] Sprite not found: " + name);
        return null;
    }
}
#endif
