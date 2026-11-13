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
    public bool flashRemainingHealthOnDamage;
    public bool persistentLightBelowFullHealth;

    private float displayed;
    private float target;
    private float from;
    private float elapsed;
    private bool initialized;
    private bool animating;
    private float initialLightAlpha = 1f;
    private bool lightAlphaCaptured;
    private float hitFlashElapsed;
    private bool hitFlashActive;
    private float hitFlashInitialAlpha = 1f;

    public void SetValue(float normalized, bool immediate = false)
    {
        normalized = Mathf.Clamp01(normalized);
        if (!initialized || immediate)
        {
            initialized = true;
            displayed = target = from = normalized;
            animating = false;
            if (fill != null) fill.fillAmount = normalized;
            if (delayedFill != null) { delayedFill.fillAmount = normalized; delayedFill.enabled = false; }
            hitFlashActive = false;
            if (persistentLightBelowFullHealth) UpdatePersistentLight(normalized);
            else HideLight();
            return;
        }
        if (Mathf.Approximately(target, normalized)) return;
        if (flashRemainingHealthOnDamage)
        {
            bool damage = normalized < target;
            target = displayed = from = normalized;
            animating = false;
            if (fill != null) fill.fillAmount = normalized;
            if (persistentLightBelowFullHealth) UpdatePersistentLight(normalized);
            else HideLight();
            if (delayedFill != null)
            {
                delayedFill.fillAmount = normalized;
                delayedFill.enabled = damage;
                if (damage)
                {
                    hitFlashInitialAlpha = 1f;
                    var c = delayedFill.color;
                    c.a = hitFlashInitialAlpha;
                    delayedFill.color = c;
                }
            }
            hitFlashActive = damage && delayedFill != null;
            hitFlashElapsed = 0f;
            return;
        }
        bool animate = normalized > target ? animateIncreases : animateDecreases;
        bool takingDamage = normalized < displayed;
        if (delayedFill != null)
        {
            delayedFill.enabled = takingDamage && animate;
            delayedFill.fillAmount = displayed;
        }
        target = normalized;
        from = displayed;
        elapsed = 0f;
        animating = animate;
        if (!animate) SetValue(normalized, true);
    }

    private void OnDisable() { HideLight(); hitFlashActive = false; if (delayedFill != null && flashRemainingHealthOnDamage) delayedFill.enabled = false; }

    private void Update()
    {
        if (flashRemainingHealthOnDamage)
        {
            if (!hitFlashActive) return;
            hitFlashElapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(hitFlashElapsed / Mathf.Max(0.01f, duration));
            if (delayedFill != null)
            {
                var c = delayedFill.color;
                c.a = hitFlashInitialAlpha * fade;
                delayedFill.color = c;
            }
            if (transitionLight != null && !persistentLightBelowFullHealth)
            {
                var c = transitionLight.color;
                c.a = 0.9f * fade;
                transitionLight.color = c;
            }
            if (fade <= 0f) { hitFlashActive = false; if (delayedFill != null) delayedFill.enabled = false; if (!persistentLightBelowFullHealth) HideLight(); }
            return;
        }
        if (!animating) return;
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        displayed = Mathf.Lerp(from, target, t);
        if (fill != null) fill.fillAmount = displayed;
        if (delayedFill != null && delayedFill.enabled)
        {
            // Bright hit overlay stays at the previous HP level, then retracts to the new level.
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
            PositionLight(displayed);
            Color color = transitionLight.color;
            color.a = initialLightAlpha * Mathf.Clamp01((1f - t) * duration / lightFadeDuration);
            transitionLight.color = color;
        }
        if (t >= 1f)
        {
            animating = false;
            if (fill != null) fill.fillAmount = target;
            if (delayedFill != null) { delayedFill.fillAmount = target; delayedFill.enabled = false; }
            HideLight();
        }
    }

    private void UpdatePersistentLight(float value)
    {
        if (transitionLight == null) return;
        bool visible = value > 0f && value < 1f;
        if (!visible) { HideLight(); return; }
        PositionLight(value);
        var color = transitionLight.color;
        color.a = 0.9f;
        transitionLight.color = color;
        transitionLight.gameObject.SetActive(true);
    }

    private void PositionLight(float value)
    {
        if (fill == null || transitionLight == null) return;
        RectTransform fillRect = fill.rectTransform;
        RectTransform lightRect = transitionLight.rectTransform;
        RectTransform container = fillRect.parent as RectTransform;
        if (container != null && lightRect.parent == container)
        {
            bool vertical = fill.fillMethod == Image.FillMethod.Vertical;
            Vector2 anchor = vertical ? new Vector2(0.5f, value) : new Vector2(value, 0.5f);
            lightRect.anchorMin = anchor;
            lightRect.anchorMax = anchor;
            lightRect.anchoredPosition = Vector2.zero;
        }
        lightRect.localRotation = Quaternion.Euler(0f, 0f, horizontalLight ? 90f : 0f);
    }

    private void HideLight()
    {
        if (transitionLight != null)
            transitionLight.gameObject.SetActive(false);
    }
}
