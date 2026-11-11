#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FirstSummonerMissionSetup
{
    [MenuItem("Tools/First Summoner/Setup Mission System")]
    public static void Setup()
    {
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.missionPanel == null || menu.missionPanel.GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("Mission setup: open the menu scene with a MainMenu, missionPanel and Canvas.");
            return;
        }

        EnsureFolder("Assets", "GameData");
        EnsureFolder("Assets/GameData", "Missions");
        const string worldPath = "Assets/GameData/Missions/MissionWorld.asset";
        var world = AssetDatabase.LoadAssetAtPath<MissionWorldDefinition>(worldPath);
        if (world == null)
        {
            world = ScriptableObject.CreateInstance<MissionWorldDefinition>();
            AssetDatabase.CreateAsset(world, worldPath);
        }

        RectTransform root = Rect(menu.missionPanel.transform, "GeneratedMissionMapUI");
        Stretch(root, Vector2.zero, Vector2.one);
        root.offsetMin = new Vector2(20, 20);
        root.offsetMax = new Vector2(-20, -20);

        var navigator = Component<MissionMenuNavigator>(root.gameObject);
        var map = Component<MissionMapUI>(root.gameObject);
        Reference(navigator, "world", world);
        Reference(map, "navigator", navigator);
        Reference(menu, "missionNavigator", navigator);

        var heading = Text(root, "Heading", "World", 30);
        Stretch((RectTransform)heading.transform, new Vector2(0, .87f), Vector2.one);
        var illustration = Component<Image>(Rect(root, "Illustration").gameObject);
        illustration.raycastTarget = false;
        illustration.enabled = illustration.sprite != null;
        Stretch((RectTransform)illustration.transform, new Vector2(.05f, .49f), new Vector2(.95f, .85f));

        RectTransform missionMapArea = Rect(root, "MissionMapArea");
        Stretch(missionMapArea, new Vector2(.08f, .14f), new Vector2(.92f, .86f));
        missionMapArea.SetAsLastSibling();
        var placeholder = Component<Image>(missionMapArea.gameObject);
        placeholder.color = new Color(.13f, .18f, .23f, .85f);
        placeholder.raycastTarget = false;
        var mapLayout = missionMapArea.GetComponent<VerticalLayoutGroup>();
        if (mapLayout != null) Object.DestroyImmediate(mapLayout);

        RectTransform entries = Rect(root, "Entries");
        Stretch(entries, new Vector2(.05f, .15f), new Vector2(.95f, .47f));
        var layout = Component<VerticalLayoutGroup>(entries.gameObject);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        Button template = Button(root, "EntryTemplate", "Mission");
        template.gameObject.SetActive(false);
        Component<LayoutElement>(template.gameObject).preferredHeight = 54;

        Button back = Button(root, "Back", "Back");
        Stretch((RectTransform)back.transform, new Vector2(.05f, .02f), new Vector2(.30f, .12f));
        Button launch = Button(root, "Launch", "Launch");
        Stretch((RectTransform)launch.transform, new Vector2(.70f, .02f), new Vector2(.95f, .12f));

        Reference(map, "entriesContainer", entries);
        Reference(map, "missionMapArea", missionMapArea);
        Reference(map, "entryButtonPrefab", template);
        Reference(map, "heading", heading);
        Reference(map, "mapIllustration", illustration);
        Reference(map, "backButton", back);
        Reference(map, "launchButton", launch);

        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(map);
        EditorUtility.SetDirty(navigator);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Mission UI created or updated. Save the scene; edit " + worldPath + " to add regions.");
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static T Component<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found as RectTransform;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Text(Transform parent, string name, string label, float size)
    {
        var rect = Rect(parent, name);
        var text = Component<TextMeshProUGUI>(rect.gameObject);
        text.text = label;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static Button Button(Transform parent, string name, string label)
    {
        var rect = Rect(parent, name);
        Component<Image>(rect.gameObject);
        var button = Component<Button>(rect.gameObject);
        var text = Text(rect, "Label", label, 22);
        Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one);
        return button;
    }

    private static void Reference(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property != null)
        {
            property.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
#endif