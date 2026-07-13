namespace RoBaStatus.Models;

public sealed record LayerInfo(byte Id, string Name, string ShortName, string ColorHex);

public static class LayerCatalog
{
    private static readonly LayerInfo[] Layers =
    [
        new(0, "DEFAULT", "A", "#4B8DFF"),
        new(1, "LANG1 EAGER", "J", "#8B6CFF"),
        new(2, "NUMPAD / ARROWS", "N", "#35B779"),
        new(3, "FUNCTIONS / SYMBOLS", "F", "#E1A23A"),
        new(4, "MISC", "X", "#8C98A8"),
        new(5, "ALT TAB", "Alt", "#39B9C9"),
        new(6, "CTRL TAB", "Ctl", "#39B9C9"),
        new(7, "MOUSE", "M", "#21B8A6"),
        new(8, "MEDIA", "▶", "#A56BDA"),
        new(9, "EXTRA FUNCTIONS", "Ex", "#8794A5"),
        new(10, "CONFIGURATION", "⚙", "#E9823B"),
        new(11, "SCROLL", "S", "#2BA4FF")
    ];

    public static LayerInfo Get(byte id) =>
        Layers.FirstOrDefault(layer => layer.Id == id) ?? new(id, $"LAYER {id}", "?", "#747E8C");

    public static string Name(byte id) => Get(id).Name;
    public static string ShortName(byte id) => Get(id).ShortName;

    public static string ActiveNames(uint mask)
    {
        var names = new List<string>();
        for (byte layer = 0; layer < 32; layer++)
        {
            if ((mask & (1u << layer)) != 0)
            {
                names.Add(Name(layer));
            }
        }

        return names.Count == 0 ? "—" : string.Join(" + ", names);
    }
}
