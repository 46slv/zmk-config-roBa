# roBa Windows Status App Implementation Plan

Status: v1.1 USB transport implementation completed; hardware validation pending
Created: 2026-07-13

## Project Boundary

Keep firmware configuration and the Windows application separable.

Resolved repository layout:

```text
zephyr/
  CMakeLists.txt
  Kconfig
src/
  roba_status.c
windows/
  RoBaStatus/
  RoBaStatus.Tests/
docs/
  WINDOWS_STATUS_APP_SPEC.md
  WINDOWS_STATUS_APP_IMPLEMENTATION_PLAN.md
```

The focused WPF client remains under `windows/`; the Zephyr module remains
independently build-gated under `zephyr/` and `src/`.

## Recommended Delivery Stages

### Stage 0: Feasibility Spikes

Produce disposable, narrowly scoped proofs:

1. Render and replace a taskbar icon at 16, 20, 24, 32, and 48 px.
2. Compare exact percentage text against battery-bar glyphs at normal scaling.
3. Read both roBa battery services on Windows.
4. Subscribe to the dedicated vendor-defined USB HID layer-state channel.
5. Confirm that ZMK Studio and status monitoring can coexist.

Exit criteria:

- one composite icon direction is readable;
- left and right batteries are correctly identified;
- a real MOUSE or SCROLL transition reaches Windows;
- the selected transport does not alter keyboard input behavior.

### Stage 1: Firmware Status Module

- Add a build-time feature flag.
- Subscribe to `zmk_layer_state_changed` on the central side only.
- Maintain the current active-layer mask and highest layer.
- Expose a read + notify snapshot.
- Add protocol-version and sequence fields.
- Keep all keycode and input-event content out of the protocol.
- Add native-sim or module-level tests where possible.

Required documentation updates in the same work item:

- `docs/ZMK_RESEARCH_NOTES.md`
- `docs/ROBA_KEYMAP_MAP.md` if layer-related source changes occur
- this implementation plan
- build/flash notes if module initialization changes the build procedure

### Stage 2: Windows Read-Only Core

- Device discovery and selection.
- Battery service reader.
- Layer status reader.
- Reconnection state machine.
- Fresh/stale/unknown data states.
- Versioned local preferences.
- Unit tests for packet parsing, thresholds, freshness, and layer mapping.

The core must not depend directly on the UI so it can be exercised without a
taskbar session.

### Stage 3: Taskbar UX

- Composite icon renderer.
- Pinned-app-compatible application identity.
- Compact flyout with layer/right/left tiles.
- Accessible tooltip and high-contrast fallback.
- Background monitoring and explicit Quit command.
- Startup option.
- Battery threshold notifications.

### Stage 4: Hardware Validation

Validate on the actual roBa hardware:

- default and held layers;
- toggled layers;
- AML MOUSE activation and timeout;
- held SCROLL layer during trackball use;
- left/right battery changes;
- USB/BLE endpoint changes;
- sleep, resume, disconnect, and reconnect;
- ZMK Studio connection after status monitoring.

## Candidate Windows Foundations

### Option A: Extend zmk-battery-center

Advantages:

- already reads central and peripheral ZMK batteries;
- Tauri v2 application, installer, startup, notifications, history, and tests
  already exist;
- MIT licensed;
- active upstream development.

Risks:

- current UX is system-tray-oriented rather than pinned-taskbar-oriented;
- adding a roBa-specific custom layer service may not fit upstream scope;
- dynamic Windows taskbar icon behavior needs source-level verification;
- a fork creates an ongoing upstream merge obligation.

### Option B: New WPF Application

Advantages:

- direct Windows taskbar integration;
- compact native footprint and predictable Windows behavior;
- roBa-specific UX can remain small;
- straightforward separation between BLE core and UI.

Risks:

- both-battery discovery, reconnection, installer, startup, history, and
  notifications must be implemented or adapted;
- duplicates proven work from zmk-battery-center.

### Provisional Decision

Perform a short source-level reuse review first. Reuse its battery discovery
logic or contribute a generic layer-status extension if the architecture is a
good fit. Choose a new WPF client only if pinned-taskbar behavior is materially
awkward in the existing Tauri application.

## Verification Gates

No stage proceeds on assumption alone:

1. Static protocol review.
2. Windows battery read on real hardware.
3. USB layer-event proof.
4. BLE layer-event proof.
5. Taskbar icon readability review.
6. Full roBa hardware test matrix.

Firmware changes are not considered complete until both normal halves build,
the right-half UF2 is available for flashing, and existing trackball, scroll,
AML, and ZMK Studio behavior remain intact.

## Rollback

- Windows app: exit or uninstall; no keyboard state is changed.
- Firmware: disable the status module build flag and rebuild both halves.
- Transport: retain standard Battery Service support independently of the
  custom status service.
- UI experiment: keep icon rendering isolated so visual directions can be
  replaced without changing communication code.

## Completed Implementation

- WPF/.NET 8 application with a stable Windows executable identity.
- Dynamic composite taskbar icon with layer and two battery zones.
- Read-only Battery Service discovery for central and peripheral levels.
- Read + notify client for the roBa custom layer status service.
- Reconnection polling with fresh, stale, unknown, and disconnected states.
- Compact accessible status window and optional login startup.
- Framework-dependent single-file publish and local installer scripts.
- Central-only ZMK GATT service subscribed to layer and battery events.
- Packet parser and layer mapping automated tests.
- Successful Release build, publish, visual QA, and both-half ZMK builds.

## Remaining Hardware Work

- Flash the generated right-half firmware.
- If Windows uses a stale GATT cache, remove and re-pair roBa once.
- Confirm live DEFAULT, MOUSE, SCROLL, and CONFIGURATION transitions.
- Confirm central/right and peripheral/left battery labels on the real device.
- Confirm Studio still connects over its existing transport.

## v1.1 USB Transport Work

1. Add a second, vendor-defined HID interface to the right/central firmware.
2. Reuse the existing 12-byte status packet for USB input reports and control
   reads; do not reuse or modify the Studio CDC byte stream.
3. Add a Windows HID transport that discovers roBa by usage page, usage,
   vendor ID, and product ID.
4. Add a coordinator that prefers USB, releases BLE while USB is active, and
   falls back to BLE after cable removal.
5. Expose the selected transport in the existing connection label and tooltip.
6. Build both halves, run Windows tests, publish, and verify that the generated
   right firmware still contains Studio, keyboard/mouse HID, and status HID.

Assumptions:

- Only the right/central half is a supported USB status source.
- Windows 11 x64 is the acceptance target.
- The first matching roBa is selected; multiple simultaneous keyboards remain
  outside scope.
- USB transport is read-only and does not persist data or require Undo.
- Charging-state detection remains outside this increment.

Implementation result:

- Dedicated `HID_1` vendor collection added without changing the Studio CDC
  framing or normal keyboard/mouse reports.
- Initial state is available through HID `GET_REPORT`; subsequent layer and
  battery changes use interrupt input reports.
- The Windows client uses SetupAPI and HID APIs directly, avoiding package
  capability requirements and third-party dependencies.
- USB-first/BLE-fallback coordination, disconnect recovery, and transport labels
  are implemented.
- Windows Release build and 10 automated tests passed; both firmware halves
  built successfully. Live hardware verification remains pending.
