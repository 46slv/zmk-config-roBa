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

User result for Lab 4: active scrolling works, but inertia is still absent.

Conclusion for Lab 4:

- The earlier no-scroll behavior was likely caused by vertical-only axis
  filtering (`axis = <1>`) suppressing roBa's effective wheel axis.
- `scroll_inertia_v` can pass through active scroll when configured as
  `axis = <0>`.
- The remaining problem is now inertia arming/output, not basic pass-through.

## Lab 5: Recommended Processor Placement With roBa Scale

Lab 5 keeps `axis = <0>`, but moves `scroll_inertia_v` before
`zip_scroll_scaler`, matching the module's binding recommendation. Instead of
using the article's small `4/675` scaler, it uses roBa's known active scroll
scale:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 1>;
```

The inertia node mirrors the downstream scaler:

```dts
axis = <0>;
scale = <4>;
scale-div = <1>;
start = <1>;
move = <1>;
min-events = <1>;
decel-samples = <1>;
```

Interpretation:

- If inertia appears, the module needed to see pre-scaler values and the prior
  post-scaler placement was preventing reliable arming.
- If active scrolling works but inertia is still absent, the issue is likely
  inside arming/release detection or the module's direct HID output path.
- If active scrolling breaks again, the recommended pre-scaler placement is
  incompatible with this reduced roBa chain unless more axis/code mapping is
  added before the scaler.

Result on hardware:

- Active scrolling works.
- No inertia is perceptible.
- This rules out processor placement alone as the cause. The next test must
  distinguish an effect that decays too quickly from failure to enter or emit
  the coasting state.

## Lab 6: Non-Decaying Inertia Diagnostic

Lab 6 keeps the working Lab 5 processor chain and makes any successfully armed
coast deliberately persistent and obvious:

```dts
start = <0>;
move = <1>;
min-events = <1>;
release = <24>;
decay-fast = <1000>;
decay-slow = <1000>;
decay-tail = <1000>;
friction = <0>;
stop = <0>;
span = <6000>;
```

This is a diagnostic configuration, not a usable final feel. Once armed, the
velocity should remain for up to six seconds. Therefore:

- If a long coast appears, earlier builds were arming but their effective
  output decayed or stopped before it became perceptible.
- If active scrolling still works but no coast appears, parameter strength is
  no longer a credible explanation. The remaining fault is in release/arming
  or the module's direct HID report path.
- If scrolling becomes stuck or runaway, unplug/reflash with Lab 5; that result
  still proves the coasting output path is active.

Lab 6 build results:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 12:25.
- `roBa_L-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 12:26.

Result on hardware:

- Active scrolling works.
- After the trackball is released, scrolling continues in the same direction
  for close to the configured six-second span.
- This is the intentionally non-decaying Lab 6 coast. It proves that release
  detection, the transition to coasting, scheduled ticks, and direct HID
  scroll reports all work on roBa.
- The Lab 5 failure was therefore an imperceptible effective decay/output
  problem, not a missing processor invocation or broken HID path.

## Lab 7: Practical Single-Curve Decay

Lab 7 restores a stopping threshold and uses a gentle single decay curve:

```dts
start = <1>;
decay-fast = <995>;
decay-slow = <995>;
decay-tail = <995>;
friction = <5>;
stop = <1>;
span = <2000>;
```

At an 8 ms tick, `995` retains 99.5% of velocity per tick. The intended result
is a clearly visible but steadily weakening coast, with a two-second safety
cap. This remains a tuning build: the next decision is based on whether the
coast feels too long, too short, or appropriately trackpad-like.

