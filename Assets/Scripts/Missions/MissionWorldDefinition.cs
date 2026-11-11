using System;
using UnityEngine;

[CreateAssetMenu(menuName = "First Summoner/Missions/World", fileName = "MissionWorld")]
public sealed class MissionWorldDefinition : ScriptableObject
{
    public Sprite worldMap;
    public MissionRegionDefinition[] regions = Array.Empty<MissionRegionDefinition>();
}
