using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD-only damage sparks. Uses local UI coordinates, not screen pixels.
/// No enemy/world health bar dependencies. Attach to TargetHealthBar prefab.</summary>
public sealed class TargetHPDamageParticles : MonoBehaviour
{
    public Image fill;
    public Image transitionLight;
    public Sprite particleSprite;
    [Min(0)] public int particleCount = 6;
    [Min(0.01f)] public float lifetime = 0.34f;
    [Min(0)] public float travelDistance = 8f;
    [Min(0.1f)] public float particleSize = 2.2f;
    public Color particleColor = new Color(1f, 0.55f, 0.18f, 1f);
    [Min(0.1f)] public float lightWidth = 1.7f;
    [Min(0.1f)] public float lightHeight = 9f;

    private struct Spark { public RectTransform rect; public Image image; public Vector2 start; public Vector2 velocity; public float age; public float duration; }
    private readonly List<Spark> sparks = new List<Spark>();
    private RectTransform container;
    private float lastValue;
    private bool initialized;

    private void Awake()
    {
        container = fill != null ? fill.rectTransform.parent as RectTransform : null;
        // Set the local HUD flash dimensions, independently of the world bars.
        if (transitionLight != null)
        {
            transitionLight.rectTransform.sizeDelta = new Vector2(lightWidth, lightHeight);
        }
    }

    public void SetHealth(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        if (!initialized) { initialized = true; lastValue = normalized; return; }
        if (normalized < lastValue - 0.0001f) Emit(normalized);
        lastValue = normalized;
    }

    private void Emit(float normalized)
    {
        if (particleSprite == null || fill == null || container == null) return;
        RectTransform fillRect = fill.rectTransform;
        Rect r = fillRect.rect;
        float x = Mathf.Lerp(r.xMin, r.xMax, normalized);
        Vector3 world = fillRect.TransformPoint(new Vector3(x, r.center.y, 0f));
        Vector3 local = container.InverseTransformPoint(world);
        for (int i = 0; i < particleCount; ++i)
        {
            var go = new GameObject("HP Damage Spark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(container, false);
            go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float t = (i + 0.5f) / Mathf.Max(1, particleCount);
            float offsetY = Mathf.Sin(i * 2.39996f) * r.height * 0.34f;
            Vector2 center = new Vector2(
                (0.5f - container.pivot.x) * container.rect.width,
                (0.5f - container.pivot.y) * container.rect.height);
            Vector2 start = (Vector2)local - center + new Vector2(0f, offsetY);
            rect.anchoredPosition = start;
            float size = particleSize * Mathf.Lerp(0.65f, 1.15f, t);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = particleSprite;
            image.color = particleColor;
            image.raycastTarget = false;
            sparks.Add(new Spark { rect = rect, image = image, start = start,
                velocity = new Vector2(travelDistance * (0.65f + 0.7f * t),
                    Mathf.Sin(i * 1.87f) * r.height * 0.8f),
                age = 0f, duration = lifetime * Mathf.Lerp(0.75f, 1.2f, t) });
        }
    }

    private void Update()
    {
        for (int i = sparks.Count - 1; i >= 0; --i)
        {
            Spark spark = sparks[i];
            if (spark.rect == null) { sparks.RemoveAt(i); continue; }
            spark.age += Time.deltaTime;
            float t = Mathf.Clamp01(spark.age / spark.duration);
            spark.rect.anchoredPosition = spark.start + spark.velocity * t;
            Color c = particleColor;
            c.a *= (1f - t) * (1f - t);
            spark.image.color = c;
            if (t >= 1f) { Destroy(spark.rect.gameObject); sparks.RemoveAt(i); }
            else sparks[i] = spark;
        }
    }

    private void OnDisable()
    {
        foreach (var spark in sparks)
            if (spark.rect != null) Destroy(spark.rect.gameObject);
        sparks.Clear();
        initialized = false;
    }
}
