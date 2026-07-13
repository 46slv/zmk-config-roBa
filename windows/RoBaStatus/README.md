# roBa Status for Windows

`roBa Status` is a Windows 11 companion for this roBa ZMK configuration. Its
detail window can stay pinned on the taskbar, while day-to-day monitoring lives
quietly in the task tray. It shows three things at a glance:

- highest active layer;
- right/central battery;
- left/peripheral battery.

The taskbar icon is generated dynamically. The tray uses three separate dynamic
indicators: a two-character active-layer label, the left battery percentage,
and the right battery percentage. Hover tooltips identify the side and provide
the full layer name. Clicking the pinned taskbar shortcut or any tray indicator
opens the same detailed status window.

## Requirements

- Windows 11 x64
- A USB cable to `roBa_R`, or Bluetooth pairing with `roBa`
- .NET 8 Desktop Runtime for the default framework-dependent build
- status-enabled `roBa_R` firmware for live layer display

USB is preferred automatically. Bluetooth is the automatic fallback after the
cable is removed. Battery display continues to work through the standard ZMK
Battery Service if the custom layer service is unavailable.

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
`Pin to taskbar`. The application does not pin itself. Starting it again while
it is hidden restores the existing window instead of creating another tray
icon.

## Use

- `再取得`: immediately retry USB first, then Bluetooth discovery and reads.
- `トレイへ`: hide the detail window and keep monitoring.
- Minimize or window close: hide to the same tray icon.
- Any tray icon left click: restore and focus the detail window.
- Any tray icon right click: open, refresh, or quit.
- `終了`: remove all three tray icons, stop USB/BLE monitoring, and exit.
- `Windowsログイン時に起動`: add or remove the current executable from the
  current user's Run key. It is off by default and starts directly in the tray.

## Firmware

The right/central firmware enables `CONFIG_ROBA_STATUS=y` and
`CONFIG_ROBA_STATUS_USB=y`. Both transports report the same 12-byte versioned
snapshot. They accept no application writes and never transmit keycodes or
typed content.

```text
Service:         5a0e1000-7c7f-4b52-a8a8-3f5c726f4261
Characteristic:  5a0e1001-7c7f-4b52-a8a8-3f5c726f4261
USB usage page:  0xFF60
USB usage:       0x0001
USB report ID:   1
USB VID/PID:     1D50:615E
```

The status interface is a second vendor-defined HID collection. It needs no
custom driver and remains separate from keyboard/mouse HID and the ZMK Studio
CDC interface.

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
- USB HID enumeration, live input reports, and USB-to-BLE fallback must be
  confirmed after flashing the v1.1 right-half UF2.
