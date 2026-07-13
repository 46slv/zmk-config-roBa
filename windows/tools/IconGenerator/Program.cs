using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: IconGenerator <output.ico>");
    return 2;
}

const int size = 256;
var visual = new DrawingVisual();
using (var dc = visual.RenderOpen())
{
    dc.DrawRoundedRectangle(
        new SolidColorBrush(Color.FromRgb(20, 25, 33)),
        null,
        new Rect(0, 0, size, size),
        48,
        48);
    dc.DrawRoundedRectangle(
        new SolidColorBrush(Color.FromRgb(43, 164, 255)),
        null,
        new Rect(16, 16, 224, 138),
        32,
        32);

    var text = new FormattedText(
        "rB",
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
        102,
        Brushes.White,
        1.0);
    dc.DrawText(text, new Point((size - text.Width) / 2, 26));

    DrawBattery(dc, new Rect(27, 180, 78, 42), 0.82);
    DrawBattery(dc, new Rect(151, 180, 78, 42), 0.76);
}

var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
bitmap.Render(visual);
var encoder = new PngBitmapEncoder();
encoder.Frames.Add(BitmapFrame.Create(bitmap));
using var png = new MemoryStream();
encoder.Save(png);
var pngBytes = png.ToArray();

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
using var file = File.Create(output);
using var writer = new BinaryWriter(file);
writer.Write((ushort)0); // reserved
writer.Write((ushort)1); // icon
writer.Write((ushort)1); // one image
writer.Write((byte)0);   // 256 px
writer.Write((byte)0);
writer.Write((byte)0);   // palette
writer.Write((byte)0);
writer.Write((ushort)1); // planes
writer.Write((ushort)32);
writer.Write((uint)pngBytes.Length);
writer.Write((uint)22);
writer.Write(pngBytes);
return 0;

static void DrawBattery(DrawingContext dc, Rect rect, double fill)
{
    var pen = new Pen(new SolidColorBrush(Color.FromRgb(205, 214, 226)), 7);
    dc.DrawRoundedRectangle(null, pen, rect, 8, 8);
    dc.DrawRectangle(Brushes.LightGray, null, new Rect(rect.Right + 3, rect.Y + 11, 8, 20));
    dc.DrawRoundedRectangle(
        new SolidColorBrush(Color.FromRgb(75, 200, 132)),
        null,
        new Rect(rect.X + 8, rect.Y + 8, (rect.Width - 16) * fill, rect.Height - 16),
        4,
        4);
}
