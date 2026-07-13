[CmdletBinding()]
param(
    [ValidateSet('slow', 'medium', 'fast', 'max-release', 'custom')]
    [string]$Phase = 'custom',

    [ValidateRange(1, 300)]
    [int]$DurationSeconds = 10,

    [string]$OutputPath,

    [switch]$Append
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$monitorSource = @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace RoBa
{
    public sealed class WheelSample
    {
        public long ElapsedMs { get; set; }
        public long IntervalMs { get; set; }
        public string Axis { get; set; }
        public int Delta { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public uint Flags { get; set; }
    }

    public static class WheelMonitor
    {
        private const int WhMouseLl = 14;
        private const int WmMouseWheel = 0x020A;
        private const int WmMouseHWheel = 0x020E;
        private const uint PmRemove = 0x0001;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static readonly HookProc HookCallbackDelegate = HookCallback;
        private static readonly object Sync = new object();
        private static List<WheelSample> samples;
        private static Stopwatch clock;
        private static long lastVerticalMs;
        private static long lastHorizontalMs;

        public static WheelSample[] Capture(int durationMs)
        {
            if (durationMs <= 0)
            {
                throw new ArgumentOutOfRangeException("durationMs");
            }

            samples = new List<WheelSample>();
            clock = Stopwatch.StartNew();
            lastVerticalMs = -1;
            lastHorizontalMs = -1;

            string moduleName = Process.GetCurrentProcess().MainModule.ModuleName;
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            IntPtr hook = SetWindowsHookEx(WhMouseLl, HookCallbackDelegate, moduleHandle, 0);
            if (hook == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Unable to install the Windows low-level mouse hook.");
            }

            try
            {
                Message message;
                while (clock.ElapsedMilliseconds < durationMs)
                {
                    while (PeekMessage(out message, IntPtr.Zero, 0, 0, PmRemove))
                    {
                        TranslateMessage(ref message);
                        DispatchMessage(ref message);
                    }
                    Thread.Sleep(1);
                }
            }
            finally
            {
                UnhookWindowsHookEx(hook);
                clock.Stop();
            }

            lock (Sync)
            {
                return samples.ToArray();
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            int message = wParam.ToInt32();
            if (nCode >= 0 && (message == WmMouseWheel || message == WmMouseHWheel))
            {
                MouseLowLevelHook data =
                    (MouseLowLevelHook)Marshal.PtrToStructure(lParam, typeof(MouseLowLevelHook));
                int delta = unchecked((short)((data.MouseData >> 16) & 0xFFFF));
                long elapsedMs = clock.ElapsedMilliseconds;
                bool vertical = message == WmMouseWheel;
                long previousMs = vertical ? lastVerticalMs : lastHorizontalMs;

                WheelSample sample = new WheelSample
                {
                    ElapsedMs = elapsedMs,
                    IntervalMs = previousMs < 0 ? -1 : elapsedMs - previousMs,
                    Axis = vertical ? "vertical" : "horizontal",
                    Delta = delta,
                    ScreenX = data.Point.X,
                    ScreenY = data.Point.Y,
                    Flags = data.Flags
                };

                if (vertical)
                {
                    lastVerticalMs = elapsedMs;
                }
                else
                {
                    lastHorizontalMs = elapsedMs;
                }

                lock (Sync)
                {
                    samples.Add(sample);
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseLowLevelHook
        {
            public Point Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        {
            public IntPtr Window;
            public uint Id;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public Point Point;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int hookId, HookProc callback, IntPtr moduleHandle, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(
            out Message message, IntPtr window, uint min, uint max, uint remove);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref Message message);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref Message message);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
'@

if (-not ('RoBa.WheelMonitor' -as [type])) {
    Add-Type -TypeDefinition $monitorSource -Language CSharp
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $outputDirectory = Join-Path $repoRoot 'artifacts\encoder-scroll-monitor'
    $fileName = 'encoder-scroll-{0}.jsonl' -f (Get-Date -Format 'yyyyMMdd-HHmmss')
    $OutputPath = Join-Path $outputDirectory $fileName
} else {
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$startedAt = [DateTimeOffset]::Now
Write-Host ('roBa wheel monitor: phase={0}, duration={1}s' -f $Phase, $DurationSeconds)
Write-Host 'During capture, use only the roBa encoder. Other mouse wheels are also recorded.'
Write-Host 'Capturing now...'

$samples = [RoBa.WheelMonitor]::Capture($DurationSeconds * 1000)

$records = New-Object 'System.Collections.Generic.List[string]'
$sessionRecord = [ordered]@{
    schema = 'roba-wheel-monitor-v1'
    record_type = 'session'
    phase = $Phase
    started_at = $startedAt.ToString('o')
    duration_ms = $DurationSeconds * 1000
}
$records.Add(($sessionRecord | ConvertTo-Json -Compress))

foreach ($sample in $samples) {
    $eventRecord = [ordered]@{
        schema = 'roba-wheel-monitor-v1'
        record_type = 'wheel'
        phase = $Phase
        captured_at = $startedAt.AddMilliseconds($sample.ElapsedMs).ToString('o')
        elapsed_ms = $sample.ElapsedMs
        interval_ms = $sample.IntervalMs
        axis = $sample.Axis
        delta = $sample.Delta
        screen_x = $sample.ScreenX
        screen_y = $sample.ScreenY
        flags = $sample.Flags
    }
    $records.Add(($eventRecord | ConvertTo-Json -Compress))
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
if ($Append -and (Test-Path -LiteralPath $OutputPath)) {
    [System.IO.File]::AppendAllLines($OutputPath, $records, $utf8NoBom)
} else {
    [System.IO.File]::WriteAllLines($OutputPath, $records, $utf8NoBom)
}

$vertical = @($samples | Where-Object { $_.Axis -eq 'vertical' })
$intervals = @($vertical | Where-Object { $_.IntervalMs -ge 0 } |
    ForEach-Object { [long]$_.IntervalMs } | Sort-Object)

Write-Host ('Captured {0} wheel events ({1} vertical).' -f $samples.Count, $vertical.Count)
if ($intervals.Count -gt 0) {
    $medianIndex = [int][Math]::Floor(($intervals.Count - 1) / 2)
    Write-Host ('Vertical interval: min={0}ms, median={1}ms, max={2}ms' -f
        $intervals[0], $intervals[$medianIndex], $intervals[-1])
}
if ($vertical.Count -gt 0) {
    $distribution = $vertical | Group-Object Delta | Sort-Object { [int]$_.Name } |
        ForEach-Object { '{0}:{1}' -f $_.Name, $_.Count }
    Write-Host ('Vertical delta distribution: {0}' -f ($distribution -join ', '))
}
Write-Host ('Saved: {0}' -f $OutputPath)
