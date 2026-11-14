using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable sprite layout for the mana bar. Does not alter Slider values or animations.</summary>
[ExecuteAlways]
public sealed class ManaBarVisualLayout : MonoBehaviour
{
    public Image fill;
    public Image frame;
    public Sprite originalManaSprite;
    public Vector2 fillSize = new Vector2(300f, 28f);
    public Vector2 fillOffset;
    public Vector2 frameSize = new Vector2(320f, 34f);
    public Vector2 frameOffset;
    public bool useOriginalManaSprite = true;
    public bool applyFrameLayout;
    public bool useSlicedFrame;
    [Header("Original mana alignment")]
    public RectTransform manaIcon;
    public bool startAtIconCenter = true;
    public bool hideLegacyBackground = true;
    public Image legacyBackground;
    [Header("Optional original frame with adjustable left / center / right")]
    public AdjustableThreeSlice threePartFrame;
    public bool applyThreePartFrame;
    public Sprite originalFrameSprite;
    [Min(0)] public float sourceLeftPixels = 12f;
    [Min(0)] public float sourceRightPixels = 12f;
    [Min(0)] public float displayedLeftWidth = 12f;
    [Min(0)] public float displayedRightWidth = 12f;

    private void OnValidate()
    {
        // Never mutate another RectTransform during Unity's validation pass.
        // The Inspector can apply changes explicitly via the context menu.
    }

    [ContextMenu("Apply Mana Visual Layout")]
    public void Apply()
    {
        if (hideLegacyBackground && legacyBackground != null && legacyBackground != fill && legacyBackground != frame)
            legacyBackground.enabled = false;
        if (fill != null)
        {
            if (useOriginalManaSprite && originalManaSprite != null)
                fill.sprite = originalManaSprite;
            SetRect(fill.rectTransform, fillSize, fillOffset);
            if (startAtIconCenter && manaIcon != null)
                AlignLeftEdge(fill.rectTransform, manaIcon);
        }
        if (applyThreePartFrame && threePartFrame != null)
        {
            if (originalFrameSprite != null) threePartFrame.SourceSprite = originalFrameSprite;
            threePartFrame.sourceLeftPixels = sourceLeftPixels;
            threePartFrame.sourceRightPixels = sourceRightPixels;
            threePartFrame.displayedLeftWidth = displayedLeftWidth;
            threePartFrame.displayedRightWidth = displayedRightWidth;
            SetRect(threePartFrame.rectTransform, frameSize, frameOffset);
            if (startAtIconCenter && manaIcon != null)
                AlignLeftEdge(threePartFrame.rectTransform, manaIcon);
            threePartFrame.SetAllDirty();
        }
        if (applyFrameLayout && frame != null)
        {
            SetRect(frame.rectTransform, frameSize, frameOffset);
            if (startAtIconCenter && manaIcon != null)
                AlignLeftEdge(frame.rectTransform, manaIcon);
            // Slicing is valid only for sprites with configured borders.
            bool hasBorders = frame.sprite != null && frame.sprite.border.sqrMagnitude > 0f;
            frame.type = useSlicedFrame && hasBorders ? Image.Type.Sliced : Image.Type.Simple;
        }
    }

    private static void AlignLeftEdge(RectTransform target, RectTransform icon)
    {
        // Transform the icon center into the target's parent space, so this
        // works even when the animated fill is nested under a Slider Fill Area.
        var parent = target.parent as RectTransform;
        if (parent == null) return;
        Vector3 local = parent.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
        Vector3 targetCenter = parent.InverseTransformPoint(target.position);
        Vector3 desired = targetCenter + new Vector3(local.x - (targetCenter.x - target.rect.width * 0.5f), local.y - targetCenter.y, 0f);
        target.position = parent.TransformPoint(desired);
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        rect.anchoredPosition = offset;
    }
}
