
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AnimationMerger
{
    private const string Folder =
        "Assets/Animations/Animations-new/";

    private const string GetArrowPath =
        Folder + "GetArrow.fbx";

    private const string BowShotPath =
        Folder + "BowShot.fbx";

    [MenuItem("Tools/Animations/Creer Attack")]
    public static void CreateAttack()
    {
        AnimationClip getArrow = FindClip(
            GetArrowPath, "Archer@GetArrow01");

        AnimationClip bowShot = FindClip(
            BowShotPath, "Archer@BowShot01");

        if (getArrow == null || bowShot == null)
        {
            Debug.LogError(
                "Impossible de trouver GetArrow ou BowShot. " +
                "Vérifie les noms des fichiers FBX et des clips.");
            return;
        }

        float offset = getArrow.length;

        AnimationClip attack = new AnimationClip
        {
            name = "Attack",
            frameRate = 30,
            legacy = false,
            wrapMode = WrapMode.Once
        };

        HashSet<EditorCurveBinding> bindings =
            new HashSet<EditorCurveBinding>();

        foreach (var binding in
                 AnimationUtility.GetCurveBindings(getArrow))
            bindings.Add(binding);

        foreach (var binding in
                 AnimationUtility.GetCurveBindings(bowShot))
            bindings.Add(binding);

        int copiedCurves = 0;

        foreach (var binding in bindings)
        {
            AnimationCurve first =
                AnimationUtility.GetEditorCurve(
                    getArrow, binding);

            AnimationCurve second =
                AnimationUtility.GetEditorCurve(
                    bowShot, binding);

            List<Keyframe> keys = new List<Keyframe>();

            if (first != null)
            {
                foreach (Keyframe key in first.keys)
                    keys.Add(key);
            }

            if (second != null)
            {
                foreach (Keyframe key in second.keys)
                {
                    Keyframe shifted = key;
                    shifted.time += offset;
                    keys.Add(shifted);
                }
            }

            if (keys.Count == 0)
                continue;

            keys.Sort((a, b) =>
                a.time.CompareTo(b.time));

            // Supprimer les clés en double à la jonction.
            for (int i = keys.Count - 2; i >= 0; i--)
            {
                if (Mathf.Abs(
                    keys[i].time - keys[i + 1].time) < 0.00001f)
                {
                    keys.RemoveAt(i);
                }
            }

            AnimationCurve merged =
                new AnimationCurve(keys.ToArray());

            AnimationUtility.SetEditorCurve(
                attack, binding, merged);

            copiedCurves++;
        }

        string output = Folder + "Attack.anim";

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(output)
            != null)
        {
            Debug.LogError(
                "Attack.anim existe déjà. " +
                "Supprime-le ou renomme-le avant de relancer.");
            return;
        }

        if (copiedCurves == 0)
        {
            Debug.LogError(
                "Aucune courbe accessible. " +
                "La fusion directe des animations Humanoid " +
                "n'est pas possible avec cette méthode.");
            DestroyClip(attack);
            return;
        }

        AssetDatabase.CreateAsset(attack, output);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "FUSION TERMINEE\n" +
            "GetArrow : " + getArrow.length.ToString("F3") + " s\n" +
            "BowShot : " + bowShot.length.ToString("F3") + " s\n" +
            "Attack : " + attack.length.ToString("F3") + " s\n" +
            "Courbes copiées : " + copiedCurves + "\n" +
            "Fichier : " + output);

        Selection.activeObject = attack;
        EditorGUIUtility.PingObject(attack);
    }

    private static AnimationClip FindClip(
        string path, string clipName)
    {
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(path);

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is AnimationClip clip &&
                clip.name == clipName)
            {
                return clip;
            }
        }

        return null;
    }

    private static void DestroyClip(AnimationClip clip)
    {
        UnityEngine.Object.DestroyImmediate(clip);
    }
}
