using Forms = System.Windows.Forms;
using RoBaStatus.Models;

namespace RoBaStatus.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _layerIcon;
    private readonly Forms.NotifyIcon _leftBatteryIcon;
    private readonly Forms.NotifyIcon _rightBatteryIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private System.Drawing.Icon? _dynamicLayerIcon;
    private System.Drawing.Icon? _dynamicLeftBatteryIcon;
    private System.Drawing.Icon? _dynamicRightBatteryIcon;
    private bool _disposed;

    public TrayIconService(DeviceStatus status)
    {
        _menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("roBa Statusを開く");
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);

        var refreshItem = new Forms.ToolStripMenuItem("再取得");
        refreshItem.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        var quitItem = new Forms.ToolStripMenuItem("終了");
        quitItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        _menu.Items.Add(openItem);
        _menu.Items.Add(refreshItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(quitItem);

        _layerIcon = CreateNotifyIcon();
        _leftBatteryIcon = CreateNotifyIcon();
        _rightBatteryIcon = CreateNotifyIcon();

        Update(status);
        _layerIcon.Visible = true;
        _leftBatteryIcon.Visible = true;
        _rightBatteryIcon.Visible = true;
    }

    public event EventHandler? ShowRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? QuitRequested;

    public void Update(DeviceStatus status)
    {
        if (_disposed)
        {
            return;
        }

        ReplaceIcon(
            _layerIcon,
            ref _dynamicLayerIcon,
            TaskbarIconRenderer.RenderLayerSystemIcon(status),
            TrayStatusText.BuildLayer(status));
        ReplaceIcon(
            _leftBatteryIcon,
            ref _dynamicLeftBatteryIcon,
            TaskbarIconRenderer.RenderBatterySystemIcon(status.LeftBattery),
            TrayStatusText.BuildLeftBattery(status));
        ReplaceIcon(
            _rightBatteryIcon,
            ref _dynamicRightBatteryIcon,
            TaskbarIconRenderer.RenderBatterySystemIcon(status.RightBattery),
            TrayStatusText.BuildRightBattery(status));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeNotifyIcon(_layerIcon);
        DisposeNotifyIcon(_leftBatteryIcon);
        DisposeNotifyIcon(_rightBatteryIcon);
        _menu.Dispose();
        _dynamicLayerIcon?.Dispose();
        _dynamicLeftBatteryIcon?.Dispose();
        _dynamicRightBatteryIcon?.Dispose();
        _dynamicLayerIcon = null;
        _dynamicLeftBatteryIcon = null;
        _dynamicRightBatteryIcon = null;
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var icon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = false
        };
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        return icon;
    }

    private static void ReplaceIcon(
        Forms.NotifyIcon target,
        ref System.Drawing.Icon? current,
        System.Drawing.Icon next,
        string tooltip)
    {
        var previous = current;
        current = next;
        target.Icon = next;
        target.Text = tooltip;
        previous?.Dispose();
    }

    private static void DisposeNotifyIcon(Forms.NotifyIcon icon)
    {
        icon.Visible = false;
        icon.Dispose();
    }
}
