
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public static class AnimationRotationComparer
{
    [MenuItem("Tools/Skeleton/Compare Selected Animations")]
    public static void Compare()
    {
        var clips = Selection.objects
            .OfType<AnimationClip>()
            .ToArray();

        if (clips.Length != 2)
        {
            Debug.LogWarning(
                "Sélectionne exactement deux fichiers .anim : Idle et Walk.");
            return;
        }

        var first = GetRotations(clips[0]);
        var second = GetRotations(clips[1]);

        var differences = new List<string>();

        foreach (string path in first.Keys.Intersect(second.Keys))
        {
            float angle = Quaternion.Angle(first[path], second[path]);

            if (angle > 1f)
            {
                differences.Add(
                    $"{path} : {angle:F1} degrés");
            }
        }

        Debug.Log(
            $"Comparaison : {clips[0].name} / {clips[1].name}\n" +
            string.Join("\n", differences.Take(20)));
    }

    private static Dictionary<string, Quaternion> GetRotations(
        AnimationClip clip)
    {
        var result = new Dictionary<string, Quaternion>();
        var bindings = AnimationUtility.GetCurveBindings(clip);

        foreach (var group in bindings
            .Where(b => b.propertyName.StartsWith("m_LocalRotation."))
            .GroupBy(b => b.path))
        {
            float[] q = new float[4];

            foreach (var binding in group)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                float value = curve.Evaluate(0f);

                switch (binding.propertyName)
                {
                    case "m_LocalRotation.x": q[0] = value; break;
                    case "m_LocalRotation.y": q[1] = value; break;
                    case "m_LocalRotation.z": q[2] = value; break;
                    case "m_LocalRotation.w": q[3] = value; break;
                }
            }

            result[group.Key] =
                new Quaternion(q[0], q[1], q[2], q[3]);
        }

        return result;
    }
}
