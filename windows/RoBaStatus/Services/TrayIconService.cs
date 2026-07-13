using Forms = System.Windows.Forms;
using RoBaStatus.Models;

namespace RoBaStatus.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private System.Drawing.Icon? _dynamicIcon;
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

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = false
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        Update(status);
        _notifyIcon.Visible = true;
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

        var next = TaskbarIconRenderer.RenderSystemIcon(status);
        var previous = _dynamicIcon;
        _dynamicIcon = next;
        _notifyIcon.Icon = next;
        _notifyIcon.Text = TrayStatusText.Build(status);
        previous?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _dynamicIcon?.Dispose();
        _dynamicIcon = null;
    }
}
