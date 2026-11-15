#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time setup on the independent target prefab, never on enemy prefabs.</summary>
public static class TargetHPDamageFXSetup
{
    private const string Path = "Assets/Prefabs/UI/TargetHealthBar.prefab";

    [MenuItem("Tools/First Summoner/Combat UI/Install Target Damage Sparks")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode first."); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) == null)
        {
            Debug.LogError("[Target FX] Independent target prefab missing: " + Path);
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var controller = root.GetComponent<TargetHealthBarUI>();
            if (controller == null || controller.healthBarFill == null)
            {
                Debug.LogError("[Target FX] Target controller or fill missing. No changes.");
                return;
            }
            var fill = controller.healthBarFill;
            var component = root.GetComponent<TargetHPDamageParticles>();
            if (component == null) component = root.AddComponent<TargetHPDamageParticles>();
            component.fill = fill;
            component.transitionLight = controller.healthAnimation != null
                ? controller.healthAnimation.transitionLight : null;
            // Sprite may be stored in one of the extracted atlases. Never
            // overwrite a manually selected particle sprite.
            if (component.particleSprite == null)
            {
                foreach (string atlas in new[] {
                    "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_Ingame.png",
                    "Assets/Generated/AssetRipperSprites/Sprite_Atlas_UI_z_total.png" })
                {
                    component.particleSprite = AssetDatabase.LoadAllAssetsAtPath(atlas)
                        .OfType<Sprite>().FirstOrDefault(s => s.name == "FX_Light_Particle_Red_00");
                    if (component.particleSprite != null) break;
                }
            }
            controller.damageParticles = component;
            if (component.particleSprite == null)
                Debug.LogWarning("[Target FX] Particle sprite not found in atlases. Assign FX_Light_Particle_Red_00 in prefab Inspector.");
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            Debug.Log("[Target FX] Independent target damage FX installed. Enemy prefab unchanged.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
