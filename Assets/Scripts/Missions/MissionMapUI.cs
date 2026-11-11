using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders the mission hierarchy from ScriptableObject definitions.
/// Attach to the Missions panel and assign the UI references in the Inspector.
/// No region or mission is hardcoded.
/// </summary>
public sealed class MissionMapUI : MonoBehaviour
{
    [SerializeField] private MissionMenuNavigator navigator;
    [SerializeField] private RectTransform entriesContainer;
    [SerializeField] private RectTransform missionMapArea;
    [SerializeField] private Button entryButtonPrefab;
    [Header("Mission markers")]
    [SerializeField] private Sprite missionMarkerSprite;
    [SerializeField] private Vector2 missionMarkerSize = new Vector2(88f, 100f);
    [SerializeField] private TMP_Text heading;
    [SerializeField] private Image mapIllustration;
    [SerializeField] private Button backButton;
    [Header("Mission launch")]
    [SerializeField] private Button launchButton;
    [SerializeField] private string gameplaySceneName = "GameScene";

    [SerializeField] private MissionProgress.Difficulty difficulty = MissionProgress.Difficulty.Normal;

    public void ShowNormal() { difficulty = MissionProgress.Difficulty.Normal; Refresh(); }
    public void ShowMedium() { difficulty = MissionProgress.Difficulty.Medium; Refresh(); }

    private void Awake()
    {
        // Repair older scene instances that contain a nested Canvas without a raycaster.
        var localCanvas = GetComponent<Canvas>();
        if (localCanvas != null)
        {
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
            localCanvas.overrideSorting = true;
            localCanvas.sortingOrder = 10;
        }
    }

    private void OnEnable()
    {
        if (navigator != null) navigator.SelectionChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (navigator != null) navigator.SelectionChanged -= Refresh;
    }

    public void Refresh()
    {
        if (navigator == null || entriesContainer == null || entryButtonPrefab == null)
            return;

        ClearEntries(entriesContainer);
        if (missionMapArea != null && missionMapArea != entriesContainer)
            ClearEntries(missionMapArea);
        if (missionMapArea != null)
            missionMapArea.gameObject.SetActive(navigator.CurrentPage == MissionMenuNavigator.Page.Region);
        entriesContainer.gameObject.SetActive(navigator.CurrentPage == MissionMenuNavigator.Page.World);

        if (backButton != null)
        {
            backButton.gameObject.SetActive(navigator.CurrentPage != MissionMenuNavigator.Page.World);
            backButton.onClick.RemoveListener(GoBack);
            backButton.onClick.AddListener(GoBack);
        }

        if (launchButton != null)
        {
            launchButton.gameObject.SetActive(navigator.CurrentPage == MissionMenuNavigator.Page.Mission);
            launchButton.onClick.RemoveListener(LaunchSelectedMission);
            launchButton.onClick.AddListener(LaunchSelectedMission);
        }

        switch (navigator.CurrentPage)
        {
            case MissionMenuNavigator.Page.World:
                SetHeading("World");
                SetImage(navigator.World != null ? navigator.World.worldMap : null);
                if (navigator.World != null && navigator.World.regions != null)
                    foreach (var region in navigator.World.regions)
                        if (region != null)
                            AddEntry(
                                (MissionProgress.IsRegionUnlocked(navigator.World, region, difficulty) ? "" : "🔒 ") + region.displayName,
                                () => navigator.SelectRegion(region),
                                MissionProgress.IsRegionUnlocked(navigator.World, region, difficulty));
                break;

            case MissionMenuNavigator.Page.Region:
                var regionSelected = navigator.SelectedRegion;
                SetHeading(regionSelected != null ? regionSelected.displayName : "");
                bool unlocked = MissionProgress.IsRegionUnlocked(navigator.World, regionSelected, difficulty);
                if (!unlocked) SetHeading((regionSelected != null ? regionSelected.displayName : "") +
                    " - Locked");
                SetImage(regionSelected != null ? regionSelected.mapIllustration : null);
                if (regionSelected != null && regionSelected.missions != null)
                    for (int missionIndex = 0; missionIndex < regionSelected.missions.Length; missionIndex++)
                    {
                        var mission = regionSelected.missions[missionIndex];
                        if (mission != null)
                        {
                            bool available = MissionProgress.IsMissionUnlocked(
                                navigator.World, regionSelected, mission, difficulty);
                            if (!available) continue; // Future missions stay hidden.
                            string label = mission.displayName;
                            AddMapMarker(label, mission, missionIndex, () => navigator.SelectMission(mission));
                        }
                    }
                break;

            case MissionMenuNavigator.Page.Mission:
                var selected = navigator.SelectedMission;
                SetHeading(selected != null ? selected.displayName : "");
                SetImage(selected != null ? selected.illustration : null);
                break;
        }
    }

