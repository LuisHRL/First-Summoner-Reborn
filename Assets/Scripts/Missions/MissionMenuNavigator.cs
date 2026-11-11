using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Data-driven navigation state for a mission menu. Bind the events to UI
/// refresh methods in the Inspector; this class does not instantiate UI.
/// </summary>
public sealed class MissionMenuNavigator : MonoBehaviour
{
    public enum Page { World, Region, Mission }

    [SerializeField] private MissionWorldDefinition world;
    [SerializeField] private UnityEvent onSelectionChanged;

    public event Action SelectionChanged;

    public Page CurrentPage { get; private set; } = Page.World;
    public MissionRegionDefinition SelectedRegion { get; private set; }
    public MissionDefinition SelectedMission { get; private set; }

    public MissionWorldDefinition World => world;

    public void ShowWorld()
    {
        CurrentPage = Page.World;
        SelectedRegion = null;
        SelectedMission = null;
        NotifyChanged();
    }

    public bool SelectRegion(MissionRegionDefinition region)
    {
        if (world == null || region == null || Array.IndexOf(world.regions, region) < 0)
            return false;
        SelectedRegion = region;
        SelectedMission = null;
        CurrentPage = Page.Region;
        NotifyChanged();
        return true;
    }

    public bool SelectMission(MissionDefinition mission)
    {
        if (SelectedRegion == null || mission == null ||
            Array.IndexOf(SelectedRegion.missions, mission) < 0)
            return false;
        SelectedMission = mission;
        CurrentPage = Page.Mission;
        NotifyChanged();
        return true;
    }

    /// <returns>True when a nested mission page handled Back.</returns>
    public bool TryGoBack()
    {
        switch (CurrentPage)
        {
            case Page.Mission:
                SelectedMission = null;
                CurrentPage = Page.Region;
                break;
            case Page.Region:
                ShowWorld();
                return true;
            default:
                return false;
        }
        NotifyChanged();
        return true;
    }

    private void OnEnable() => ShowWorld();

    private void NotifyChanged()
    {
        onSelectionChanged?.Invoke();
        SelectionChanged?.Invoke();
    }
}
