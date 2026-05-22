using Godot;
using System.Collections.Generic;

public partial class PriceChart : Control
{
    private List<float> _prices = new();

    private static readonly Color BackgroundColor = new Color(0.05f, 0.05f, 0.1f, 1f);
    private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.6f, 1f);
    private static readonly Color LineColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    private static readonly Color DotColor = new Color(0.9f, 0.9f, 0.2f, 1f);
    private static readonly Color NoDataColor = new Color(0.4f, 0.4f, 0.5f, 1f);

    private const float Padding = 14f;

    public void SetPrices(List<float> prices)
    {
        _prices = prices ?? new List<float>();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = GetRect();
        float w = rect.Size.X;
        float h = rect.Size.Y;

        DrawRect(new Rect2(Vector2.Zero, rect.Size), BackgroundColor);

        float left = Padding;
        float right = w - Padding;
        float top = Padding;
        float bottom = h - Padding;
        float chartW = right - left;
        float chartH = bottom - top;

        DrawLine(new Vector2(left, bottom), new Vector2(right, bottom), AxisColor, 1f);
        DrawLine(new Vector2(left, top), new Vector2(left, bottom), AxisColor, 1f);

        if (_prices == null || _prices.Count == 0)
        {
            var font = ThemeDB.FallbackFont;
            if (font != null)
                DrawString(font, new Vector2(left + chartW * 0.5f - 24f, top + chartH * 0.5f + 4f),
                    "No trades", HorizontalAlignment.Left, -1, 10, NoDataColor);
            return;
        }

        float minPrice = float.MaxValue;
        float maxPrice = float.MinValue;
        foreach (var p in _prices)
        {
            if (p < minPrice) minPrice = p;
            if (p > maxPrice) maxPrice = p;
        }

        if (Mathf.IsEqualApprox(minPrice, maxPrice))
        {
            minPrice -= 1f;
            maxPrice += 1f;
        }

        float priceRange = maxPrice - minPrice;

        var points = new Vector2[_prices.Count];
        for (int i = 0; i < _prices.Count; i++)
        {
            float xRatio = _prices.Count > 1 ? (float)i / (_prices.Count - 1) : 0.5f;
            float yRatio = 1f - (_prices[i] - minPrice) / priceRange;
            points[i] = new Vector2(left + xRatio * chartW, top + yRatio * chartH);
        }

        DrawPolyline(points, LineColor, 1.5f, true);

        foreach (var pt in points)
            DrawCircle(pt, 2.5f, DotColor);

        var fallbackFont = ThemeDB.FallbackFont;
        if (fallbackFont != null)
        {
            DrawString(fallbackFont, new Vector2(left + 1f, top + 8f),
                ((int)maxPrice).ToString(), HorizontalAlignment.Left, -1, 9, AxisColor);
            DrawString(fallbackFont, new Vector2(left + 1f, bottom - 1f),
                ((int)minPrice).ToString(), HorizontalAlignment.Left, -1, 9, AxisColor);
        }
    }
}
