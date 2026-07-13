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
