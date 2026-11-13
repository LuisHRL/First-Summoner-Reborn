#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Idempotent, explicit installer. Never deletes gameplay UI or changes Slider references.</summary>
public static class CombatUIInstaller
{
    [MenuItem("Tools/First Summoner/Combat UI/Install Animated UI")]
    public static void Install()
    {
        const string scenePath = "Assets/Scenes/GameScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var playerUI = UnityEngine.Object.FindObjectsByType<PlayerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (playerUI == null || playerUI.manaBar == null)
        {
            Debug.LogError("[Combat UI] PlayerUI or ManaBar missing. No changes made.");
            return;
        }
        Undo.IncrementCurrentGroup();
        var manaSlider = playerUI.manaBar;
        var barFill = manaSlider.fillRect != null ? manaSlider.fillRect.GetComponent<Image>() : null;
        if (barFill == null)
        {
            Debug.LogError("[Combat UI] ManaBar Slider Fill Image missing. No changes made.");
            return;
        }
        var manaAnimation = EnsureAnimation(manaSlider.transform, "AnimatedManaBar", barFill, null, false);
        var manaIconSprite = FindSprite("icon_mana");
        var manaIcon = EnsureImage(manaSlider.transform.parent, "AnimatedManaIcon", manaIconSprite);
        var iconRect = manaIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot = new Vector2(1, 0.5f);
        iconRect.anchoredPosition = new Vector2(-8, 0);
        iconRect.sizeDelta = new Vector2(44, 44);
        manaIcon.color = new Color(0.25f, 0.18f, 0.35f, 1);
        manaIcon.gameObject.SetActive(manaIconSprite != null);
        var iconFill = EnsureImage(manaIcon.transform, "ManaIconFill", manaIcon.sprite);
        Stretch(iconFill.rectTransform);
        iconFill.color = Color.white;
        ConfigureFill(iconFill, Image.FillMethod.Vertical);
        var iconAnimation = EnsureAnimation(manaIcon.transform, "AnimatedManaIconFill", iconFill, null, true);
        var gain = EnsureImage(manaIcon.transform, "ManaGainFx", FindSprite("icon_mana_fx"));
        Stretch(gain.rectTransform);
        gain.raycastTarget = false;
        gain.gameObject.SetActive(false);
        Undo.RecordObject(playerUI, "Wire mana animation");
        playerUI.manaBarAnimation = manaAnimation;
        playerUI.manaIconAnimation = iconAnimation;
        playerUI.manaGainFx = gain;
        EditorUtility.SetDirty(playerUI);

        // Target UI: retain existing target tracking, replace only fill visuals.
        var target = UnityEngine.Object.FindObjectsByType<TargetHealthBarUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (target != null && target.healthBarFill != null)
        {
            var hpFill = target.healthBarFill;
            var hpSprite = FindSprite("img_bar_hp");
            if (hpSprite != null) { Undo.RecordObject(hpFill, "HP sprite"); hpFill.sprite = hpSprite; hpFill.color = Color.white; }
            ConfigureFill(hpFill, Image.FillMethod.Horizontal);
            var hit = EnsureImage(hpFill.transform.parent, "HPDamageTrail", FindSprite("img_bar_hp_hit"));
            hit.rectTransform.SetSiblingIndex(hpFill.transform.GetSiblingIndex());
            CopyRect(hpFill.rectTransform, hit.rectTransform);
            ConfigureFill(hit, Image.FillMethod.Horizontal);
            var hpAnimation = EnsureAnimation(hpFill.transform.parent, "AnimatedTargetHP", hpFill, hit, false);
            Undo.RecordObject(target, "Wire target HP animation");
            target.healthAnimation = hpAnimation;
            EditorUtility.SetDirty(target);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Prefab is configured independently, since enemy bars are spawned dynamically.
        const string prefabPath = "Assets/Prefabs/UI/EnemyHealthBar.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var enemyBar = prefab.GetComponent<EnemyHealthBar>();
            var hpFill = prefab.transform.Find("HealthBarBackground/HealthBarFill")?.GetComponent<Image>();
            if (enemyBar != null && hpFill != null)
            {
                var hpSprite = FindSprite("img_bar_hp");
                if (hpSprite != null) { hpFill.sprite = hpSprite; hpFill.color = Color.white; }
                ConfigureFill(hpFill, Image.FillMethod.Horizontal);
                var hit = EnsureImage(hpFill.transform.parent, "HPDamageTrail", FindSprite("img_bar_hp_hit"));
                CopyRect(hpFill.rectTransform, hit.rectTransform);
                hit.rectTransform.SetSiblingIndex(hpFill.transform.GetSiblingIndex());
                ConfigureFill(hit, Image.FillMethod.Horizontal);
                enemyBar.healthBarFill = hpFill;
                enemyBar.healthAnimation = EnsureAnimation(hpFill.transform.parent, "AnimatedEnemyHP", hpFill, hit, false);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Combat UI] Animated UI installed. Existing sliders, player HP and card mana icons preserved. Missing sprites use fallback visuals.");
    }

    private static AnimatedFillEffect EnsureAnimation(Transform parent, string name, Image fill, Image trail, bool vertical)
    {
        var t = parent.Find(name);
        GameObject go;
        if (t == null) { go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go, name); go.transform.SetParent(parent, false); }
        else go = t.gameObject;
        var animation = go.GetComponent<AnimatedFillEffect>();
        if (animation == null) animation = Undo.AddComponent<AnimatedFillEffect>(go);
        animation.fill = fill;
        animation.delayedFill = trail;
        animation.horizontalLight = vertical;
        var light = EnsureImage(fill.transform.parent, name + "Light", FindSprite("fx_light_03"));
        light.rectTransform.SetAsLastSibling();
        light.rectTransform.sizeDelta = vertical ? new Vector2(46, 8) : new Vector2(8, 28);
        light.raycastTarget = false;
        light.gameObject.SetActive(false);
        animation.transitionLight = light.sprite != null ? light : null;
        EditorUtility.SetDirty(animation);
        return animation;
    }

    private static Image EnsureImage(Transform parent, string name, Sprite sprite)
    {
        var t = parent.Find(name);
        GameObject go;
        if (t == null) { go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); Undo.RegisterCreatedObjectUndo(go, name); go.transform.SetParent(parent, false); }
        else go = t.gameObject;
        var image = go.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(go);
        if (sprite != null) image.sprite = sprite;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);
        return image;
    }

    private static Sprite FindSprite(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var match = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == name);
            if (match != null) return match;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var match = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == name);
            if (match != null) return match;
        }
        Debug.LogWarning("[Combat UI] Sprite not found: " + name + " (existing graphic retained where possible)");
        return null;
    }

    private static void ConfigureFill(Image image, Image.FillMethod method)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = method;
        image.fillOrigin = 0;
        image.fillAmount = 1f;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }
    private static void CopyRect(RectTransform source, RectTransform dest)
    {
        dest.anchorMin = source.anchorMin; dest.anchorMax = source.anchorMax;
        dest.pivot = source.pivot; dest.anchoredPosition = source.anchoredPosition;
        dest.sizeDelta = source.sizeDelta;
    }
}
#endif
