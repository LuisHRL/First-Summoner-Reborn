#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>Read-only audit of mission UI interaction and hierarchy.</summary>
public static class MissionUIInputAudit
{
    [MenuItem("Tools/First Summoner/Diagnose Mission UI Input")]
    public static void Diagnose()
    {
        var sb = new StringBuilder();
        sb.AppendLine("First Summoner - Mission UI Input Audit");
        sb.AppendLine("Unity: " + Application.unityVersion);
        sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        var menus = Resources.FindObjectsOfTypeAll<MainMenu>();
        foreach (var menu in menus)
        {
            if (!menu.gameObject.scene.IsValid() || !menu.gameObject.scene.isLoaded) continue;
            sb.AppendLine("MainMenu: " + PathOf(menu.transform));
            var panel = menu.missionPanel;
            if (panel == null) { sb.AppendLine("ERROR: missionPanel is null"); continue; }
            sb.AppendLine("MissionPanel: " + PathOf(panel.transform) + " active=" + panel.activeInHierarchy);
            var rect = panel.GetComponent<RectTransform>();
            if (rect != null) sb.AppendLine("MissionPanel Rect: size=" + rect.rect.size + " scale=" + rect.lossyScale);
            foreach (var canvas in panel.GetComponentsInParent<Canvas>(true))
                CanvasInfo(sb, canvas);
            foreach (var canvas in panel.GetComponentsInChildren<Canvas>(true))
                CanvasInfo(sb, canvas);
            foreach (var raycaster in panel.GetComponentsInParent<GraphicRaycaster>(true))
                sb.AppendLine("Parent Raycaster: " + PathOf(raycaster.transform) + " enabled=" + raycaster.enabled + " active=" + raycaster.gameObject.activeInHierarchy);
            foreach (var raycaster in panel.GetComponentsInChildren<GraphicRaycaster>(true))
                sb.AppendLine("Child Raycaster: " + PathOf(raycaster.transform) + " enabled=" + raycaster.enabled + " active=" + raycaster.gameObject.activeInHierarchy);
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<TMP_Text>(true);
                var image = button.GetComponent<Image>();
                sb.AppendLine("Button: " + PathOf(button.transform) +
                    " active=" + button.gameObject.activeInHierarchy +
                    " enabled=" + button.enabled + " interactable=" + button.interactable +
                    " listeners=" + button.onClick.GetPersistentEventCount() +
                    " label=" + (label != null ? label.text : "(none)") +
                    " graphicRaycast=" + (image != null && image.raycastTarget));
            }
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget || !graphic.gameObject.activeInHierarchy) continue;
                var r = graphic.rectTransform;
                sb.AppendLine("Raycast Graphic: " + PathOf(graphic.transform) +
                    " type=" + graphic.GetType().Name + " depth=" + graphic.depth +
                    " rect=" + r.rect + " canvas=" + (graphic.canvas != null ? PathOf(graphic.canvas.transform) : "(none)"));
            }
            var map = panel.GetComponentInChildren<MissionMapUI>(true);
            sb.AppendLine("MissionMapUI: " + (map != null ? PathOf(map.transform) : "MISSING"));
        }
        foreach (var es in Resources.FindObjectsOfTypeAll<EventSystem>())
            if (es.gameObject.scene.IsValid() && es.gameObject.scene.isLoaded)
            {
                sb.AppendLine("EventSystem: " + PathOf(es.transform) + " enabled=" + es.enabled +
                    " active=" + es.gameObject.activeInHierarchy +
                    " module=" + (es.currentInputModule != null ? es.currentInputModule.GetType().Name : "(none)"));
            }
        string folder = "Assets/FirstSummonerDiagnostics";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "FirstSummonerDiagnostics");
        string file = folder + "/MissionUIInputAudit.txt";
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(file);
        Debug.Log("Mission UI input audit saved to " + file + "\n" + sb);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(file);
    }

    private static void CanvasInfo(StringBuilder sb, Canvas c)
    {
        var ray = c.GetComponent<GraphicRaycaster>();
        var group = c.GetComponent<CanvasGroup>();
        sb.AppendLine("Canvas: " + PathOf(c.transform) +
            " enabled=" + c.enabled + " active=" + c.gameObject.activeInHierarchy +
            " overrideSorting=" + c.overrideSorting + " order=" + c.sortingOrder +
            " renderMode=" + c.renderMode +
            " raycaster=" + (ray != null ? ray.enabled.ToString() : "MISSING") +
            " group=" + (group != null ? ("blocksRaycasts=" + group.blocksRaycasts + " interactable=" + group.interactable) : "(none)"));
    }

    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }
}
#endif