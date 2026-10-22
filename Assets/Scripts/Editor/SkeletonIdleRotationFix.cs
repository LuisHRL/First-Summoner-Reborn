
using UnityEngine;
using UnityEditor;
using System.Linq;

public static class SkeletonIdleRotationFix
{
    [MenuItem("Tools/Skeleton/Fix Idle Orientation")]
    public static void Fix()
    {
        AnimationClip clip = Selection.activeObject as AnimationClip;

        if (clip == null || clip.name != "Lesha_idle")
        {
            Debug.LogWarning("Sélectionne Lesha_idle.anim.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(clip);

        if (!path.EndsWith("/Lesha_idle.anim"))
        {
            Debug.LogError("Fichier Idle incorrect.");
            return;
        }

        var bindings = AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.path.EndsWith("/Bone_57") &&
                        b.propertyName.StartsWith("m_LocalRotation."))
            .ToArray();

        if (bindings.Length != 4)
        {
            Debug.LogError("Les 4 courbes de rotation sont introuvables.");
            return;
        }

        AnimationCurve Curve(string axis)
        {
            var binding = bindings.First(
                b => b.propertyName == "m_LocalRotation." + axis);

            return AnimationUtility.GetEditorCurve(clip, binding);
        }

        var cx = Curve("x");
        var cy = Curve("y");
        var cz = Curve("z");
        var cw = Curve("w");

        float fps = clip.frameRate;
        int count = Mathf.CeilToInt(clip.length * fps);

        Quaternion correction =
            Quaternion.Euler(0f, 45f, 0f);

        var nx = new AnimationCurve();
        var ny = new AnimationCurve();
        var nz = new AnimationCurve();
        var nw = new AnimationCurve();

        for (int frame = 0; frame <= count; frame++)
        {
            float time = Mathf.Min(frame / fps, clip.length);

            Quaternion original = new Quaternion(
                cx.Evaluate(time),
                cy.Evaluate(time),
                cz.Evaluate(time),
                cw.Evaluate(time)
            ).normalized;

            Quaternion result = correction * original;

            nx.AddKey(time, result.x);
            ny.AddKey(time, result.y);
            nz.AddKey(time, result.z);
            nw.AddKey(time, result.w);
        }

        Undo.RegisterCompleteObjectUndo(
            clip, "Fix Skeleton Idle Orientation");

        AnimationUtility.SetEditorCurve(
            clip, bindings.First(b => b.propertyName.EndsWith(".x")), nx);

        AnimationUtility.SetEditorCurve(
            clip, bindings.First(b => b.propertyName.EndsWith(".y")), ny);

        AnimationUtility.SetEditorCurve(
            clip, bindings.First(b => b.propertyName.EndsWith(".z")), nz);

        AnimationUtility.SetEditorCurve(
            clip, bindings.First(b => b.propertyName.EndsWith(".w")), nw);

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        Debug.Log("Orientation Idle corrigée (+42.834° sur Y).");
    }
}