    public void LaunchSelectedMission()
    {
        if (navigator == null || navigator.CurrentPage != MissionMenuNavigator.Page.Mission ||
            !GameSession.TrySelectMission(navigator.World, navigator.SelectedRegion,
                navigator.SelectedMission, difficulty))
        {
            Debug.LogWarning("MissionMapUI: mission unavailable or incomplete selection.");
            return;
        }
        if (string.IsNullOrWhiteSpace(gameplaySceneName) ||
            !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError("MissionMapUI: gameplay scene is not available in Build Settings: " + gameplaySceneName);
            GameSession.ClearMissionSelection();
            return;
        }
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void GoBack()
    {
        if (navigator != null && navigator.TryGoBack())
            Refresh();
    }

    private void ClearEntries(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; --i)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void AddMapMarker(string label, MissionDefinition mission, int index,
        UnityEngine.Events.UnityAction action)
    {
        if (missionMapArea == null)
        {
            AddEntry(label, action);
            return;
        }
        Button button = Instantiate(entryButtonPrefab, missionMapArea);
        button.gameObject.SetActive(true);
        var layout = button.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        var rect = button.GetComponent<RectTransform>();
        Vector2 point = new Vector2(Mathf.Clamp01(mission.mapPosition.x),
            Mathf.Clamp01(mission.mapPosition.y));
        rect.anchorMin = point;
        rect.anchorMax = point;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = missionMarkerSprite != null ? missionMarkerSize : new Vector2(116f, 78f);
        Image markerImage = button.GetComponent<Image>();
        if (markerImage != null && missionMarkerSprite != null)
        {
            markerImage.sprite = missionMarkerSprite;
            markerImage.type = Image.Type.Simple;
            markerImage.preserveAspect = true;
            markerImage.color = Color.white;
        }
        int stars = MissionProgress.BestStars(mission, difficulty);
        if (stars == 0 && MissionProgress.IsCompleted(mission, difficulty)) stars = 1;
        stars = Mathf.Clamp(stars, 0, 3);
        string rating = stars == 0 ? "☆☆☆" : new string('★', stars) + new string('☆', 3 - stars);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            var labelObject = new GameObject("MissionLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(button.transform, false);
            text = labelObject.GetComponent<TextMeshProUGUI>();
        }
        text.text = label + "\n" + rating;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 21f;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        RectTransform labelRect = text.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -3f);
        labelRect.sizeDelta = new Vector2(130f, 60f);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void AddEntry(string label, UnityEngine.Events.UnityAction action, bool interactable = true)
    {
        Button button = Instantiate(entryButtonPrefab, entriesContainer);
        button.gameObject.SetActive(true);
        button.interactable = interactable;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            text.color = Color.black;
            text.raycastTarget = false;
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SetHeading(string text)
    {
        if (heading != null) heading.text = text;
    }

    private void SetImage(Sprite sprite)
    {
        if (mapIllustration == null) return;
        mapIllustration.sprite = sprite;
        mapIllustration.enabled = sprite != null;
    }
}
