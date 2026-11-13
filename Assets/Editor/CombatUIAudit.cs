#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Read-only audit of existing combat UI, including inactive scene objects.</summary>
public static class CombatUIAudit
{
    [MenuItem("Tools/First Summoner/Combat UI/Audit GameScene")]
    public static void Audit()
    {
        const string scenePath = "Assets/Scenes/GameScene.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var report = new StringBuilder();
        report.AppendLine("Combat UI audit - " + scenePath);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    if (!(component is PlayerUI) && !(component is EnemyHealthBar) &&
                        !(component is WorldHealthBar) && !(component is TargetHealthBarUI) &&
                        !(component is AnimatedFillEffect)) continue;
                    report.AppendLine("\nCOMPONENT " + PathOf(component.transform) +
                                      " [" + component.GetType().Name + "] active=" +
                                      component.gameObject.activeInHierarchy);
                    SerializedObject serialized = new SerializedObject(component);
                    SerializedProperty iterator = serialized.GetIterator();
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                            report.AppendLine("  " + iterator.propertyPath + " = " +
                                (iterator.objectReferenceValue != null ?
                                    AssetDatabase.GetAssetPath(iterator.objectReferenceValue) + " :: " +
                                    iterator.objectReferenceValue.name : "None"));
                    }
                }
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image == null) continue;
                    string path = PathOf(image.transform);
                    if (path.IndexOf("mana", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf("health", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf("hp", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    report.AppendLine("IMAGE " + path + " active=" + image.gameObject.activeSelf +
                        " sprite=" + (image.sprite != null ? image.sprite.name : "None") +
                        " type=" + image.type + " fill=" + image.fillAmount);
                }
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        const string output = "Temp/FirstSummoner_CombatUI_Audit.txt";
        Directory.CreateDirectory("Temp");
        File.WriteAllText(output, report.ToString());
        Debug.Log("[Combat UI Audit] Report saved: " + output + "\n" + report);
    }

    private static string PathOf(Transform t)
    {
        string result = t.name;
        while (t.parent != null) { t = t.parent; result = t.name + "/" + result; }
        return result;
    }
}
#endif
