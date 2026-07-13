# roBa Windows Status App Specification

Status: v1.1 USB transport implemented; hardware validation pending
Created: 2026-07-13

## 1. Purpose

Provide an always-available Windows view of the current roBa layer and the
battery level of both halves without requiring the user to open a settings
screen or inspect Windows Bluetooth settings.

The primary experience is a normal Windows application that the user can pin
to the taskbar. It should feel like a small hardware status instrument, not a
configuration utility.

## 2. Target User

- Primary user: the owner of this roBa configuration on Windows 11.
- Secondary future users: owners of split ZMK keyboards that expose compatible
  battery data and an optional roBa status service.

## 3. UX Priorities

In order of importance:

1. Recognize the effective layer with a glance.
2. Notice a low left or right battery before input stops.
3. Open details with one taskbar click.
4. Recover automatically after sleep, Bluetooth loss, or keyboard reconnection.
5. Avoid distracting notifications during automatic mouse-layer changes.

## 4. Assumptions

These assumptions allow preparation to continue without blocking on a full
interview. They must be validated with a clickable prototype before the visual
design is frozen.

- Windows 11 is the first target; Windows 10 is not a v1 acceptance target.
- The app is user-pinned to the taskbar. It does not attempt to pin itself.
- One pinned taskbar button is preferable to several independent app windows.
- The default taskbar presentation is a composite status icon with three
  information zones: layer, right battery, and left battery.
- Detailed text appears in a compact flyout opened by one click.
- The app does not edit the keymap or ZMK settings in v1.
- The app stores preferences and optional battery history, but never records
  typed keys or text.

## 5. Taskbar Experience

### 5.1 Why One Composite Icon

Creating several top-level windows can produce several taskbar buttons, but
Windows may group them and treat them as one application. Multiple buttons also
consume taskbar space and make pinning behavior harder to understand.

The v1 design therefore uses one taskbar button whose icon is rendered from
three logical status tiles:

```text
┌─────── taskbar icon ───────┐
│ layer │ right │ left       │
│  SCR  │  82   │  76       │
└────────────────────────────┘
```

At small icon sizes, the renderer may simplify this to a layer glyph plus two
battery bars. Exact percentages remain available in the flyout and tooltip.

### 5.2 Icon States

Layer zone:

| State | Short label | Visual cue |
|---|---|---|
| Default | `A` | neutral blue |
| Language/eager tap | `J` | violet |
| Numpad/arrows | `N` | green |
| Functions/symbols | `F` | amber |
| Misc | `X` | gray |
| Alt tab | `Alt` | cyan |
| Ctrl tab | `Ctl` | cyan |
| Mouse | mouse glyph | teal |
| Media | media glyph | purple |
| Extra functions | `Ex` | gray |
| Configuration | gear glyph | orange |
| Scroll | wheel glyph | bright blue |
| Unknown | `?` | gray |

Battery zones:

- 51-100%: normal.
- 21-50%: normal with reduced fill.
- 11-20%: amber.
- 1-10%: red.
- Charging: lightning mark.
- Stale data: dotted outline.
- Unknown/disconnected: dash.

Color must not be the only carrier of meaning. Labels, fill level, glyphs, and
tooltips remain available for color-vision and high-contrast use.

### 5.3 Update Policy

- Layer icon changes should appear within 100 ms of receiving a state event.
- Battery icon changes only when the integer level or freshness state changes.
- Re-rendering is coalesced to avoid taskbar flicker during rapid AML changes.
- A 150 ms debounce is allowed for visual-only layer changes; internal state is
  updated immediately.

## 6. Flyout

One taskbar click opens or focuses a compact non-modal window near the taskbar.
It contains three large tiles:

```text
┌─────────────────────────────┐
│ roBa                        │
│                             │
│ LAYER       RIGHT    LEFT   │
│ SCROLL       82%      76%   │
│ Default+     BLE      BLE   │
│                             │
│ Connected · updated now     │
└─────────────────────────────┘
```

Required interactions:

