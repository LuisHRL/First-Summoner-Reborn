using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared sprite-based mission rating for map markers and results.</summary>
public static class MissionStarsUI
{
    public static void Create(Transform parent, int earned, Sprite earnedSprite,
        Sprite greySprite, Vector2 starSize, Vector2 offset)
    {
        earned = Mathf.Clamp(earned, 0, 3);
        float spacing = starSize.x * 1.12f;
        for (int i = 0; i < 3; i++)
        {
            var star = new GameObject("Star_" + (i + 1), typeof(RectTransform), typeof(Image));
            star.transform.SetParent(parent, false);
            var rect = star.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = starSize;
            rect.anchoredPosition = offset + new Vector2((i - 1) * spacing, 0f);
            var image = star.GetComponent<Image>();
            bool achieved = i < earned;
            image.sprite = achieved ? earnedSprite : greySprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            // Visible fallback if the optional sprites have not been assigned.
            image.color = image.sprite == null
                ? (achieved ? new Color(1f, 0.76f, 0.1f) : new Color(0.38f, 0.38f, 0.38f))
                : Color.white;
        }
    }
}
