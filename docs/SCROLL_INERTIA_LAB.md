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

User result for Lab 3: active scrolling returned.

Additional observation: trackball pointer movement felt less responsive and
more uneven starting from the reduced lab builds. This is expected for the lab
configuration because it intentionally removed the production pointer path:

- `pointer_accel` is removed from the trackball listener.
- `CONFIG_PMW3610_SMART_ALGORITHM` is set to `n`.
- Auto mouse layer processing and its pointer scaling are removed.
- Mouse gesture and other production helper modules are removed.

Conclusion for Lab 3:

- The base reduced scroll path works when `scroll_inertia_v` is not referenced.
- The current scroll failure is therefore strongly tied to inserting
  `scroll_inertia_v` into the active input processor chain.
- Pointer quality in this lab should not be treated as representative of the
  production keymap until pointer acceleration and PMW3610 smart behavior are
  restored.

## Lab 4: Axis-Mismatch Diagnostic

Lab 4 returns `scroll_inertia_v` to the active chain, but changes its axis from
vertical-only to both axes:

```dts
axis = <0>;
```

Reason:

- In single-axis mode, the module treats the other wheel axis as cross-axis
  input and sets `event->value = 0`.
- Lab 3 proved the reduced `transform -> mapper -> scaler` scroll path works
  without the inertia processor.
- Therefore, if roBa's effective scroll output reaches `scroll_inertia_v` as
  `INPUT_REL_HWHEEL` while the inertia node is configured as `axis = <1>`, the
  inertia processor itself will suppress normal scrolling.

Interpretation:

- If active scrolling returns with `axis = <0>`, the previous no-scroll result
  was likely an axis/code mismatch, not a general pass-through failure.
- If active scrolling still fails with `axis = <0>`, the issue is more likely
  caused by coasting/arming state or the module's handling after the scaler.
- If inertia appears, continue by finding the correct single-axis setting
  (`axis = <1>` vs `axis = <2>`) and/or by moving the processor back before the
  scaler with matching `scale`/`scale-div`.

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
