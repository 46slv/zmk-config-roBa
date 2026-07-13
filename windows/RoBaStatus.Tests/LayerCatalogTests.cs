using RoBaStatus.Models;

namespace RoBaStatus.Tests;

public sealed class LayerCatalogTests
{
    [Fact]
    public void ReportsKnownRoBaLayers()
    {
        Assert.Equal("MOUSE", LayerCatalog.Name(7));
        Assert.Equal("SCROLL", LayerCatalog.Name(11));
    }

    [Fact]
    public void ListsAllActiveLayersInOrder()
    {
        Assert.Equal("DEFAULT + MOUSE + SCROLL", LayerCatalog.ActiveNames((1u << 0) | (1u << 7) | (1u << 11)));
    }
}
