using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RoBaStatus.Models;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FlowDirection = System.Windows.FlowDirection;
using FontFamily = System.Windows.Media.FontFamily;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

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

    public static System.Drawing.Icon RenderSystemIcon(DeviceStatus status, int size = 32)
    {
        return ToSystemIcon(Render(status, size));
    }

    public static System.Drawing.Icon RenderLayerSystemIcon(DeviceStatus status, int size = 32)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var layer = LayerCatalog.Get(status.HighestLayer);
            var color = status.IsConnected
                ? (Color)ColorConverter.ConvertFromString(layer.ColorHex)
                : Color.FromRgb(92, 99, 108);
            DrawIconBackground(dc, size);
            var rect = new Rect(3, 3, size - 6, size - 6);
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null, rect, size * 0.16, size * 0.16);
            var shortName = status.IsConnected ? layer.ShortName : "—";
            DrawCenteredText(dc, shortName, layer.ShortName.Length >= 3 ? size * 0.26 : size * 0.40, rect);
        }

        return ToSystemIcon(Render(visual, size));
    }

    public static System.Drawing.Icon RenderBatterySystemIcon(BatteryReading battery, string side, int size = 32)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            DrawIconBackground(dc, size);
            DrawCenteredText(dc, side, size * 0.27, new Rect(2, 2, size - 4, size * 0.26));
            var batteryRect = new Rect(size * 0.17, size * 0.43, size * 0.60, size * 0.30);
            DrawBattery(dc, batteryRect, battery);
            if (battery.IsCharging)
            {
                DrawCenteredText(dc, "⚡", size * 0.25, new Rect(2, size * 0.72, size - 4, size * 0.22));
            }
        }

        return ToSystemIcon(Render(visual, size));
    }

    private static System.Drawing.Icon ToSystemIcon(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        using var bitmap = new System.Drawing.Bitmap(stream);
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = System.Drawing.Icon.FromHandle(handle);
            return (System.Drawing.Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static BitmapSource Render(DrawingVisual visual, int size)
    {
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawIconBackground(DrawingContext dc, int size)
    {
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(20, 25, 33)),
            new Pen(new SolidColorBrush(Color.FromRgb(66, 76, 91)), Math.Max(1, size / 32d)),
            new Rect(1, 1, size - 2, size - 2),
            size * 0.16,
            size * 0.16);
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