- Click the taskbar button: show/focus the flyout.
- Click outside or press Escape: dismiss the flyout.
- Open again: show the last known state immediately, then refresh.
- Settings button: open a conventional settings page.
- Close button: close the visible window but keep monitoring if background
  monitoring is enabled.
- Explicit Quit command: terminate monitoring and exit.

## 7. Notifications

Default notification policy:

- Notify at 20%, 10%, and 5% independently for left and right.
- Do not repeat a threshold notification until the battery has recovered above
  that threshold and falls below it again.
- Notify after a prolonged disconnect, not on brief reconnects.
- Do not show Windows notifications for ordinary layer changes.
- Optional layer OSD is disabled by default.
- If enabled, automatic MOUSE layer changes are excluded from OSD by default.

## 8. Data Model

```text
DeviceStatus
  protocolVersion
  connectionState
  transport
  activeLayerMask
  highestLayer
  layerName
  rightBattery
    percent
    charging
    freshness
    sampledAt
  leftBattery
    percent
    charging
    freshness
    sampledAt
  receivedAt
  sequence
```

The UI displays the highest active layer prominently while retaining the full
active-layer bitmask. This is necessary because ZMK can have multiple active
layers at once.

## 9. Communication

### 9.1 v1 Battery Path

Reuse the standard BLE Battery Service where practical. The current roBa
configuration already enables:

- `CONFIG_ZMK_BATTERY_REPORTING=y`
- `CONFIG_ZMK_SPLIT_BLE_CENTRAL_BATTERY_LEVEL_FETCHING=y`
- `CONFIG_ZMK_SPLIT_BLE_CENTRAL_BATTERY_LEVEL_PROXY=y`

The existing MIT-licensed `kot149/zmk-battery-center` proves that Windows can
read central and peripheral ZMK battery services and is the first reuse or
reference candidate.

### 9.2 Layer Path

Standard HID and the current ZMK Studio RPC messages do not expose the active
layer. A small central-side ZMK module must therefore:

1. subscribe to `zmk_layer_state_changed`;
2. read the active layer mask and highest active layer;
3. publish a versioned status packet;
4. send an initial snapshot after the Windows client subscribes;
5. avoid recording or transmitting keycodes.

The wireless transport is a custom BLE GATT status characteristic with read and
notify support.

The same packet is also exposed through a dedicated vendor-defined USB HID
collection. USB is selected automatically when the right/central half is
connected by cable; BLE is the automatic fallback after USB removal. The user
does not select a transport manually.

USB CDC/ACM is not used for status data. The existing ZMK Studio USB UART must
remain byte-for-byte independent so monitoring cannot interfere with Studio.

### 9.3 Proposed Status Packet

```text
byte 0     protocol version
byte 1     message flags
byte 2     highest layer
byte 3-6   active layer mask, little endian
byte 7     right battery, 0-100 or 255 unknown
byte 8     left battery, 0-100 or 255 unknown
byte 9     charging and freshness flags
byte 10-11 sequence, little endian
```

Battery fields may initially remain sourced from the standard Battery Service.
They are reserved in the custom packet so a later firmware version can provide
one atomic snapshot without breaking the Windows data model.

## 10. Persistence

Store only local application preferences:

- launch at login;
- continue monitoring when the window closes;
- taskbar icon style;
- low-battery thresholds;
- OSD preference and exclusions;
- last selected device;
- optional battery history retention.

Use a versioned JSON settings file under the user's local application data
directory. If it is missing or malformed, use defaults and keep the app usable.

## 11. Error Handling

- Keyboard unavailable: show the last state as stale, then disconnected.
- Bluetooth permission denied: show a direct remediation message.
- Battery service missing: keep layer monitoring and mark battery unknown.
- Custom layer service missing: keep battery monitoring and explain that the
  status-enabled firmware is required.
- Resume from sleep: discard stale handles, rediscover, and resubscribe.
- Unknown protocol version: do not guess packet fields; show an update-required
  state.
- Icon rendering failure: fall back to a bundled static roBa icon.

