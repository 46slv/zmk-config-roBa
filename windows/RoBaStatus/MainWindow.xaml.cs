using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using RoBaStatus.Models;
using RoBaStatus.Services;

namespace RoBaStatus;

public partial class MainWindow : Window
{
    private const int ShowWindowRestore = 9;
    private readonly DeviceStatus _status = new();
    private readonly StatusMonitor _monitor;
    private readonly TrayIconService _trayIcon;
    private readonly DispatcherTimer _iconDebounce;
    private bool _allowClose;
    private bool _isQuitting;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _status;
        _monitor = new StatusMonitor(_status);
        _trayIcon = new TrayIconService(_status);
        _trayIcon.ShowRequested += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _trayIcon.RefreshRequested += TrayRefreshRequested;
        _trayIcon.QuitRequested += TrayQuitRequested;
        _iconDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _iconDebounce.Tick += (_, _) =>
        {
            _iconDebounce.Stop();
            UpdateTaskbarIcon();
        };
        _status.VisualStateChanged += (_, _) => Dispatcher.Invoke(ScheduleIconUpdate);
        StateChanged += (_, _) =>
        {
            if (!_allowClose && WindowState == WindowState.Minimized)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (!_allowClose && WindowState == WindowState.Minimized)
                    {
                        HideToTray();
                    }
                }, DispatcherPriority.Background);
            }
        };
        Loaded += (_, _) =>
        {
            StartupCheckBox.IsChecked = StartupManager.IsEnabled();
            UpdateTaskbarIcon();
            _monitor.Start();
        };
    }

    private void ScheduleIconUpdate()
    {
        _iconDebounce.Stop();
        _iconDebounce.Start();
    }

    private void UpdateTaskbarIcon()
    {
        var icon = TaskbarIconRenderer.Render(_status);
        Icon = icon;
        TaskbarInfo.Overlay = null;
        TaskbarInfo.Description = $"roBa · {_status.TransportLabel} · {_status.LayerName} · R {_status.RightBattery.Display} · L {_status.LeftBattery.Display}";
        TaskbarInfo.ProgressState = _status.IsConnected ? TaskbarItemProgressState.None : TaskbarItemProgressState.Paused;
        TaskbarInfo.ProgressValue = _status.IsConnected ? 0 : 1;
        _trayIcon.Update(_status);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _status.Message = "再取得しています…";
            await _monitor.RefreshNowAsync();
        }
        catch (Exception ex)
        {
            _status.Message = $"取得失敗: {ex.Message}";
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => HideToTray();

    private async void TrayRefreshRequested(object? sender, EventArgs e) => await RefreshAsync();

    private async void TrayQuitRequested(object? sender, EventArgs e) => await QuitAsync();

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        try
        {
            StartupManager.SetEnabled(StartupCheckBox.IsChecked == true);
        }
        catch (Exception ex)
        {
            _status.Message = $"自動起動設定を保存できません: {ex.Message}";
        }
    }

    private async void Quit_Click(object sender, RoutedEventArgs e)
    {
        await QuitAsync();
    }

    private async Task QuitAsync()
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        _allowClose = true;
        _iconDebounce.Stop();
        _trayIcon.Dispose();
        await _monitor.DisposeAsync();
        System.Windows.Application.Current.Shutdown();
    }

    private void HideToTray()
    {
        if (_allowClose)
        {
            return;
        }

        Hide();
        WindowState = WindowState.Normal;
        _status.Message = "タスクトレイで監視を継続しています";
    }

    public void ShowFromTray()
    {
        if (_isQuitting)
        {
            return;
        }

        WindowState = WindowState.Normal;
        if (!IsVisible)
        {
            Show();
        }
        var handle = new WindowInteropHelper(this).Handle;
        ShowWindow(handle, ShowWindowRestore);
        Activate();
        SetForegroundWindow(handle);
        Topmost = true;
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
        }
        base.OnClosing(e);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}
