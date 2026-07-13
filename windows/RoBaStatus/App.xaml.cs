using System.Windows;

namespace RoBaStatus;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\RoBaStatus.SingleInstance.v1";
    private const string ActivationEventName = @"Local\RoBaStatus.Activate.v1";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;
    private Task? _activationTask;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _instanceMutex = new Mutex(true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            _activationEvent.Set();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        _activationCancellation = new CancellationTokenSource();
        _activationTask = Task.Run(() => ListenForActivation(_activationCancellation.Token));

        if (e.Args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void ListenForActivation(CancellationToken cancellationToken)
    {
        if (_activationEvent is null)
        {
            return;
        }

        var handles = new[] { _activationEvent, cancellationToken.WaitHandle };
        while (!cancellationToken.IsCancellationRequested)
        {
            if (WaitHandle.WaitAny(handles) != 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (MainWindow is RoBaStatus.MainWindow window)
                {
                    window.ShowFromTray();
                }
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        _activationEvent?.Set();
        try
        {
            _activationTask?.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch (AggregateException)
        {
        }

        if (_ownsMutex)
        {
            _instanceMutex?.ReleaseMutex();
        }

        _activationCancellation?.Dispose();
        _activationEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
