using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Data-driven navigation state for a mission menu. Bind the events to UI
/// refresh methods in the Inspector; this class does not instantiate UI.
/// </summary>
public sealed class MissionMenuNavigator : MonoBehaviour
{
    public enum Page { World, Region, Subzone, Mission }

    [SerializeField] private MissionWorldDefinition world;
    [SerializeField] private UnityEvent onSelectionChanged;

    public Page CurrentPage { get; private set; } = Page.World;
    public MissionRegionDefinition SelectedRegion { get; private set; }
    public MissionSubzoneDefinition SelectedSubzone { get; private set; }
    public MissionDefinition SelectedMission { get; private set; }

    public MissionWorldDefinition World => world;

    public void ShowWorld()
    {
        CurrentPage = Page.World;
        SelectedRegion = null;
        SelectedSubzone = null;
        SelectedMission = null;
        NotifyChanged();
    }

    public bool SelectRegion(MissionRegionDefinition region)
    {
        if (world == null || region == null || Array.IndexOf(world.regions, region) < 0)
            return false;
        SelectedRegion = region;
        SelectedSubzone = null;
        SelectedMission = null;
        CurrentPage = Page.Region;
        NotifyChanged();
        return true;
    }

    public bool SelectSubzone(MissionSubzoneDefinition subzone)
    {
        if (SelectedRegion == null || subzone == null ||
            Array.IndexOf(SelectedRegion.subzones, subzone) < 0)
            return false;
        SelectedSubzone = subzone;
        SelectedMission = null;
        CurrentPage = Page.Subzone;
        NotifyChanged();
        return true;
    }

    public bool SelectMission(MissionDefinition mission)
    {
        if (SelectedSubzone == null || mission == null ||
            Array.IndexOf(SelectedSubzone.missions, mission) < 0)
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
                CurrentPage = Page.Subzone;
                break;
            case Page.Subzone:
                SelectedSubzone = null;
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

    private void NotifyChanged() => onSelectionChanged?.Invoke();
}
