using UnityEngine;
using UnityEngine.UI;

/// <summary>Optional visual-only fill transition. All light effects are disabled when idle.</summary>
public class AnimatedFillEffect : MonoBehaviour
{
    [Header("Images (assign existing sprites in the Inspector)")]
    public Image fill;
    public Image delayedFill;
    public Image transitionLight;
    [Header("Animation")]
    [Min(0.01f)] public float duration = 0.3f;
    [Min(0.01f)] public float lightFadeDuration = 0.15f;
    public bool horizontalLight;
    public bool animateDecreases = true;
    public bool animateIncreases = true;

    private float displayed;
    private float target;
    private float from;
    private float elapsed;
    private bool initialized;
    private bool animating;
    private float initialLightAlpha = 1f;
    private bool lightAlphaCaptured;

    public void SetValue(float normalized, bool immediate = false)
    {
        normalized = Mathf.Clamp01(normalized);
        if (!initialized || immediate)
        {
            initialized = true;
            displayed = target = from = normalized;
            animating = false;
            if (fill != null) fill.fillAmount = normalized;
            if (delayedFill != null) delayedFill.fillAmount = normalized;
            HideLight();
            return;
        }
        if (Mathf.Approximately(target, normalized)) return;
        bool animate = normalized > target ? animateIncreases : animateDecreases;
        target = normalized;
        from = displayed;
        elapsed = 0f;
        animating = animate;
        if (!animate) SetValue(normalized, true);
    }

    private void OnDisable() { HideLight(); }

    private void Update()
    {
        if (!animating) return;
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        displayed = Mathf.Lerp(from, target, t);
        if (fill != null) fill.fillAmount = displayed;
        if (delayedFill != null)
        {
            // A delayed hit layer remains behind the main HP bar on damage.
            delayedFill.fillAmount = Mathf.Lerp(from, target, Mathf.Clamp01((t - 0.2f) / 0.8f));
        }
        if (transitionLight != null)
        {
            if (!lightAlphaCaptured)
            {
                initialLightAlpha = transitionLight.color.a;
                lightAlphaCaptured = true;
            }
            transitionLight.gameObject.SetActive(true);
            RectTransform lightRect = transitionLight.rectTransform;
            if (fill != null)
            {
                RectTransform fillRect = fill.rectTransform;
                RectTransform container = fillRect.parent as RectTransform;
                if (container != null && lightRect.parent == container)
                {
                    bool vertical = fill.fillMethod == Image.FillMethod.Vertical;
                    Vector2 min = vertical ? new Vector2(0.5f, displayed) : new Vector2(displayed, 0.5f);
                    lightRect.anchorMin = min;
                    lightRect.anchorMax = min;
                    lightRect.anchoredPosition = Vector2.zero;
                }
            }
            lightRect.localRotation = Quaternion.Euler(0f, 0f, horizontalLight ? 90f : 0f);
            Color color = transitionLight.color;
            color.a = initialLightAlpha * Mathf.Clamp01((1f - t) * duration / lightFadeDuration);
            transitionLight.color = color;
        }
        if (t >= 1f)
        {
            animating = false;
            if (fill != null) fill.fillAmount = target;
            if (delayedFill != null) delayedFill.fillAmount = target;
            HideLight();
        }
    }

    private void HideLight()
    {
        if (transitionLight != null)
            transitionLight.gameObject.SetActive(false);
    }
}
