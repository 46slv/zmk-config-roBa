using Microsoft.Win32;
using System.Diagnostics;

namespace RoBaStatus.Services;

public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RoBaStatus";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        var executable = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("実行ファイルの場所を取得できません");
        key.SetValue(ValueName, $"\"{executable}\" --minimized", RegistryValueKind.String);
    }
}
