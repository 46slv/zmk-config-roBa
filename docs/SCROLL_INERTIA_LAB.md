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

