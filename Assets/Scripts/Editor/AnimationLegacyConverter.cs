
using UnityEngine;
using UnityEditor;

public static class AnimationLegacyConverter
{
    [MenuItem("Tools/Skeleton/Convert Selected Animation")]
    public static void ConvertSelectedAnimation()
    {
        AnimationClip clip = Selection.activeObject as AnimationClip;

        if (clip == null)
        {
            Debug.LogWarning("Sélectionne un fichier .anim.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(clip);

        if (!path.EndsWith(".anim"))
        {
            Debug.LogWarning("Sélectionne une copie .anim indépendante.");
            return;
        }

        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty legacy = serializedClip.FindProperty("m_Legacy");

        if (legacy == null)
        {
            Debug.LogError("Propriété Legacy introuvable.");
            return;
        }

        legacy.boolValue = false;
        serializedClip.ApplyModifiedProperties();
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        Debug.Log("Animation convertie : " + clip.name);
    }
}
