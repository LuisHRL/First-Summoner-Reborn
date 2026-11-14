using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable visuals for a world-space HP bar. Does not change health or visibility.</summary>
public sealed class WorldHealthVisualLayout : MonoBehaviour
{
    public Image healthFill;
    public AdjustableThreeSlice frame;
    public Image oldBackground;
    public Sprite originalFrameSprite;
    public Sprite originalFillSprite;
    public Vector2 frameSize = new Vector2(100f, 24f);
    public Vector2 frameOffset;
    public Vector2 fillSize = new Vector2(90f, 14f);
    public Vector2 fillOffset;
    [Min(0)] public float sourceLeftPixels = 12f;
    [Min(0)] public float sourceRightPixels = 12f;
    [Min(0)] public float displayedLeftWidth = 12f;
    [Min(0)] public float displayedRightWidth = 12f;
    public bool hideOldBackground = true;

    [ContextMenu("Apply World HP Visual Layout")]
    public void Apply()
    {
        if (frame != null)
        {
            if (originalFrameSprite != null) frame.SourceSprite = originalFrameSprite;
            frame.sourceLeftPixels = sourceLeftPixels;
            frame.sourceRightPixels = sourceRightPixels;
            frame.displayedLeftWidth = displayedLeftWidth;
            frame.displayedRightWidth = displayedRightWidth;
            SetRect(frame.rectTransform, frameSize, frameOffset);
            frame.SetAllDirty();
        }
        if (healthFill != null)
        {
            if (originalFillSprite != null) healthFill.sprite = originalFillSprite;
            SetRect(healthFill.rectTransform, fillSize, fillOffset);
        }
        if (oldBackground != null && oldBackground != healthFill)
            oldBackground.enabled = !hideOldBackground;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        rect.anchoredPosition = offset;
    }
}
