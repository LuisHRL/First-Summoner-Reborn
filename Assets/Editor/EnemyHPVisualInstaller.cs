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

    [MenuItem("Tools/First Summoner/Combat UI/Reset Enemy HP Native Sizes")]
    public static void ResetNativeSizes()
    {
        const string path = "Assets/Prefabs/UI/EnemyHealthBar.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var parent = prefab.transform.Find("HealthBarBackground");
            var layout = parent != null ? parent.GetComponent<WorldHealthVisualLayout>() : null;
            if (layout == null || layout.frame == null || layout.healthFill == null)
            {
                Debug.LogError("[Enemy HP] Install the adjustable enemy frame first. Nothing changed.");
                return;
            }
            // Sprite.rect is expressed in pixels. Convert through PPU to the
            // RectTransform's native UI units, without touching the prefab scale.
            var frameSprite = layout.originalFrameSprite ?? layout.frame.SourceSprite;
            var fillSprite = layout.originalFillSprite ?? layout.healthFill.sprite;
            if (frameSprite == null || fillSprite == null)
            {
                Debug.LogError("[Enemy HP] Original frame/fill sprites are missing. Nothing changed.");
                return;
            }
            // The two atlas sprites have unrelated native dimensions (e.g. 90x59
            // versus 329x29). Reset them to a shared bar footprint instead of
            // assigning both native sizes independently.
            Vector2 nativeFrame = NativeSize(frameSprite, layout.frame.canvas);
            Vector2 nativeFill = NativeSize(fillSprite, layout.healthFill.canvas);
            float width = Mathf.Max(1f, nativeFrame.x);
            float fillWidth = width * 0.88f;
            float fillHeight = fillWidth * nativeFill.y / Mathf.Max(1f, nativeFill.x);
            // Give the frame a little breathing room vertically, rather than
            // inheriting a 59px-high background for a ~7px-high red fill.
            float frameHeight = fillHeight * 1.65f;
            layout.frameSize = new Vector2(width, frameHeight);
            layout.fillSize = new Vector2(fillWidth, fillHeight);
            layout.frameOffset = Vector2.zero;
            layout.fillOffset = Vector2.zero;
            // Recover end caps from the original sprite instead of preserving
            // the erroneous 0.5px values left by the earlier reset.
            // Prefer authored sprite borders; for borderless atlas sprites,
            // use the original sprite height to estimate symmetrical end caps.
            float sourceCap = Mathf.Min(frameSprite.rect.width * 0.25f, frameSprite.rect.height * 0.5f);
            float left = frameSprite.border.x > 0f ? frameSprite.border.x : sourceCap;
            float right = frameSprite.border.z > 0f ? frameSprite.border.z : sourceCap;
            layout.sourceLeftPixels = left;
            layout.sourceRightPixels = right;
            float capScale = frameHeight / Mathf.Max(1f, frameSprite.rect.height);
            layout.displayedLeftWidth = left * capScale;
            layout.displayedRightWidth = right * capScale;
            layout.Apply();
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            Debug.Log("[Enemy HP] Enemy HP frame caps restored from sprite borders or proportional fallback; check sprite alpha separately.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    [MenuItem("Tools/First Summoner/Combat UI/Fit Enemy HP Fill Inside Frame")]
    public static void FitFillInsideFrame()
    {
        const string path = "Assets/Prefabs/UI/EnemyHealthBar.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var background = prefab.transform.Find("HealthBarBackground");
            var layout = background != null ? background.GetComponent<WorldHealthVisualLayout>() : null;
            if (layout == null || layout.frame == null || layout.healthFill == null)
            {
                Debug.LogError("[Enemy HP] Missing adjustable frame or fill. No changes made.");
                return;
            }
            // Only fit the red fill; preserve the manually validated frame,
            // source caps, positions and tint. The frame's opaque center must
            // stay behind the fill, not be moved in front of it.
            float width = Mathf.Max(1f, layout.frameSize.x);
            float height = Mathf.Max(1f, layout.frameSize.y);
            layout.fillSize = new Vector2(width * 0.82f, height * 0.43f);
            layout.fillOffset = Vector2.zero;
            layout.Apply();
            layout.frame.transform.SetAsFirstSibling();
            if (layout.hitOverlay != null)
            {
                layout.hitOverlay.transform.SetSiblingIndex(layout.healthFill.transform.GetSiblingIndex() + 1);
                layout.hitOverlay.enabled = false;
            }
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            Debug.Log("[Enemy HP] Fill fitted inside frame; frame geometry and cap settings preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    [MenuItem("Tools/First Summoner/Combat UI/Trim Enemy HP Corner Transitions")]
    public static void TrimCornerTransitions()
    {
        const string path = "Assets/Prefabs/UI/EnemyHealthBar.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var parent = prefab.transform.Find("HealthBarBackground");
            var layout = parent != null ? parent.GetComponent<WorldHealthVisualLayout>() : null;
            var sprite = layout != null ? layout.originalFrameSprite : null;
            if (layout == null || layout.frame == null || sprite == null)
            {
                Debug.LogError("[Enemy HP] Missing frame or source sprite. Nothing changed.");
                return;
            }
            // Trim the diagonal transition by sampling a narrower end section.
            // Preserve the frame's current size, fill geometry, tint and sprites.
            float cap = Mathf.Min(sprite.rect.width * 0.12f, sprite.rect.height * 0.18f);
            cap = Mathf.Max(1f, cap);
            layout.sourceLeftPixels = cap;
            layout.sourceRightPixels = cap;
            float scale = layout.frameSize.y / Mathf.Max(1f, sprite.rect.height);
            layout.displayedLeftWidth = cap * scale;
            layout.displayedRightWidth = cap * scale;
            layout.Apply();
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            Debug.Log("[Enemy HP] Corner transitions trimmed; existing frame and fill sizes preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    [MenuItem("Tools/First Summoner/Combat UI/Extend Enemy HP Frame Center")]
    public static void ExtendFrameCenter()
    {
        const string path = "Assets/Prefabs/UI/EnemyHealthBar.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var parent = prefab.transform.Find("HealthBarBackground");
            var layout = parent != null ? parent.GetComponent<WorldHealthVisualLayout>() : null;
            if (layout == null || layout.frame == null || layout.healthFill == null)
            {
                Debug.LogError("[Enemy HP] Missing adjustable frame or fill. Nothing changed.");
                return;
            }
            // Stretch only the middle of the three-slice mesh. The source
            // cuts, displayed caps, height, tint and fill stay unchanged.
            float endPadding = Mathf.Max(layout.displayedLeftWidth, layout.displayedRightWidth);
            float desiredWidth = layout.fillSize.x + 2f * (endPadding + layout.frameSize.y * 0.20f);
            layout.frameSize = new Vector2(Mathf.Max(layout.frameSize.x, desiredWidth), layout.frameSize.y);
            layout.Apply();
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            Debug.Log("[Enemy HP] Frame center extended around existing fill without changing end caps.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static Vector2 NativeSize(Sprite sprite, Canvas canvas)
    {
        float referencePPU = canvas != null ? canvas.referencePixelsPerUnit : 100f;
        float scale = referencePPU / Mathf.Max(0.001f, sprite.pixelsPerUnit);
        return sprite.rect.size * scale;
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
