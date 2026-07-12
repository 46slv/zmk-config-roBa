# Scroll Inertia Lab

## Purpose

This branch isolates whether `mjmjm0101/zmk-input-processor-scroll-inertia`
can produce usable inertia on roBa without the production trackball stack.

The production branch had too many interacting parts for a clean answer:
auto mouse layer, pointer acceleration, mouse gesture, scroll snap, extra
horizontal-wheel suppression, RGB/charge modules, and PMW3610 smart behavior.

## Branch and Workspace

- Branch: `codex/scroll-inertia-lab`
- Windows worktree:
  `C:\Users\shiro\Documents\GitHub\zmk-config-roBa-inertia-lab`
- WSL build config:
  `~/zmk-workspace/config/zmk-config-roBa-inertia-lab`

## What Was Removed

- Trackball input path:
  - `zip_mouse_gesture`
  - `pointer_accel`
  - auto mouse layer processor chain
  - `zip_temp_layer` AML entry
  - `zip_scroll_snap`
  - `disable-scroll-x`
  - custom `wheel_x_scaler`
- PMW3610 right-hand config:
  - `CONFIG_PMW3610_AUTOMOUSE_TIMEOUT_MS`
  - `CONFIG_PMW3610_SMART_ALGORITHM=y`
  - right-hand EC11 settings
- Manifest modules:
  - `zmk-listeners`
  - `zmk-rgbled-widget`
  - `zmk-feature-charge-indicator`
  - `zmk-mouse-gesture`
  - `zmk-scroll-snap`
  - `zmk-pointing-acceleration`
- Non-scroll hardware/module noise:
  - RGB widget include/config
  - charge indicator config and `custom,chg-stat` node

## What Remains

The right-hand scroll layer keeps only the article-style inertia chain:

```dts
<&zip_y_scaler (-1) 1>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 675>;
```

`scroll_inertia_v` remains:

```dts
axis = <1>;
layer = <11>;
scale = <4>;
scale-div = <675>;
tick = <8>;
```

## Lab 2: Restore Active Scroll Scale

Lab 1 result: active scrolling did not work with the article-style
`scroll_inertia_v -> zip_scroll_scaler 4 675` chain, even after removing the
production stack.

Lab 2 keeps the lab branch minimal, but changes the scroll chain back toward
the known-working active scroll scale:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>,
<&scroll_inertia_v>;
```

`scroll_inertia_v` is intentionally exaggerated for visibility:

```dts
layer = <(-1)>;
scale = <8000>;
scale-div = <1000>;
start = <1>;
move = <1>;
min-events = <1>;
decel-samples = <1>;
```

Interpretation:

- If active scrolling returns, the article-style `4/675` scaling is too small
  for roBa's current event stream.
- If active scrolling returns but inertia is still absent, the problem is not
  the removed production stack. Focus on whether `scroll_inertia_v` arms or
  whether its direct HID output is effective.
- If active scrolling still does not return, the lab removal changed another
  required piece of roBa's scroll path.

## Lab 3: Remove Inertia Processor From The Chain

User result for Lab 2: active scroll itself still did not work.

Lab 3 keeps the same reduced branch and same known-working active scroll scale,
but removes `scroll_inertia_v` from the active scroller chain:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>;
```

`scroll_inertia_v` is still defined in the devicetree, but it is not referenced
by the input listener.

Interpretation:

- If active scrolling returns, `scroll_inertia_v` is blocking or consuming the
  normal scroll event path when inserted into the chain.
- If active scrolling still does not return, the lab branch has removed another
  required part of roBa's scroll path and should be compared against production
  before continuing inertia-specific tests.

## Build Results

Built from WSL after syncing this worktree to:
`~/zmk-workspace/config/zmk-config-roBa-inertia-lab`.

Output directory:
`\\wsl.localhost\Ubuntu-24.04\home\shiro\zmk-workspace\firmware\zmk-config-roBa-inertia-lab`

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 10:44.
- `roBa_L-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 10:45.

## Test Interpretation

- If this build scrolls with inertia, the module works on roBa hardware and the
  production problem is an interaction with the removed stack.
- If this build scrolls but still has no inertia, the next likely issue is the
  module arming/tick output with roBa's PMW3610 event stream.
- If this build does not scroll, the article-style chain itself is incompatible
  with roBa's current scroll signal scale/orientation and should be adjusted in
  the lab before being reintroduced to production.
