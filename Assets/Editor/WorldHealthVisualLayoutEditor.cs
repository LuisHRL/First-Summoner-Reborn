#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Single inspector for all world HP visual adjustments.
/// Changes are previewed in Edit Mode, with Undo and prefab overrides.</summary>
[CustomEditor(typeof(WorldHealthVisualLayout))]
[CanEditMultipleObjects]
public sealed class WorldHealthVisualLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            foreach (Object item in targets)
            {
                var layout = item as WorldHealthVisualLayout;
                if (layout == null) continue;
                // Record changes to children made by Apply, not just the layout.
                if (layout.frame != null)
                {
                    Undo.RecordObject(layout.frame, "Update HP frame");
                    Undo.RecordObject(layout.frame.rectTransform, "Update HP frame geometry");
                }
                if (layout.healthFill != null)
                {
                    Undo.RecordObject(layout.healthFill, "Update HP fill");
                    Undo.RecordObject(layout.healthFill.rectTransform, "Update HP fill geometry");
                }
                if (layout.hitOverlay != null)
                {
                    Undo.RecordObject(layout.hitOverlay, "Update HP hit overlay");
                    Undo.RecordObject(layout.hitOverlay.rectTransform, "Update HP hit geometry");
                }
                if (layout.oldBackground != null)
                    Undo.RecordObject(layout.oldBackground, "Update HP background");
                layout.Apply();
                RecordPrefab(layout.frame);
                RecordPrefab(layout.healthFill);
                RecordPrefab(layout.hitOverlay);
                RecordPrefab(layout.oldBackground);
                if (layout.frame != null) RecordPrefab(layout.frame.rectTransform);
                if (layout.healthFill != null) RecordPrefab(layout.healthFill.rectTransform);
                if (layout.hitOverlay != null) RecordPrefab(layout.hitOverlay.rectTransform);
                EditorUtility.SetDirty(layout);
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.HelpBox("Les changements sont appliqués directement dans la Scene et sauvegardables dans le prefab. Frame Size X étire uniquement la section centrale ; Source Left/Right Pixels déterminent les limites des pointes dans le sprite.", MessageType.Info);
    }

    private static void RecordPrefab(Object obj)
    {
        if (obj is Component component)
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }
}
#endif
