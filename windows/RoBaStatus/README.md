# roBa Status for Windows

`roBa Status` is a Windows 11 companion for this roBa ZMK configuration. It is
designed to stay pinned on the taskbar and show three things at a glance:

- highest active layer;
- right/central battery;
- left/peripheral battery.

The taskbar icon is generated dynamically. Its upper area identifies the layer
and its two lower battery bars represent the two halves. Clicking the taskbar
button opens the detailed status window.

## Requirements

- Windows 11 x64
- Bluetooth pairing with `roBa`
- .NET 8 Desktop Runtime for the default framework-dependent build
- status-enabled `roBa_R` firmware for live layer display

Battery display continues to work through the standard ZMK Battery Service if
the custom layer service is unavailable.

## Build and Test

```powershell
dotnet build windows\RoBaStatus.sln -c Release -p:Platform=x64
dotnet test windows\RoBaStatus.Tests\RoBaStatus.Tests.csproj -c Release
```

## Publish

Framework-dependent single executable:

```powershell
powershell -ExecutionPolicy Bypass -File windows\publish.ps1
```

Self-contained build:

```powershell
powershell -ExecutionPolicy Bypass -File windows\publish.ps1 -Mode self-contained
```

Output:

```text
artifacts\RoBaStatus-win-x64\RoBaStatus.exe
```

## Install and Pin

After publishing:

```powershell
powershell -ExecutionPolicy Bypass -File windows\install.ps1
```

Then open `roBa Status` from Start, right-click its taskbar icon, and select
`Pin to taskbar`. The application does not pin itself.

## Use

- `再取得`: immediately retry Bluetooth discovery and reads.
- `最小化`: keep the taskbar monitor running.
- Window close button: same as minimize.
- `終了`: stop BLE monitoring and exit.
- `Windowsログイン時に起動`: add or remove the current executable from the
  current user's Run key. It is off by default.

## Firmware

The right/central firmware enables `CONFIG_ROBA_STATUS=y`. The service is
read-only and encrypted, and reports a 12-byte versioned snapshot. It does not
accept writes and never transmits keycodes or typed content.

```text
Service:         5a0e1000-7c7f-4b52-a8a8-3f5c726f4261
Characteristic:  5a0e1001-7c7f-4b52-a8a8-3f5c726f4261
```

If the app shows battery information but says that layer firmware is missing,
flash the status-enabled right-half UF2. If it still does not appear, remove and
re-pair roBa once so Windows refreshes its cached GATT services.

## Privacy and Safety

- No keyboard input is recorded.
- No text, keycode, or key position is sent to Windows.
- No cloud service or telemetry is used.
- The app does not change ZMK settings or keymaps.
- Removing the app does not change keyboard firmware or stored keyboard data.

## Known Unverified Items

- Live layer notifications have not yet been tested on flashed hardware.
- Real-device left/right battery identity must be confirmed after flashing.
- ZMK Studio coexistence is build-verified but not yet hardware-verified.
