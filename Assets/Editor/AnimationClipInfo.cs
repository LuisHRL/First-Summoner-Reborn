
using UnityEngine;
using UnityEditor;

public class AnimationClipInfo
{
    [MenuItem("Tools/Animations/Afficher les informations")]
    public static void ShowInfo()
    {
        Object[] selection = Selection.objects;

        foreach (Object obj in selection)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__"))
                {
                    Debug.Log(
                        $"Animation : {clip.name}\n" +
                        $"Fréquence : {clip.frameRate} FPS\n" +
                        $"Durée : {clip.length:F3} secondes\n" +
                        $"Nombre d'images approximatif : " +
                        $"{Mathf.RoundToInt(clip.length * clip.frameRate)}"
                    );
                }
            }
        }
    }
}
