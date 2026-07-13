# roBa Windows Status Decisions

## 2026-07-13: One Composite Taskbar Icon

### Background

The desired experience was several dynamic indicators visible from the taskbar.

### Options

- Several independent top-level Windows and taskbar buttons.
- A tray-only monitor.
- One pinned application with a composite icon and detailed window.

### Decision

Use one pinned application. Render layer, right battery, and left battery as
three logical zones inside one dynamic icon.

### Reason

Windows can group multiple windows under one application identity, while
several buttons consume taskbar space and make pinning unclear. A composite icon
keeps the three status channels visible without fighting taskbar behavior.

### Impact

Exact battery percentages move to the window and taskbar description. The small
icon uses bars and a short layer label.

### Rollback

The renderer is isolated in `TaskbarIconRenderer.cs`. It can be replaced without
changing BLE or firmware code.

## 2026-07-13: New WPF Client

### Background

`zmk-battery-center` already solves multi-Battery-Service discovery, but its
current product model is cross-platform and tray-oriented.

### Decision

Build a focused WPF/.NET 8 application and use the existing project's public,
MIT-licensed implementation as a compatibility reference for ZMK battery
service enumeration and Characteristic User Description labels.

### Reason

WPF gives direct control over a pinned Windows taskbar icon and avoids adding a
Rust/Tauri toolchain that is not installed locally.

### Impact

The app is Windows-only and maintains its own BLE reconnect logic.

### Rollback

The firmware protocol is independent of WPF. A future client can consume the
same GATT characteristic.

## 2026-07-13: Separate Custom GATT Status Service

### Background

Current ZMK Studio messages expose keymap editing but not active-layer state or
battery snapshots.

### Decision

Add an encrypted read/notify GATT service on the right/central half.

### Reason

This avoids forking ZMK Studio protobuf messages, keeps Studio available, and
supports wireless monitoring.

### Impact

Windows may require re-pairing once to refresh its cached GATT service list.

### Rollback

Disable `CONFIG_ROBA_STATUS` and rebuild the right half.

## 2026-07-13: Dedicated USB HID with Automatic BLE Fallback

### Background

roBa already exposes keyboard/mouse HID and a ZMK Studio CDC interface over
USB. Status monitoring must work over the cable without competing with Studio.

### Options

- Mix status frames into the Studio CDC byte stream.
- Add another serial/COM interface.
- Add a dedicated vendor-defined HID input collection.

### Decision

Use a second vendor-defined HID interface for the same versioned 12-byte status
packet. The Windows app prefers USB and falls back to BLE automatically.

### Reason

Vendor HID needs no custom Windows driver, has a stable device identity, and
keeps the existing Studio RPC framing untouched. Automatic selection avoids a
settings burden and makes cable insertion the only user action.

### Impact

The right firmware uses two HID interfaces in addition to the existing Studio
CDC interface. The status interface is input/read-only and does not carry
keycodes or typed text.

### Rollback

Disable `CONFIG_ROBA_STATUS_USB`, restore `CONFIG_USB_HID_DEVICE_COUNT=1`, and
rebuild. BLE monitoring continues independently.

## 2026-07-13: Preserve the Detail Window and Add Task-Tray Residency

### Background

The existing compact status window and dynamic taskbar icon are useful, but an
always-running monitor should not occupy taskbar space when details are not
needed.

### Options

- Replace the current window with a tray-only application.
- Keep minimizing to the taskbar and add a redundant tray icon.
- Preserve the current window, but hide it to a dynamic tray icon on close or
  minimize.

### Decision

Preserve the current detail window and taskbar rendering. Close, minimize, and
`--minimized` startup hide the window to the notification area. A left click or
`roBa Statusを開く` restores it. Only explicit `終了` quits.

### Reason

This keeps the already accepted information layout while making long-running
monitoring unobtrusive. Single-instance activation also makes a pinned taskbar
shortcut a valid way to restore the hidden window.

### Impact

The Windows project enables the built-in WinForms `NotifyIcon` component, but
adds no third-party dependency or new persisted setting. The tray icon is
updated from the same renderer and debounced device state as the taskbar icon.
Restoring also issues the Windows `SW_RESTORE` command so a hidden window does
not remain internally minimized after `--minimized` startup or title-bar
minimization.

### Rollback

Remove `TrayIconService`, restore minimize/close to `WindowState.Minimized`, and
remove the single-instance activation event. Firmware and communication code
are unaffected.
