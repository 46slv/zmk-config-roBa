using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RoBaStatus.Models;

public sealed class DeviceStatus : INotifyPropertyChanged
{
    private bool _isConnected;
    private byte _highestLayer;
    private uint _activeLayerMask = 1;
    private BatteryReading _rightBattery = BatteryReading.Unknown;
    private BatteryReading _leftBattery = BatteryReading.Unknown;
    private string _message = "roBaを探しています…";
    private DateTimeOffset? _lastUpdated;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? VisualStateChanged;

    public bool IsConnected
    {
        get => _isConnected;
        set => Set(ref _isConnected, value);
    }

    public byte HighestLayer
    {
        get => _highestLayer;
        set => Set(ref _highestLayer, value);
    }

    public uint ActiveLayerMask
    {
        get => _activeLayerMask;
        set => Set(ref _activeLayerMask, value);
    }

    public BatteryReading RightBattery
    {
        get => _rightBattery;
        set => Set(ref _rightBattery, value);
    }

    public BatteryReading LeftBattery
    {
        get => _leftBattery;
        set => Set(ref _leftBattery, value);
    }

    public string Message
    {
        get => _message;
        set => Set(ref _message, value, false);
    }

    public DateTimeOffset? LastUpdated
    {
        get => _lastUpdated;
        set => Set(ref _lastUpdated, value, false);
    }

    public string LayerName => LayerCatalog.Name(HighestLayer);
    public string LayerShortName => LayerCatalog.ShortName(HighestLayer);
    public string ActiveLayers => LayerCatalog.ActiveNames(ActiveLayerMask);
    public string ConnectionLabel => IsConnected ? "接続中" : "未接続";
    public string LastUpdatedLabel => LastUpdated is { } time
        ? $"更新 {time.LocalDateTime:HH:mm:ss}"
        : "未取得";

    private void Set<T>(ref T field, T value, bool affectsVisual = true, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);

        if (name is nameof(HighestLayer))
        {
            OnPropertyChanged(nameof(LayerName));
            OnPropertyChanged(nameof(LayerShortName));
        }
        else if (name is nameof(ActiveLayerMask))
        {
            OnPropertyChanged(nameof(ActiveLayers));
        }
        else if (name is nameof(IsConnected))
        {
            OnPropertyChanged(nameof(ConnectionLabel));
        }
        else if (name is nameof(LastUpdated))
        {
            OnPropertyChanged(nameof(LastUpdatedLabel));
        }

        if (affectsVisual)
        {
            VisualStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
