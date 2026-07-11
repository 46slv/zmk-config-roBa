# Scroll Inertia Research

Date: 2026-07-11

This report records research for adding trackpad-like inertial scrolling to the
roBa ZMK configuration. The first vertical-only experiment is now wired into the
configuration.

## Current roBa State

- ZMK is pinned to `v0.3` in `config/west.yml`.
- Scroll mode is `SCROLL` / layer `11`.
- `&trackball` uses `scroll-layers = <11>;`.
- Current scroll processor chain:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 1>,
<&zip_scroll_snap>
```

- Current right-hand shield scroll setting:

```conf
CONFIG_ZMK_POINTING_SMOOTH_SCROLLING=y
CONFIG_PMW3610_SCROLL_TICK=4
```

The low-speed scroll tuning is currently reported as acceptable.

## What ZMK Provides By Default

ZMK input processors can scale, transform, map movement to scroll, and activate
temporary layers. ZMK also allows custom input processors through external
modules.

Important distinction:

- `CONFIG_ZMK_POINTING_SMOOTH_SCROLLING=y` enables high-resolution HID scroll
  behavior through HID resolution multipliers.
- It does not itself create inertia. Inertia requires firmware to keep emitting
  decaying wheel events after physical motion stops.

Sources:

- https://zmk.dev/docs/keymaps/input-processors
- https://zmk.dev/docs/keymaps/input-processors/usage
- https://zmk.dev/docs/keymaps/input-processors/code-mapper
- https://zmk.dev/docs/config/pointing
- https://zmk.dev/docs/keymaps/behaviors/mouse-emulation

## Candidate Modules

### 1. `mjmjm0101/zmk-input-processor-scroll-inertia`

URL:

- https://github.com/mjmjm0101/zmk-input-processor-scroll-inertia

Fit for roBa:

- Strong candidate.
- It is specifically for iOS-style inertial scrolling on ZMK trackballs.
- It watches scroll events, detects a flick/release, and emits a fading scroll
  tail.
- The README says it was tested against ZMK `v0.3.0`, matching roBa's current
  ZMK pin.
- It is scroll-focused, which matches the current goal better than a general
  cursor+scroll inertia module.

Important behavior notes:

- It is not a scroll acceleration processor. It should be treated as inertia
  only.
- It recommends placing the inertia processor before `zip_scroll_scaler`, while
  matching the downstream scaler settings in the inertia node.
- It warns against having pointer acceleration in the scroll chain because
  accelerated slow movement can be misread as a flick. roBa currently does not
  have `&pointer_accel` in the `scroller` chain, which is good.
- It emphasizes explicit axis intent. The README recommends separate vertical
  and horizontal scroll layers, or modifier-controlled axis swap/unlock, because
  automatic axis guessing is unreliable around diagonals, reversals, slow drift,
  and the handoff from active scroll to inertia.
- It warns not to run multiple inertia implementations at the same time.

Potential conflict with current roBa setup:

- roBa currently uses one `SCROLL` layer plus `zip_scroll_snap` for axis locking.
- The inertia module's model prefers deterministic axis selection before/inside
  inertia, not post-hoc snap guessing.
- `zip_scroll_snap` may need to be removed, moved, or replaced by the inertia
  module's axis handling for the inertia version.

Likely first implementation shape:

1. Add remote/project to `config/west.yml`.
2. Include the module's DTS include.
3. Add one vertical inertia node for layer `SCROLL`.
4. Start without pointer acceleration in the scroll chain.
5. Test with `zip_scroll_snap` removed from the inertia experiment branch, or
   create a separate temporary scroll layer for inertia testing.
6. Keep `CONFIG_ZMK_POINTING_SMOOTH_SCROLLING=y`, but expect to retune scale,
   stop, and tail settings.

Candidate chain concept for a first test:

```dts
input-processors =
    <&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
    <&zip_xy_to_scroll_mapper>,
    <&scroll_inertia_v>,
    <&zip_scroll_scaler 4 1>;
