using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [SerializeField] private MissionProgress.Difficulty difficulty = MissionProgress.Difficulty.Normal;

    public void ShowNormal() { difficulty = MissionProgress.Difficulty.Normal; Refresh(); }
    public void ShowMedium() { difficulty = MissionProgress.Difficulty.Medium; Refresh(); }

    private void OnEnable() => Refresh();

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
                SetImage(regionSelected != null ? regionSelected.mapIllustration : null);
                if (regionSelected != null && regionSelected.subzones != null)
                    foreach (var subzone in regionSelected.subzones)
                        if (subzone != null)
                            AddEntry(subzone.displayName, () => { navigator.SelectSubzone(subzone); Refresh(); });
                break;

            case MissionMenuNavigator.Page.Subzone:
                var subzoneSelected = navigator.SelectedSubzone;
                SetHeading(subzoneSelected != null ? subzoneSelected.displayName : "");
                bool unlocked = MissionProgress.IsUnlocked(subzoneSelected, difficulty);
                if (!unlocked) SetHeading((subzoneSelected != null ? subzoneSelected.displayName : "") + " - Medium locked");
                SetImage(subzoneSelected != null ? subzoneSelected.illustration : null);
                if (subzoneSelected != null && subzoneSelected.missions != null)
                    foreach (var mission in subzoneSelected.missions)
                        if (mission != null)
                            AddEntry((MissionProgress.IsCompleted(mission, difficulty) ? "✓ " : "") + mission.displayName,
                                () => { if (MissionProgress.IsUnlocked(navigator.SelectedSubzone, difficulty)) { navigator.SelectMission(mission); Refresh(); } }, unlocked);
                break;

            case MissionMenuNavigator.Page.Mission:
                var selected = navigator.SelectedMission;
                SetHeading(selected != null ? selected.displayName : "");
                SetImage(selected != null ? selected.illustration : null);
                break;
        }
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
