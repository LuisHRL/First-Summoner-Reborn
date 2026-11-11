using UnityEngine;

/// <summary>Runtime visual settings passed from the mission menu to the results screen.</summary>
public static class MissionVisualStyle
{
    public static Sprite EarnedStar { get; private set; }
    public static Sprite GreyStar { get; private set; }
    public static Sprite ButtonBackground { get; private set; }

    public static void Set(Sprite earnedStar, Sprite greyStar, Sprite buttonBackground)
    {
        EarnedStar = earnedStar;
        GreyStar = greyStar;
        ButtonBackground = buttonBackground;
    }
}
