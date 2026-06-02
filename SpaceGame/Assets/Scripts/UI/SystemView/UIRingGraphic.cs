using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a thin circular ring (orbit indicator) centred on the RectTransform.
/// The outer radius is half the RectTransform's width (sizeDelta.x should equal
/// sizeDelta.y — keep it square). Colour, thickness, and segment count are
/// configurable at runtime via the public properties.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIRingGraphic : MaskableGraphic
{
    [SerializeField] private float _thickness = 1.5f;
    [SerializeField] private int   _segments  = 80;

    /// <summary>Ring stroke width in canvas units.</summary>
    public float Thickness
    {
        get => _thickness;
        set { _thickness = value; SetVerticesDirty(); }
    }

    /// <summary>Number of quad segments around the ring. Higher = smoother.</summary>
    public int Segments
    {
        get => _segments;
        set { _segments = Mathf.Max(3, value); SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // Outer radius = half the shorter side of the rect
        Rect  r       = rectTransform.rect;
        float outerR  = Mathf.Min(r.width, r.height) * 0.5f;
        float innerR  = Mathf.Max(0f, outerR - _thickness);
        float da      = 2f * Mathf.PI / _segments;

        for (int i = 0; i < _segments; i++)
        {
            float a0 = da * i;
            float a1 = da * (i + 1);

            float cos0 = Mathf.Cos(a0), sin0 = Mathf.Sin(a0);
            float cos1 = Mathf.Cos(a1), sin1 = Mathf.Sin(a1);

            // Four corners of this arc segment (outer two, then inner two)
            var v0 = new Vector3(outerR * cos0, outerR * sin0, 0f);
            var v1 = new Vector3(outerR * cos1, outerR * sin1, 0f);
            var v2 = new Vector3(innerR * cos1, innerR * sin1, 0f);
            var v3 = new Vector3(innerR * cos0, innerR * sin0, 0f);

            int b = i * 4;
            vh.AddVert(v0, color, Vector2.zero);
            vh.AddVert(v1, color, Vector2.zero);
            vh.AddVert(v2, color, Vector2.zero);
            vh.AddVert(v3, color, Vector2.zero);

            vh.AddTriangle(b,     b + 1, b + 2);
            vh.AddTriangle(b,     b + 2, b + 3);
        }
    }
}
