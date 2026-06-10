using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a series of connected line segments inside a RectTransform using Unity's
/// UI mesh system.  Positions are in the local space of this RectTransform.
///
/// Usage:
///   var line = go.AddComponent<UILineRenderer>();
///   line.SetPoints(new List<Vector2> { start, end });
///   line.lineWidth = 3f;
///   line.color = Color.white;
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UILineRenderer : Graphic
{
    [SerializeField] private float _lineWidth = 3f;
    [SerializeField] private Color _lineColor = Color.white;

    private List<Vector2> _points = new List<Vector2>();

    public float lineWidth
    {
        get => _lineWidth;
        set { _lineWidth = value; SetVerticesDirty(); }
    }

    public override Color color
    {
        get => _lineColor;
        set { _lineColor = value; SetVerticesDirty(); }
    }

    public void SetPoints(List<Vector2> points)
    {
        _points = points ?? new List<Vector2>();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_points == null || _points.Count < 2) return;

        var c = _lineColor;
        float hw = _lineWidth * 0.5f;

        for (int i = 0; i < _points.Count - 1; i++)
        {
            Vector2 a = _points[i];
            Vector2 b = _points[i + 1];

            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * hw;

            int baseIdx = vh.currentVertCount;

            // 4 corners of the quad
            vh.AddVert(new Vector3(a.x - perp.x, a.y - perp.y, 0), c, Vector2.zero);
            vh.AddVert(new Vector3(a.x + perp.x, a.y + perp.y, 0), c, Vector2.up);
            vh.AddVert(new Vector3(b.x + perp.x, b.y + perp.y, 0), c, new Vector2(1, 1));
            vh.AddVert(new Vector3(b.x - perp.x, b.y - perp.y, 0), c, Vector2.right);

            vh.AddTriangle(baseIdx,     baseIdx + 1, baseIdx + 2);
            vh.AddTriangle(baseIdx,     baseIdx + 2, baseIdx + 3);
        }
    }
}
