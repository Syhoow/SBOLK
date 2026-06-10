using Godot;
using System;
using System.Collections.Generic;

public partial class PriceChart : Control
{
    private const int MaxRecords = 20;

    private List<float> _prices = new();
    private Vector2[] _points = Array.Empty<Vector2>();
    private int _hoverIndex = -1;

    private static readonly Color BackgroundColor  = new Color(0.05f, 0.05f, 0.1f, 1f);
    private static readonly Color AxisColor        = new Color(0.5f,  0.5f,  0.6f, 1f);
    private static readonly Color LineColor        = new Color(0.2f,  0.9f,  0.4f, 1f);
    private static readonly Color DotColor         = new Color(0.9f,  0.9f,  0.2f, 1f);
    private static readonly Color HoverDotColor    = new Color(1.0f,  0.4f,  0.2f, 1f);
    private static readonly Color AvgLineColor     = new Color(0.4f,  0.6f,  1.0f, 0.7f);
    private static readonly Color TooltipBg        = new Color(0.1f,  0.1f,  0.2f, 0.92f);
    private static readonly Color TooltipTextColor      = new Color(1.0f,  1.0f,  1.0f, 1f);
    private static readonly Color NoDataColor      = new Color(0.4f,  0.4f,  0.5f, 1f);

    private const float Padding     = 14f;
    private const float HoverRadius = 12f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetProcessInput(false);
    }

    public void SetPrices(List<float> prices)
    {
        if (prices == null || prices.Count == 0)
        {
            _prices = new List<float>();
        }
        else
        {
            var deduped = new List<float>();
            foreach (var p in prices)
            {
                if (deduped.Count == 0 || !Mathf.IsEqualApprox(p, deduped[deduped.Count - 1]))
                    deduped.Add(p);
            }

            int start = Mathf.Max(0, deduped.Count - MaxRecords);
            _prices = deduped.GetRange(start, deduped.Count - start);
        }
        _hoverIndex = -1;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
        {
            int nearest = FindNearestPoint(mm.Position);
            if (nearest != _hoverIndex)
            {
                _hoverIndex = nearest;
                QueueRedraw();
            }
        }
        else if (@event is InputEventMouseButton)
        {
        }
    }

    private int FindNearestPoint(Vector2 mousePos)
    {
        if (_points == null || _points.Length == 0) return -1;
        float bestDist = HoverRadius;
        int bestIdx = -1;
        for (int i = 0; i < _points.Length; i++)
        {
            float d = mousePos.DistanceTo(_points[i]);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        return bestIdx;
    }

    public override void _Draw()
    {
        var rect   = GetRect();
        float w    = rect.Size.X;
        float h    = rect.Size.Y;

        DrawRect(new Rect2(Vector2.Zero, rect.Size), BackgroundColor);

        float left    = Padding + 20f;
        float right   = w - Padding;
        float top     = Padding;
        float bottom  = h - Padding - 12f;
        float chartW  = right  - left;
        float chartH  = bottom - top;

        DrawLine(new Vector2(left, bottom), new Vector2(right, bottom), AxisColor, 1f);
        DrawLine(new Vector2(left, top),    new Vector2(left, bottom),  AxisColor, 1f);

        var font = ThemeDB.FallbackFont;

        if (_prices == null || _prices.Count == 0)
        {
            if (font != null)
                DrawString(font, new Vector2(left + chartW * 0.5f - 24f, top + chartH * 0.5f + 4f),
                    "No trades yet", HorizontalAlignment.Left, -1, 10, NoDataColor);
            _points = Array.Empty<Vector2>();
            return;
        }

        float minPrice = float.MaxValue;
        float maxPrice = float.MinValue;
        foreach (var p in _prices)
        {
            if (p < minPrice) minPrice = p;
            if (p > maxPrice) maxPrice = p;
        }

        if (Mathf.IsEqualApprox(minPrice, maxPrice)) { minPrice -= 1f; maxPrice += 1f; }
        float priceRange = maxPrice - minPrice;

        _points = new Vector2[_prices.Count];
        for (int i = 0; i < _prices.Count; i++)
        {
            float xRatio = _prices.Count > 1 ? (float)i / (_prices.Count - 1) : 0.5f;
            float yRatio = 1f - (_prices[i] - minPrice) / priceRange;
            _points[i] = new Vector2(left + xRatio * chartW, top + yRatio * chartH);
        }

        float sum = 0f;
        foreach (var p in _prices) sum += p;
        float avg = sum / _prices.Count;
        float avgYRatio = 1f - (avg - minPrice) / priceRange;
        float avgY = top + avgYRatio * chartH;

        float dashLen = 5f;
        float gapLen  = 4f;
        float x = left;
        while (x < right)
        {
            float xEnd = Mathf.Min(x + dashLen, right);
            DrawLine(new Vector2(x, avgY), new Vector2(xEnd, avgY), AvgLineColor, 1f);
            x += dashLen + gapLen;
        }

        if (font != null)
            DrawString(font, new Vector2(right + 2f, avgY + 4f),
                "avg", HorizontalAlignment.Left, -1, 8, AvgLineColor);

        DrawPolyline(_points, LineColor, 1.5f, true);

        for (int i = 0; i < _points.Length; i++)
        {
            bool isHover = i == _hoverIndex;
            DrawCircle(_points[i], isHover ? 5f : 2.5f, isHover ? HoverDotColor : DotColor);
        }
        if (font != null)
        {
            DrawString(font, new Vector2(2f, top + 8f),
                ((int)maxPrice).ToString(), HorizontalAlignment.Left, -1, 9, AxisColor);
            DrawString(font, new Vector2(2f, bottom - 1f),
                ((int)minPrice).ToString(), HorizontalAlignment.Left, -1, 9, AxisColor);
            DrawString(font, new Vector2(2f, avgY + 4f),
                ((int)avg).ToString(), HorizontalAlignment.Left, -1, 9, AvgLineColor);

            DrawString(font, new Vector2(left, bottom + 10f),
                $"({_prices.Count} trades)", HorizontalAlignment.Left, -1, 8, AxisColor);
        }

        if (_hoverIndex >= 0 && _hoverIndex < _prices.Count && font != null)
        {
            var pt      = _points[_hoverIndex];
            string text = $"{(int)_prices[_hoverIndex]}c  #{_hoverIndex + 1}";

            float textW = font.GetStringSize(text, HorizontalAlignment.Left, -1, 11).X + 8f;
            float textH = 18f;
            float tx    = Mathf.Clamp(pt.X - textW * 0.5f, left, right - textW);
            float ty    = pt.Y - textH - 6f;
            if (ty < top) ty = pt.Y + 8f;

            DrawRect(new Rect2(tx - 2f, ty - 2f, textW + 4f, textH + 2f), TooltipBg);
            DrawString(font, new Vector2(tx + 2f, ty + 12f),
                text, HorizontalAlignment.Left, -1, 11, TooltipTextColor);
        }
    }
}
