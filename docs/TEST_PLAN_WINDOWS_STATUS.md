# roBa Windows Status Test Plan

Status: automated and build checks passed; hardware checks pending

## Automated Checks

```powershell
dotnet test windows\RoBaStatus.Tests\RoBaStatus.Tests.csproj -c Release
```

Current result on 2026-07-13: 14 passed, 0 failed.

Covered:

- valid protocol v1 parsing;
- unknown battery values;
- invalid size, version, layer, and mask rejection;
- MOUSE and SCROLL layer mapping;
- ordered active-layer labels;
- three tray tooltips and the Windows 63-character limit.
- independent layer, left-battery, and right-battery tray icon rendering.
- two-character layer labels and numeric battery labels, including `100` and unknown state.

## Windows Build and Visual Checks

- [x] Release build has no warnings or errors.
- [x] Framework-dependent single executable publishes.
- [x] Static executable icon is embedded.
- [x] Dynamic disconnected icon renders.
- [x] 520 x 390 layout shows all labels without clipping.
- [x] Explicit Quit stops the application.
- [x] Close hides the detail window without losing monitoring state.
- [ ] Close hides the window and leaves exactly one tray icon/process running.
- [x] Minimize hides the window to the same tray-resident process.
- [ ] Each of the Layer, Left, and Right tray icons restores the unchanged detail window on left click.
- [ ] Each tray icon's `再取得` requests an immediate refresh.
- [ ] Each tray icon's menu and window `終了` remove all three tray icons and stop the process.
- [x] A second launch restores the existing hidden instance without duplication.
- [x] `--minimized` starts with no taskbar window and remains one running process.
- [ ] Layer shows a two-character label and Left/Right show live numeric percentages.
- [ ] Layer, Left, and Right tooltips update with full live state.
- [ ] Dynamic DEFAULT/MOUSE/SCROLL icons verified from live BLE events.
- [ ] 125%, 150%, and 200% display scaling verified manually.
- [ ] Secondary-monitor taskbar placement verified manually.
- [ ] High Contrast mode verified manually.
- [ ] Connection label changes between `USB接続` and `Bluetooth接続`.

## Firmware Build Checks

- [x] `roBa_R` compiles `src/roba_status.c`.
- [x] `roBa_R` links with ZMK Studio enabled.
- [x] `roBa_R` builds with `CONFIG_USB_HID_DEVICE_COUNT=2`, ZMK Studio, and USB
  CDC enabled.
- [x] `roBa_R` produces a 566272-byte UF2.
- [x] `roBa_L` excludes the central-only status source.
- [x] `roBa_L` produces a 358400-byte UF2.
- [ ] Right-half firmware flashed.
- [ ] Keyboard typing unchanged.
- [ ] Trackball cursor unchanged.
- [ ] AML MOUSE activation unchanged.
- [ ] SCROLL behavior unchanged.
- [ ] ZMK Studio connects after the change.
- [ ] USB keyboard and pointing reports remain unchanged with the second HID
  interface enabled.
- [ ] Vendor-defined status HID returns an initial 12-byte snapshot.

## Live State Matrix

| Test | Expected |
|---|---|
| App starts before keyboard | Disconnected state, retry every 5 seconds |
| Keyboard reconnects | Battery and layer snapshot refresh automatically |
| Hold momentary layer | Layer icon changes, then returns on release |
| AML activates MOUSE | Layer 7 / MOUSE appears without notification spam |
| Hold SCROLL | Layer 11 / SCROLL appears while held |
| Toggle CONFIGURATION | Layer 10 remains active until toggled off |
| Left half off | Left battery becomes stale/unknown; app remains usable |
| Sleep and resume | Old handles are discarded and services rediscovered |
| Insert right-side USB cable | USB becomes active without user selection |
| Remove USB while BLE is paired | BLE resumes automatically |
| Bluetooth disabled while USB is present | USB monitoring continues |
| Open ZMK Studio over USB | Studio connects without corrupt status traffic |

## Rollback

1. Exit or uninstall the Windows app.
2. Set `CONFIG_ROBA_STATUS=n` or remove the explicit line from `roBa_R.conf`.
3. Rebuild and flash the previous right-half firmware.

The service is additive and does not change the keymap or stored settings.
