using System.ComponentModel;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using RoBaStatus.Models;
using RoBaStatus.Services;

namespace RoBaStatus;

public partial class MainWindow : Window
{
    private readonly DeviceStatus _status = new();
    private readonly BleStatusMonitor _monitor;
    private readonly DispatcherTimer _iconDebounce;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _status;
        _monitor = new BleStatusMonitor(_status);
        _iconDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _iconDebounce.Tick += (_, _) =>
        {
            _iconDebounce.Stop();
            UpdateTaskbarIcon();
        };
        _status.VisualStateChanged += (_, _) => Dispatcher.Invoke(ScheduleIconUpdate);
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
        TaskbarInfo.Description = $"roBa · {_status.LayerName} · R {_status.RightBattery.Display} · L {_status.LeftBattery.Display}";
        TaskbarInfo.ProgressState = _status.IsConnected ? TaskbarItemProgressState.None : TaskbarItemProgressState.Paused;
        TaskbarInfo.ProgressValue = _status.IsConnected ? 0 : 1;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
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

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

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
        _allowClose = true;
        await _monitor.DisposeAsync();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            _status.Message = "タスクバーで監視を継続しています";
        }
        base.OnClosing(e);
    }
}