## 12. Undo and Safety

- v1 is read-only toward the keyboard.
- It does not change keymap bindings, layers, Bluetooth profiles, or settings.
- Uninstalling the app leaves firmware and keyboard data unchanged.
- Firmware support is additive and must not change existing key behavior.
- Disabling the feature at build time restores the prior firmware behavior.

## 13. Compatibility

- Current target repository: ZMK `v0.3`, roBa right half as central.
- The firmware extension must compile conditionally and stay isolated from the
  current trackball, scroll, AML, combo, macro, and keymap paths.
- Existing ZMK Studio behavior must continue to work.
- The dedicated status HID collection must coexist with the keyboard/mouse HID
  collection and the existing ZMK Studio CDC interface.
- The Windows app must continue showing battery information if the custom layer
  service is unavailable.

## 14. Out of Scope for v1

- Editing the keymap or configuring ZMK Studio.
- Recording key presses, key frequency, WPM, or typed text.
- Firmware flashing from the Windows app.
- Multiple keyboards at the same time.
- macOS or Linux versions.
- Several independent taskbar buttons.
- Cloud accounts, synchronization, telemetry, or external APIs.
- Manual USB/BLE transport selection.
- Charging-state detection in the first USB transport revision.

## 15. Verification

### UX

- Composite icon remains readable at 100%, 125%, 150%, and 200% scaling.
- Light, dark, and high-contrast taskbars remain distinguishable.
- Flyout is usable on primary and secondary monitors.
- Rapid AML transitions do not flicker or steal focus.
- Default, MOUSE, SCROLL, CONFIGURATION, and an unknown layer are recognizable.

### Firmware and Communication

- Initial snapshot is received after connection.
- Momentary, toggle, macro-driven, AML, and SCROLL layer changes are correct.
- Active layer mask and highest layer agree with ZMK behavior.
- Left and right battery identities are not swapped.
- Reconnection succeeds after sleep and Bluetooth interruption.
- USB is preferred automatically when present and BLE resumes after cable
  removal.
- ZMK Studio still connects when the status feature is enabled.

### Failure Cases

- Keyboard off at startup.
- One split half unavailable.
- Battery value stale.
- Unsupported firmware.
- Corrupt local settings.
- Bluetooth permission denied.

## 16. Resolved v1 Decisions

- The taskbar icon uses a layer label plus two battery bars. Exact percentages
  are shown in the window and taskbar description.
- The pinned icon opens a compact persistent WPF window. Closing minimizes it;
  the explicit Quit button stops monitoring.
- The implementation is a focused WPF/.NET 8 client. The battery service
  discovery model follows the proven `zmk-battery-center` approach.
- Production transports are a dedicated vendor-defined USB HID input interface
  and encrypted BLE GATT. USB is preferred automatically; the existing USB CDC
  stream remains exclusive to ZMK Studio.
- Startup at login is optional and controlled by an unchecked setting.

## 17. Implementation Result

- Windows solution: `windows/RoBaStatus.sln`
- Companion executable project: `windows/RoBaStatus/RoBaStatus.csproj`
- Firmware status service: `src/roba_status.c`
- Service UUID: `5a0e1000-7c7f-4b52-a8a8-3f5c726f4261`
- Status characteristic UUID: `5a0e1001-7c7f-4b52-a8a8-3f5c726f4261`
- Packet parser and layer mapping tests: 10 passed on 2026-07-13
- Windows Release build: passed without warnings on 2026-07-13
- roBa_R and roBa_L ZMK builds: passed on 2026-07-13
- Visual QA: completed on the published executable at 520 x 390
- Hardware flash and live BLE notification verification: not yet performed
- USB vendor HID uses usage page `0xFF60`, usage `0x0001`, and report ID `1`.
  The Windows client uses Win32 SetupAPI/HID APIs so the single executable does
  not require package capabilities, a custom driver, or a third-party library.
- Transport order is USB first, then BLE. USB removal is detected by the HID
  read loop and the coordinator rediscovers BLE automatically.
