using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders the mission hierarchy from ScriptableObject definitions.
/// Attach to the Missions panel and assign the UI references in the Inspector.
/// No region, subzone or mission is hardcoded.
/// </summary>
public sealed class MissionMapUI : MonoBehaviour
{
    [SerializeField] private MissionMenuNavigator navigator;
    [SerializeField] private RectTransform entriesContainer;
    [SerializeField] private Button entryButtonPrefab;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private Image mapIllustration;
    [SerializeField] private Button backButton;
    [Header("Mission launch")]
    [SerializeField] private Button launchButton;
    [SerializeField] private string gameplaySceneName = "GameScene";

    [SerializeField] private MissionProgress.Difficulty difficulty = MissionProgress.Difficulty.Normal;

    public void ShowNormal() { difficulty = MissionProgress.Difficulty.Normal; Refresh(); }
    public void ShowMedium() { difficulty = MissionProgress.Difficulty.Medium; Refresh(); }

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

        for (int i = entriesContainer.childCount - 1; i >= 0; --i)
            Destroy(entriesContainer.GetChild(i).gameObject);

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
                            AddEntry(region.displayName, () => { navigator.SelectRegion(region); Refresh(); });
                break;

            case MissionMenuNavigator.Page.Region:
                var regionSelected = navigator.SelectedRegion;
                SetHeading(regionSelected != null ? regionSelected.displayName : "");
                bool unlocked = MissionProgress.IsUnlocked(navigator.World, difficulty);
                if (!unlocked) SetHeading((regionSelected != null ? regionSelected.displayName : "") +
                    " - Medium locked - complete all Normal world missions");
                SetImage(regionSelected != null ? regionSelected.mapIllustration : null);
                if (regionSelected != null && regionSelected.missions != null)
                    foreach (var mission in regionSelected.missions)
                        if (mission != null)
                        {
                            bool available = MissionProgress.IsMissionUnlocked(
                                navigator.World, regionSelected, mission, difficulty);
                            string label = (MissionProgress.IsCompleted(mission, difficulty) ? "✓ " :
                                available ? "" : "🔒 ") + mission.displayName;
                            AddEntry(label, () => navigator.SelectMission(mission), available);
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

    private void AddEntry(string label, UnityEngine.Events.UnityAction action, bool interactable = true)
    {
        Button button = Instantiate(entryButtonPrefab, entriesContainer);
        button.gameObject.SetActive(true);
        button.interactable = interactable;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
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
