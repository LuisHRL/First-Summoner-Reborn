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

    [Header("Responsive screen alignment")]
    [Tooltip("Keep the mana icon group centered on the existing fill left edge without moving the mana bar or frame.")]
    public bool keepAlignedAtRuntime = true;

    private void LateUpdate()
    {
        if (!Application.isPlaying || !keepAlignedAtRuntime ||
            !startAtIconCenter || manaIcon == null) return;

        // Follow the already-laid-out mana fill. Only the icon parent moves;
        // its light, fill and folded visuals keep their existing local offsets.
        if (fill != null) AlignIconToFill(manaIcon, fill.rectTransform);
    }

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
                AlignIconToFill(manaIcon, fill.rectTransform);
        }
        if (applyThreePartFrame && threePartFrame != null)
        {
            if (originalFrameSprite != null) threePartFrame.SourceSprite = originalFrameSprite;
            threePartFrame.sourceLeftPixels = sourceLeftPixels;
            threePartFrame.sourceRightPixels = sourceRightPixels;
            threePartFrame.displayedLeftWidth = displayedLeftWidth;
            threePartFrame.displayedRightWidth = displayedRightWidth;
            SetRect(threePartFrame.rectTransform, frameSize, frameOffset);
            threePartFrame.SetAllDirty();
        }
        if (applyFrameLayout && frame != null)
        {
            SetRect(frame.rectTransform, frameSize, frameOffset);
            // Slicing is valid only for sprites with configured borders.
            bool hasBorders = frame.sprite != null && frame.sprite.border.sqrMagnitude > 0f;
            frame.type = useSlicedFrame && hasBorders ? Image.Type.Sliced : Image.Type.Simple;
        }
    }

    private static void AlignIconToFill(RectTransform icon, RectTransform manaFill)
    {
        var parent = icon.parent as RectTransform;
        if (parent == null) return;

        // Compare actual world positions, even with different UI parents,
        // Canvas scaling, anchors and screen aspect ratios.
        Vector3 iconCenterWorld = icon.TransformPoint(icon.rect.center);
        Vector3 fillLeftWorld = manaFill.TransformPoint(
            new Vector3(manaFill.rect.xMin, manaFill.rect.center.y, 0f));
        Vector3 desired = parent.InverseTransformPoint(fillLeftWorld);
        Vector3 current = parent.InverseTransformPoint(iconCenterWorld);
        float deltaX = desired.x - current.x;
        if (Mathf.Abs(deltaX) < 0.001f) return;

        // Horizontal alignment only: preserve height, size, rotation, FX and animation.
        icon.anchoredPosition += new Vector2(deltaX, 0f);
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        rect.anchoredPosition = offset;
    }
}
