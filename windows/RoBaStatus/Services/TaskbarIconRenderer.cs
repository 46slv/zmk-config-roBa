using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RoBaStatus.Models;

namespace RoBaStatus.Services;

public static class TaskbarIconRenderer
{
    public static BitmapSource Render(DeviceStatus status, int size = 64)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var connected = status.IsConnected;
            var layer = LayerCatalog.Get(status.HighestLayer);
            var layerColor = connected
                ? (Color)ColorConverter.ConvertFromString(layer.ColorHex)
                : Color.FromRgb(92, 99, 108);

            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(20, 25, 33)),
                new Pen(new SolidColorBrush(Color.FromRgb(66, 76, 91)), Math.Max(1, size / 32d)),
                new Rect(1, 1, size - 2, size - 2),
                size * 0.16,
                size * 0.16);

            var layerRect = new Rect(3, 3, size - 6, size * 0.55);
            dc.DrawRoundedRectangle(new SolidColorBrush(layerColor), null, layerRect, size * 0.12, size * 0.12);

            var shortName = connected ? layer.ShortName : "—";
            DrawCenteredText(dc, shortName, layer.ShortName.Length >= 3 ? size * 0.22 : size * 0.31, layerRect);

            var batteryTop = size * 0.66;
            DrawBattery(dc, new Rect(size * 0.08, batteryTop, size * 0.34, size * 0.20), status.RightBattery);
            DrawBattery(dc, new Rect(size * 0.57, batteryTop, size * 0.34, size * 0.20), status.LeftBattery);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawBattery(DrawingContext dc, Rect rect, BatteryReading battery)
    {
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(190, 199, 211)), Math.Max(1, rect.Height * 0.08));
        dc.DrawRectangle(null, outline, rect);
        dc.DrawRectangle(Brushes.LightGray, null, new Rect(rect.Right, rect.Y + rect.Height * 0.28, rect.Width * 0.08, rect.Height * 0.44));

        if (battery.Percent is not int value)
        {
            DrawCenteredText(dc, "–", rect.Height * 0.8, rect);
            return;
        }

        value = Math.Clamp(value, 0, 100);
        var color = value <= 10
            ? Color.FromRgb(236, 77, 82)
            : value <= 20
                ? Color.FromRgb(238, 165, 52)
                : Color.FromRgb(75, 200, 132);
        var inset = Math.Max(1.5, rect.Height * 0.16);
        var width = Math.Max(1, (rect.Width - inset * 2) * value / 100d);
        dc.DrawRectangle(
            new SolidColorBrush(color),
            null,
            new Rect(rect.X + inset, rect.Y + inset, width, rect.Height - inset * 2));
    }

    private static void DrawCenteredText(DrawingContext dc, string text, double size, Rect rect)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size,
            Brushes.White,
            1.0);
        dc.DrawText(formatted, new Point(
            rect.X + (rect.Width - formatted.Width) / 2,
            rect.Y + (rect.Height - formatted.Height) / 2));
    }
}