```

This is only a concept. The real parameters must follow the module's binding
and build output.

### 2. `amgskobo/zmk-input-inertia`

URL:

- https://github.com/amgskobo/zmk-input-inertia

Fit for roBa:

- Possible, but less targeted for this exact goal.
- It supports inertial movement and scrolling for relative input events.
- It has separate movement and scrolling decay/report/start/stop settings.
- It says it is Zephyr 4.1 compatible. roBa is pinned to ZMK `v0.3`, so
  compatibility would need to be verified before adopting it.

Important behavior notes:

- The README says to place `&zip_inertia` at the end of the processor list
  because it emits synthesized inertia events directly to HID and bypasses later
  processors.
- It recommends using the same `&zip_inertia` node for cursor and scroll chains
  so one mode can stop the other's inertia naturally.
- It recommends setting `trigger-ms` to at least 2x the sensor polling interval
  to avoid false release detection caused by packet timing variance.

Potential conflict with current roBa setup:

- It is broader than needed: cursor inertia may be unwanted.
- Placement advice differs from the mjm module. This means the two modules are
  not interchangeable; their chain order must follow the chosen module.
- Because roBa already uses external modules and a split keyboard, build-side
  compatibility must be checked before any firmware change.

## Recommendation

Use `mjmjm0101/zmk-input-processor-scroll-inertia` as the first serious
candidate.

Reasons:

- It targets exactly "trackball scroll inertia".
- It is tested against ZMK `v0.3`, matching this repo.
- It provides guidance for scroll-layer axis handling, which is the main hard
  part of trackball inertia.
- It aligns with the user's goal: trackpad-like scroll tail after a fast flick,
  not cursor inertia.

Do not add it directly to the main scroll behavior without an experiment branch
or reversible commit. The likely risky area is interaction with
`zip_scroll_snap`; inertia and snap both try to reason about scroll direction.

## Suggested Experiment Plan

1. Preserve current working low-speed scroll settings:
   - `CONFIG_PMW3610_SCROLL_TICK=4`
   - `zip_scroll_snap.require-n-samples=<2>`
   - `xy_to_scroll_mapper -> scroll_scaler -> scroll_snap`

2. Create an inertia experiment:
   - Add the `mjmjm0101` module in `config/west.yml`.
   - Add the required include and inertia node.
   - Create a minimal vertical-only inertia test first.

3. Keep variables controlled:
   - Do not add scroll acceleration.
   - Do not add pointer acceleration to the scroll chain.
   - Initially test either inertia without `zip_scroll_snap`, or use a separate
     temporary layer so the current scroll layer remains easy to restore.

4. Test on hardware:
   - Slow scroll still responds immediately.
   - Fast flick produces a tail.
   - The tail stops naturally.
   - Direction does not get stuck after diagonal input.
   - Middle-click combo and scroll-layer exit still work.
   - BLE and wired HID behavior both work after descriptor/settings refresh.

## Open Questions Before Implementation

- Should inertia be vertical-only first, or should horizontal scroll be available
  through a modifier?
- Should the existing `SCROLL` layer be changed, or should a temporary
  experimental layer be added first?
- Should `zip_scroll_snap` be removed in the inertia chain, or kept for a direct
  A/B test?
- Does the current `zmk-pmw3610-driver` fork have any built-in inertia or
  scroll smoothing behavior that must be disabled before adding a module?

## Current Experiment

The first implementation uses `mjmjm0101/zmk-input-processor-scroll-inertia` as
a narrow vertical-only experiment:

- `config/west.yml` adds remote/project `mjmjm0101/zmk-input-processor-scroll-inertia`.
- `boards/shields/roBa/roBa.dtsi` defines `scroll_inertia_v` as disabled so the
  shared keymap can reference the label.
- `boards/shields/roBa/roBa_R.overlay` enables `scroll_inertia_v` only on the
  central/right build.
- `config/roBa.keymap` inserts `&scroll_inertia_v` before
  `&zip_scroll_scaler 4 1`.
- `axis = <1>` keeps inertia vertical-only for the first test.
- `layer = <11>` resets inertia when the `SCROLL` layer turns off.
- `scale = <4>` and `scale-div = <1>` match the downstream
  `zip_scroll_scaler 4 1`.
- `tick = <8>` matches the PMW3610 125 Hz polling interval.

`zip_scroll_snap` remains in the chain for this first test so existing active
scroll behavior changes as little as possible. Treat `zip_scroll_snap` as the
main integration risk if inertia feels sticky, delayed, or axis-confused.
