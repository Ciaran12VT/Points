using Microsoft.Maui.Graphics;

namespace Points.Views.Cards;

public sealed class SparklineDrawable : IDrawable
{
    public IList<double> Values { get; set; } = Array.Empty<double>();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        if (Values == null || Values.Count == 0)
        {
            canvas.StrokeSize = 2;
            canvas.StrokeColor = Colors.Gray.WithAlpha(0.35f);
            var y = dirtyRect.Top + dirtyRect.Height / 2;
            canvas.DrawLine(dirtyRect.Left + 8, y, dirtyRect.Right - 8, y);
            canvas.RestoreState();
            return;
        }

        if (Values.Count == 1)
        {
            canvas.FillColor = Colors.Gray.WithAlpha(0.6f);
            canvas.FillCircle(dirtyRect.Center.X, dirtyRect.Center.Y, 3);
            canvas.RestoreState();
            return;
        }

        double min = Values.Min();
        double max = Values.Max();
        double range = Math.Max(1e-9, max - min);

        float padX = 6, padY = 6;
        float left = dirtyRect.Left + padX;
        float right = dirtyRect.Right - padX;
        float top = dirtyRect.Top + padY;
        float bottom = dirtyRect.Bottom - padY;

        int n = Values.Count;

        canvas.StrokeSize = 2.5f;
        canvas.StrokeColor = Colors.White;

        var path = new PathF();

        for (int i = 0; i < n; i++)
        {
            float x = left + (right - left) * i / (n - 1);

            double t = (Values[i] - min) / range;
            float y = bottom - (float)((bottom - top) * t);

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        canvas.DrawPath(path);

        // last-point marker
        float lastX = right;
        double lastT = (Values[n - 1] - min) / range;
        float lastY = bottom - (float)((bottom - top) * lastT);

        canvas.FillColor = Colors.White;
        canvas.FillCircle(lastX, lastY, 3);

        canvas.RestoreState();
    }
}
