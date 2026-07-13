using System.Windows;

namespace RoBaStatus;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (e.Args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            window.WindowState = WindowState.Minimized;
        }
    }
}