Lab 7 build results:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 12:32.
- `roBa_L-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 12:33.

Handoff observation:

- In the current reduced lab lineage, active scrolling is choppy and
  intermittent: some trackball movement scrolls and some does not.
- The trackpoint-style scroll operation shows the same issue.
- Before further inertia feel tuning, restore a smooth, continuously responsive
  active scroll baseline while preserving the Lab 6 proof that inertia works.
- Continue from `docs/SCROLL_INERTIA_HANDOFF.md` in a fresh task.

## Lab 8: Raise PMW3610 CPI Only

The failed scroll-snap restoration experiment is not included in this branch.
Lab 8 returns to the Lab 7 processor chain and changes only the PMW3610 sensor
resolution:

```conf
CONFIG_PMW3610_CPI=3200
```

Reason:

- Earlier roBa history used `CONFIG_PMW3610_CPI=3200` together with a low
  scroll tick before later changing CPI to `400`.
- The reduced inertia lab also has `CONFIG_PMW3610_SMART_ALGORITHM=n`, so it no
  longer has the production driver's smart behavior smoothing the low-speed
  event stream.
- If the active scroll path is starving the inertia processor of fine-grained
  movement events, raising CPI should improve both continuous active scrolling
  and coast arming without changing inertia parameters.

Not changed in Lab 8:

- `CONFIG_PMW3610_SCROLL_TICK` remains `4`.
- `scroll_inertia_v.axis = <0>` remains unchanged.
- `scroll_inertia_v` remains before `zip_scroll_scaler`.
- `zip_scroll_snap`, auto mouse layer, pointer acceleration, mouse gesture, and
  horizontal wheel suppression remain removed.

Test focus:

- Layer-11 active scroll should respond to smaller trackball movements.
- Trackpoint-style scroll should become less intermittent.
- Inertia should still coast after release; if it does not, CPI alone is not the
  missing condition.

Lab 8 build results:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 15:23.
- Output path:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`
- Left-hand firmware was not rebuilt because this step changes only the
  right-hand PMW3610 sensor configuration.

Hardware result:

- Tested on the right half on 2026-07-12.
- Raising CPI from `400` to `3200` increased scroll speed, but did not restore
  smooth, continuous response.
- Even when the trackball was moved smoothly, scrolling sometimes stopped
  completely or emitted only a very small movement before responding again.
- A screen recording of the test showed visible pauses followed by larger
  movement. This is not explained by a speed or acceleration-curve change
  alone.
- Therefore, the cause is present somewhere in the current inertia-lab changes
  relative to the comfortable production version. Do not treat this result as
  evidence of a mechanical PMW3610 problem.
- Relevant lab differences include `CONFIG_PMW3610_SMART_ALGORITHM=n`, removal
  of pointer remainder/acceleration and the production listener stack, and the
  active `scroll_inertia_v` state machine.
- In particular, `scroll_inertia_v` can suppress same-direction input while it
  is in `COASTING`. The current diagnostic thresholds (`start=1`, `move=1`,
  `min-events=1`, `decel-samples=1`) make an early transition into that state a
  plausible explanation for smooth physical movement producing intermittent
  HID scroll output. This remains a hypothesis, not a confirmed root cause.
- The next decisive one-element test is to keep the Lab 8 sensor settings and
  bypass only `scroll_inertia_v`. If continuous scrolling returns, the failure
  is in the inertia processor path rather than CPI or physical sensor motion.

## Lab 9: Bypass Inertia With Lab 8 Sensor Settings

Lab 9 keeps every Lab 8 sensor and scroll-path setting except for one change:
`scroll_inertia_v` is removed from the active scroller chain.

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>;
```

Unchanged:

- `CONFIG_PMW3610_CPI=3200`
- `CONFIG_PMW3610_SMART_ALGORITHM=n`
- `CONFIG_PMW3610_SCROLL_TICK=4`
- `zip_scroll_scaler 4 1`
- The reduced listener stack remains in place.
- The inertia node and Lab 7 parameters remain defined and enabled, but no
  active input-listener references the processor.

Purpose:

- Determine whether the intermittent response seen in the Lab 8 video is
  introduced by the active inertia processor path.
- Do not evaluate inertia feel in this build; no software coast is expected.

Hardware decision:

- If smooth physical movement becomes continuous, investigate
  `scroll_inertia_v` state transitions, axis merging, and event suppression.
- If pauses remain, the cause is elsewhere in the reduced lab changes. Restore
  the production PMW3610 smart behavior next, one element at a time.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 15:54.
- Output path:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`
- The left half was not rebuilt because this test changes only the right-hand
  trackball input chain.

Hardware result:

- Pending user flash/test.

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
