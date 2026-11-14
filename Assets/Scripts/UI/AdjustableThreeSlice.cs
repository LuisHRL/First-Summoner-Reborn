using UnityEngine;
using UnityEngine.UI;

/// <summary>Three-section UI sprite renderer. Source cap widths and displayed cap widths are independent.
/// Uses sprite UVs directly, so atlas sub-sprites work without readable textures or generated assets.</summary>
[ExecuteAlways]
public sealed class AdjustableThreeSlice : MaskableGraphic
{
    [SerializeField] private Sprite sourceSprite;
    [Min(0)] public float sourceLeftPixels = 12f;
    [Min(0)] public float sourceRightPixels = 12f;
    [Min(0)] public float displayedLeftWidth = 12f;
    [Min(0)] public float displayedRightWidth = 12f;

    public Sprite SourceSprite
    {
        get => sourceSprite;
        set { if (sourceSprite == value) return; sourceSprite = value; SetAllDirty(); }
    }

    public override Texture mainTexture => sourceSprite != null && sourceSprite.texture != null
        ? sourceSprite.texture : s_WhiteTexture;

    protected override void OnValidate()
    {
        base.OnValidate();
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (sourceSprite == null) return;
        Rect rect = GetPixelAdjustedRect();
        Rect spriteRect = sourceSprite.rect;
        if (rect.width <= 0 || rect.height <= 0 || spriteRect.width <= 0 || spriteRect.height <= 0) return;

        float sourceLeft = Mathf.Clamp(sourceLeftPixels, 0, spriteRect.width);
        float sourceRight = Mathf.Clamp(sourceRightPixels, 0, spriteRect.width - sourceLeft);
        float destLeft = Mathf.Min(Mathf.Max(0, displayedLeftWidth), rect.width);
        float destRight = Mathf.Min(Mathf.Max(0, displayedRightWidth), rect.width - destLeft);

        // Sprite.uv accounts for sub-sprites packed into an atlas. This renderer requires
        // a rectangular, unrotated sprite (Unity's standard Multiple-sprite atlas import).
        Vector2[] uv = sourceSprite.uv;
        if (uv == null || uv.Length == 0) return;
        float uMin = float.PositiveInfinity, uMax = float.NegativeInfinity;
        float vMin = float.PositiveInfinity, vMax = float.NegativeInfinity;
        foreach (var p in uv)
        {
            uMin = Mathf.Min(uMin, p.x); uMax = Mathf.Max(uMax, p.x);
            vMin = Mathf.Min(vMin, p.y); vMax = Mathf.Max(vMax, p.y);
        }
        float[] x = { rect.xMin, rect.xMin + destLeft, rect.xMax - destRight, rect.xMax };
        float[] u = { uMin, Mathf.Lerp(uMin, uMax, sourceLeft / spriteRect.width),
            Mathf.Lerp(uMin, uMax, 1f - sourceRight / spriteRect.width), uMax };
        for (int i = 0; i < 3; i++)
        {
            int start = vh.currentVertCount;
            AddVert(vh, x[i], rect.yMin, u[i], vMin);
            AddVert(vh, x[i], rect.yMax, u[i], vMax);
            AddVert(vh, x[i + 1], rect.yMax, u[i + 1], vMax);
            AddVert(vh, x[i + 1], rect.yMin, u[i + 1], vMin);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    private void AddVert(VertexHelper vh, float x, float y, float u, float v)
    {
        var vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = new Vector3(x, y, 0);
        vertex.uv0 = new Vector2(u, v);
        vh.AddVert(vertex);
    }
}
