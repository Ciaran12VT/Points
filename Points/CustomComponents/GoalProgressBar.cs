using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Points.CustomComponents
{
    public sealed class GoalProgressBar : GraphicsView
    {
        // --- Values ---
        public static readonly BindableProperty MaxValueProperty =
            BindableProperty.Create(nameof(MaxValue), typeof(double), typeof(GoalProgressBar), 100.0,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty TotalValueProperty =
            BindableProperty.Create(nameof(TotalValue), typeof(double), typeof(GoalProgressBar), 0.0,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty CurrentValueProperty =
            BindableProperty.Create(nameof(CurrentValue), typeof(double?), typeof(GoalProgressBar), null,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty ExpectedValueProperty =
            BindableProperty.Create(nameof(ExpectedValue), typeof(double?), typeof(GoalProgressBar), null,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());


        // --- Appearance ---
        public static readonly BindableProperty ThicknessProperty =
            BindableProperty.Create(nameof(Thickness), typeof(float), typeof(GoalProgressBar), 14f,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty CornerRadiusProperty =
            BindableProperty.Create(nameof(CornerRadius), typeof(float), typeof(GoalProgressBar), 10f,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty TrackColorProperty =
            BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(GoalProgressBar), Colors.DimGray,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty TotalColorProperty =
            BindableProperty.Create(nameof(TotalColor), typeof(Color), typeof(GoalProgressBar), Colors.DodgerBlue,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty CurrentColorProperty =
            BindableProperty.Create(nameof(CurrentColor), typeof(Color), typeof(GoalProgressBar), Colors.LimeGreen,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty ExpectedLineColorProperty =
            BindableProperty.Create(nameof(ExpectedLineColor), typeof(Color), typeof(GoalProgressBar), Colors.White,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty ExpectedLineThicknessProperty =
            BindableProperty.Create(nameof(ExpectedLineThickness), typeof(float), typeof(GoalProgressBar), 2f,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        // --- Wavy edge tuning ---
        public static readonly BindableProperty WaveAmplitudeProperty =
            BindableProperty.Create(nameof(WaveAmplitude), typeof(float), typeof(GoalProgressBar), 5f,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty WaveWavelengthProperty =
            BindableProperty.Create(nameof(WaveWavelength), typeof(float), typeof(GoalProgressBar), 14f,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty ShowCurrentOverlayProperty =
            BindableProperty.Create(nameof(ShowCurrentOverlay), typeof(bool), typeof(GoalProgressBar), true,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        public static readonly BindableProperty ShowExpectedMarkerProperty =
            BindableProperty.Create(nameof(ShowExpectedMarker), typeof(bool), typeof(GoalProgressBar), true,
                propertyChanged: (_, __, ___) => ((GoalProgressBar)_).Invalidate());

        // --- Labels ---
        public static readonly BindableProperty ShowLabelsProperty =
            BindableProperty.Create(nameof(ShowLabels), typeof(bool), typeof(GoalProgressBar), true,
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelTextColorProperty =
            BindableProperty.Create(nameof(LabelTextColor), typeof(Color), typeof(GoalProgressBar), Colors.White,
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelBackgroundColorProperty =
            BindableProperty.Create(nameof(LabelBackgroundColor), typeof(Color), typeof(GoalProgressBar), Colors.Black.WithAlpha(0.65f),
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelFontSizeProperty =
            BindableProperty.Create(nameof(LabelFontSize), typeof(float), typeof(GoalProgressBar), 12f,
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelPaddingXProperty =
            BindableProperty.Create(nameof(LabelPaddingX), typeof(float), typeof(GoalProgressBar), 6f,
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelPaddingYProperty =
            BindableProperty.Create(nameof(LabelPaddingY), typeof(float), typeof(GoalProgressBar), 3f,
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelCornerRadiusProperty =
            BindableProperty.Create(nameof(LabelCornerRadius), typeof(float), typeof(GoalProgressBar), 8f,
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelOffsetProperty =
            BindableProperty.Create(nameof(LabelOffset), typeof(float), typeof(GoalProgressBar), 6f,
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public static readonly BindableProperty LabelFormatProperty =
            BindableProperty.Create(nameof(LabelFormat), typeof(string), typeof(GoalProgressBar), "0",
                propertyChanged: (b, o, n) => ((GoalProgressBar)b).Invalidate());

        public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
        public double TotalValue { get => (double)GetValue(TotalValueProperty); set => SetValue(TotalValueProperty, value); }
        public double? CurrentValue { get => (double?)GetValue(CurrentValueProperty); set => SetValue(CurrentValueProperty, value); }
        public double? ExpectedValue { get => (double?)GetValue(ExpectedValueProperty); set => SetValue(ExpectedValueProperty, value); }

        public float Thickness { get => (float)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
        public float CornerRadius { get => (float)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

        public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }
        public Color TotalColor { get => (Color)GetValue(TotalColorProperty); set => SetValue(TotalColorProperty, value); }
        public Color CurrentColor { get => (Color)GetValue(CurrentColorProperty); set => SetValue(CurrentColorProperty, value); }

        public Color ExpectedLineColor { get => (Color)GetValue(ExpectedLineColorProperty); set => SetValue(ExpectedLineColorProperty, value); }
        public float ExpectedLineThickness { get => (float)GetValue(ExpectedLineThicknessProperty); set => SetValue(ExpectedLineThicknessProperty, value); }

        public float WaveAmplitude { get => (float)GetValue(WaveAmplitudeProperty); set => SetValue(WaveAmplitudeProperty, value); }
        public float WaveWavelength { get => (float)GetValue(WaveWavelengthProperty); set => SetValue(WaveWavelengthProperty, value); }

        public bool ShowCurrentOverlay { get => (bool)GetValue(ShowCurrentOverlayProperty); set => SetValue(ShowCurrentOverlayProperty, value); }
        public bool ShowExpectedMarker { get => (bool)GetValue(ShowExpectedMarkerProperty); set => SetValue(ShowExpectedMarkerProperty, value); }

        public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
        public Color LabelTextColor { get => (Color)GetValue(LabelTextColorProperty); set => SetValue(LabelTextColorProperty, value); }
        public Color LabelBackgroundColor { get => (Color)GetValue(LabelBackgroundColorProperty); set => SetValue(LabelBackgroundColorProperty, value); }
        public float LabelFontSize { get => (float)GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
        public float LabelPaddingX { get => (float)GetValue(LabelPaddingXProperty); set => SetValue(LabelPaddingXProperty, value); }
        public float LabelPaddingY { get => (float)GetValue(LabelPaddingYProperty); set => SetValue(LabelPaddingYProperty, value); }
        public float LabelCornerRadius { get => (float)GetValue(LabelCornerRadiusProperty); set => SetValue(LabelCornerRadiusProperty, value); }
        public float LabelOffset { get => (float)GetValue(LabelOffsetProperty); set => SetValue(LabelOffsetProperty, value); }
        public string LabelFormat { get => (string)GetValue(LabelFormatProperty); set => SetValue(LabelFormatProperty, value); }

        public GoalProgressBar()
        {
            Drawable = new BarDrawable(this);
            HeightRequest = Thickness;
        }

        private sealed class BarDrawable : IDrawable
        {
            private readonly GoalProgressBar _b;
            public BarDrawable(GoalProgressBar b) => _b = b;

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                var max = _b.MaxValue <= 0 ? 1.0 : _b.MaxValue;

                // Bar rect (centered vertically)
                var h = _b.Thickness;
                var y = (dirtyRect.Height - h) / 2f;
                var barRect = new RectF(0, y, dirtyRect.Width, h);

                // Track
                canvas.FillColor = _b.TrackColor;

                canvas.FillRoundedRectangle(barRect, _b.CornerRadius);

                // Total fill
                var totalT = Clamp01(_b.TotalValue / max);
                var totalW = barRect.Width * totalT;
                if (totalW > 0)
                {
                    var totalRect = new RectF(barRect.X, barRect.Y, totalW, barRect.Height);
                    canvas.FillColor = _b.TotalColor;
                    canvas.FillRoundedRectangle(totalRect, _b.CornerRadius);
                }

                // Current overlay (wavy right edge)
                float? curW = null;
                if (_b.ShowCurrentOverlay && _b.CurrentValue is double cur)
                {
                    var curT = Clamp01(cur / max);
                    var w = barRect.Width * curT;
                    if (w > 0)
                    {
                        curW = w;

                        var effectiveCurrentColor = ResolveCurrentColor();
                        canvas.FillColor = effectiveCurrentColor;
                        var overlay = BuildWavyOverlayPath(
                            x: barRect.X,
                            y: barRect.Y,
                            width: w,
                            height: barRect.Height,
                            cornerRadius: _b.CornerRadius,
                            waveAmplitude: _b.WaveAmplitude,
                            waveWavelength: _b.WaveWavelength);

                        canvas.FillPath(overlay);
                    }
                }

                // Expected marker line
                float? expX = null;
                if (_b.ShowExpectedMarker && _b.ExpectedValue is double exp)
                {
                    var expT = Clamp01(exp / max);
                    var xLine = barRect.X + barRect.Width * expT;

                    // keep inside bounds
                    xLine = MathF.Max(barRect.X + 0.5f, MathF.Min(barRect.Right - 0.5f, xLine));

                    canvas.StrokeColor = _b.ExpectedLineColor;
                    canvas.StrokeSize = _b.ExpectedLineThickness;
                    canvas.DrawLine(xLine, barRect.Y, xLine, barRect.Bottom);

                    expX = xLine;
                }

                // Labels (Total end, Current end, Expected line)
                if (_b.ShowLabels)
                {
                    var anchors = new List<LabelAnchor>(3);

                    // Total label at end of total bar
                    var totalX = barRect.X + totalW;
                    anchors.Add(new LabelAnchor(
                        CenterX: totalX,
                        Text: Format(_b.TotalValue, _b.LabelFormat),
                        Kind: "total"));

                    // Current label at end of overlay (if present)
                    if (_b.ShowCurrentOverlay && _b.CurrentValue is double cv && curW is float wCur && wCur > 0)
                    {
                        anchors.Add(new LabelAnchor(
                            CenterX: barRect.X + wCur,
                            Text: Format(cv, _b.LabelFormat),
                            Kind: "current"));
                    }

                    // Expected label above expected line
                    if (_b.ShowExpectedMarker && _b.ExpectedValue is double ev && expX is float xE)
                    {
                        anchors.Add(new LabelAnchor(
                            CenterX: xE,
                            Text: Format(ev, _b.LabelFormat),
                            Kind: "expected"));
                    }

                    // Layout the labels: measure + clamp + stagger to avoid overlap
                    DrawAnchoredLabels(canvas, dirtyRect, barRect, anchors);
                }
            }

            private void DrawAnchoredLabels(ICanvas canvas, RectF dirtyRect, RectF barRect, List<LabelAnchor> anchors)
            {
                // If you want label colors to match the element, you can change per kind here.
                // For now: shared pill background + shared text color.

                var font = Microsoft.Maui.Graphics.Font.Default;
                var fontSize = _b.LabelFontSize;

                // Build rects
                var placed = new List<PlacedLabel>(anchors.Count);

                foreach (var a in anchors)
                {
                    var textSize = canvas.GetStringSize(a.Text, font, fontSize);
                    var w = textSize.Width + (_b.LabelPaddingX * 2f);
                    var h = textSize.Height + (_b.LabelPaddingY * 2f);

                    // Center over anchor, clamp to drawing bounds
                    var x = a.CenterX - w / 2f;
                    x = MathF.Max(dirtyRect.X, MathF.Min(dirtyRect.Right - w, x));

                    // Default Y: above the bar
                    var y = a.Kind == "current" ? barRect.Y + _b.LabelOffset + h : barRect.Y - _b.LabelOffset - h;

                    placed.Add(new PlacedLabel(a, new RectF(x, y, w, h), textSize));
                }

                // Sort by X so we can do a simple overlap pass
                placed.Sort((p1, p2) => p1.Rect.X.CompareTo(p2.Rect.X));

                // If two labels overlap, nudge the right one upwards (stacking)
                for (int i = 1; i < placed.Count; i++)
                {
                    var prev = placed[i - 1];
                    var cur = placed[i];

                    if (RectsOverlap(prev.Rect, cur.Rect))
                    {
                        // raise current label above previous label
                        var newY = prev.Rect.Y - cur.Rect.Height - 4f;
                        placed[i] = cur with { Rect = new RectF(cur.Rect.X, newY, cur.Rect.Width, cur.Rect.Height) };
                    }
                }

                // Draw
                foreach (var p in placed)
                {
                    // Background pill
                    canvas.FillColor = _b.LabelBackgroundColor;
                    canvas.FillRoundedRectangle(p.Rect, _b.LabelCornerRadius);

                    // Text
                    canvas.Font = Microsoft.Maui.Graphics.Font.Default;
                    canvas.FontSize = _b.LabelFontSize;
                    canvas.FontColor = _b.LabelTextColor;

                    // DrawString rect draws with alignment options
                    canvas.DrawString(
                        p.Anchor.Text,
                        p.Rect,
                        HorizontalAlignment.Center,
                        VerticalAlignment.Center);
                }
            }

            private static bool RectsOverlap(RectF a, RectF b)
            {
                return a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
            }

            private static string Format(double value, string fmt)
            {
                // fmt like "0", "0.0", "0.##", "N0", etc
                return value.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
            }

            private static float Clamp01(double v)
            {
                if (v < 0) return 0;
                if (v > 1) return 1;
                return (float)v;
            }

            private static PathF BuildWavyOverlayPath(
                float x, float y, float width, float height,
                float cornerRadius, float waveAmplitude, float waveWavelength)
            {
                if (width <= 1)
                {
                    var pTiny = new PathF();
                    pTiny.AppendRoundedRectangle(new RectF(x, y, width, height), cornerRadius);
                    return pTiny;
                }

                var right = x + width;
                var bottom = y + height;

                var amp = MathF.Min(waveAmplitude, height * 0.45f);
                var wl = MathF.Max(4f, waveWavelength);

                var steps = (int)MathF.Ceiling(height / (wl / 2f));
                steps = Math.Max(10, steps);

                var r = MathF.Min(cornerRadius, height / 2f);

                var path = new PathF();
                path.MoveTo(x + r, y);
                path.LineTo(right, y);

                for (int i = 0; i <= steps; i++)
                {
                    var t = i / (float)steps;
                    var yy = y + t * height;

                    var phase = (t * height) * (2f * MathF.PI / wl);
                    var xx = right + MathF.Sin(phase) * amp;

                    path.LineTo(xx, yy);
                }

                path.LineTo(x + r, bottom);
                path.LineTo(x, bottom);
                path.LineTo(x, y);
                path.Close();

                return path;
            }

            private readonly record struct LabelAnchor(float CenterX, string Text, string Kind);
            private readonly record struct PlacedLabel(LabelAnchor Anchor, RectF Rect, SizeF TextSize);

            private Color ResolveCurrentColor()
            {
                if (_b.CurrentValue is not double current)
                    return _b.CurrentColor;

                // Pale blue when >= TotalValue
                if (current >= _b.TotalValue)
                    return Colors.AliceBlue; // Pale blue

                if (_b.ExpectedValue is double expected)
                {
                    if (current < expected)
                        return Colors.Red;

                    if (current > expected && current < _b.TotalValue)
                        return Colors.Green;
                }

                // Fallback
                return _b.CurrentColor;
            }

        }
    }
}
