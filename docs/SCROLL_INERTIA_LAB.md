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
- Why this worked despite the integration mismatch: the modified PMW3610
  driver had already reduced scroll-layer motion to sparse `WHEEL/HWHEEL`
  events with values of `+1` or `-1`. Lab 6 used `start=0`, `move=1`, and
  `min-events=1`, so one such event was enough to arm the processor. After
  `release=24` ms without another quantized wheel event, the stop detector
  entered `COASTING`. With `decay=1000`, `friction=0`, and `stop=0`, velocity
  could not decrease; `span=6000` was therefore the effective stop condition.
- This validates the coasting scheduler and HID output mechanism, but not the
  intended velocity tracking quality. The processor was operating on a
  thresholded `+/-1` event stream rather than raw or magnitude-preserving
  scroll deltas.
- The same mismatch can explain the intermittent active-scroll symptom. While
  the ball is still moving, the PMW3610 driver's accumulation threshold can
  leave gaps longer than `release=24` ms. The inertia processor can interpret
  such a gap as release, enter `COASTING` early, and absorb later same-direction
  wheel events as physical tail input.
- The Lab 5 failure was not a missing processor invocation or broken HID path,
  but it cannot be attributed to decay alone. The quantized driver event stream
  also prevented meaningful velocity estimation under practical thresholds.

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

## Lab 10: Raw-Input Reference Integration

Lab 10 replaces the diagnostic integration with the data path documented by
the inertia module. This is a coherent reference configuration rather than a
single-parameter tuning step.

Driver ownership change:

- Remove `scroll-layers = <11>` from the PMW3610 node. The optional property now
  defaults to an empty list, so the modified driver emits raw `INPUT_REL_X/Y`
  on layer 11 instead of accumulating and quantizing motion into `+/-1` wheel
  events.
- The ZMK input-listener becomes the only owner of scroll conversion.

Active chain:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 675>;
```

Reference settings:

- `CONFIG_PMW3610_CPI=1000`, matching the module's documented calibration.
- `CONFIG_PMW3610_SMART_ALGORITHM=y`, restoring stable sensor tracking without
  adding a scroll processor.
- `axis=1` for dedicated vertical scrolling.
- `layer=11` so leaving the scroll layer immediately clears inertia state.
- `scale=4`, `scale-div=675`, matching the downstream scaler.
- `tick=8` for the 125 Hz PMW3610 polling rate.
- Diagnostic Lab 6/7 overrides are removed; the module defaults provide the
  initial `start`, `move`, event-count, decay, friction, stop, and span values.
- The inertia module revision is pinned to tested commit `f7dadef` so subsequent
  builds cannot silently change behavior.

Expected behavior:

- Active scroll receives magnitude-bearing deltas on every PMW3610 report and
  should no longer pause at the driver's old scroll threshold.
- Casual slow scrolling should remain in `TRACKING` and pass through directly.
- A deliberate flick should enter `COASTING` after deceleration or release and
  produce a fading vertical tail.
- Cursor behavior outside layer 11 is not part of this scroll-chain test.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 16:45.
- Output path:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`
- Generated devicetree verification:
  - PMW3610 node has no `scroll-layers` property.
  - Layer 11 chain resolves to transform, mapper, inertia, scaler `4/675`.
  - Inertia resolves to `axis=1`, `layer=11`, scale `4/675`, tick `8`.
- Generated Kconfig verification:
  - `CONFIG_PMW3610_CPI=1000`
  - `CONFIG_PMW3610_SMART_ALGORITHM=y`
  - `CONFIG_PMW3610_POLLING_RATE_125_SW=y`
- The left half was not rebuilt because the experiment changes only the
  right-hand trackball input path.

Hardware result:

- Tested on the right half on 2026-07-12.
- Active scrolling is continuous with the raw-input path.
- A software inertia tail is perceptible after release.
- The integration objective is therefore achieved: the PMW3610 driver no
  longer quantizes the input before the inertia processor.
- Active scroll speed is too slow at scale `4/675`; increase output scale next
  without changing CPI, arming, axis, or decay.

## Lab 11: Triple Active And Inertia Output Scale

Lab 11 keeps the working Lab 10 raw-input integration and changes only the
matched active/inertia output ratio:

```dts
scroll_inertia_v {
    scale = <4>;
    scale-div = <225>;
};

<&zip_scroll_scaler 4 225>;
```

`4/225` is exactly three times `4/675`. Matching both locations preserves the
handoff between direct active scroll and the inertia module's direct HID coast.

Unchanged:

- Raw X/Y input on layer 11; PMW3610 `scroll-layers` remains omitted.
- `CONFIG_PMW3610_CPI=1000`
- `CONFIG_PMW3610_SMART_ALGORITHM=y`
- `axis=1`, `layer=11`, and `tick=8`
- Module-default arming, decay, friction, stop, and span parameters

Acceleration decision:

- Do not add the existing `pointer_accel` processor to this scroll chain. It
  would alter the values used by the inertia detector and can make slow motion
  look like a flick.
- First determine whether the 3x linear scale plus inertia provides enough
  range. If speed-dependent acceleration is still required, add it as a
  scroll-specific output policy that does not modify the raw values observed by
  inertia.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 16:55.
- Output path:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`
- Generated devicetree confirms both the downstream scaler and inertia
  `scale-div` resolve to `225`.
- The left half was not rebuilt because this changes only the right-hand scroll
  output scale.

Hardware result:

- Pending user flash/test.

## Lab 12: Production-Like Trackball Stack

Lab 12 applies the working raw-input inertia path to the original roBa
trackball feature set and reverses the vertical scroll direction.

Restored from the comfortable keymap:

- Cursor-only `pointer_accel`
- Auto mouse layer with X/Y cursor scaling and `zip_temp_layer`
- Mouse gesture input and the right-click gesture binding
- Horizontal-wheel suppression on `EXTRA_FINCTIONS`
- PMW3610 `CPI=400` and smart behavior

Intentionally not restored:

- PMW3610 `scroll-layers`; raw X/Y must continue to reach the listener.
- `zip_scroll_snap`; vertical `axis=1` already provides deterministic intent.
- Scroll-chain pointer acceleration; it would distort inertia detection.
- RGB, charge-indicator, and encoder modules unrelated to trackball input.

Scroll integration:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 75>;
```

Removing `INPUT_TRANSFORM_Y_INVERT` reverses vertical scroll relative to Lab
10/11 while preserving the transformed horizontal direction.

The module defaults were calibrated at 1000 CPI. For the restored 400 CPI
cursor baseline, physical thresholds are scaled to 40 percent:

```dts
start = <16>;
move = <32>;
friction = <14>;
stop = <3>;
```

The base listener runs mouse gesture only. `pointer_accel` is referenced inside
the `DEFAULT/MOUSE` conditional child so layer 11 raw X/Y reaches the scroller
without acceleration.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 17:32.
- `roBa_L-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 17:33.
- Generated right-hand devicetree confirms transform flags `0x3`, matched
  scale `4/75`, the CPI-adjusted inertia values, AML, and horizontal-wheel
  suppression.
- Generated right-hand Kconfig confirms `CPI=400` and PMW3610 smart behavior.

Hardware result:

- Tested on the right half on 2026-07-12.
- Medium-speed gestures produce the strongest perceived inertia.
- Very fast flicks either stop or visibly slow almost to a halt before a coast
  begins at roughly the same speed as a medium gesture.
- This indicates two likely gates: the default `min-events=10` can reject a
  short fast flick before arming, and the default `gain=300` / `blend=700` EMA
  can under-estimate a short high-speed gesture at the handoff.

## Lab 13: Arm Short Fast Flicks

Lab 13 changes one inertia state-machine parameter:

```dts
min-events = <4>;
```

At 125 Hz, the default `10` events requires about 80 ms of tracked input before
inertia may arm. A short, fast flick can end before that gate is met even when
`start` and `move` are exceeded. Four events reduce the gate to about 32 ms and
remain inside the module's documented recommended range of 3 to 30.

Unchanged:

- `gain=300`, `blend=700`, and `release=24`
- `limit=600`
- CPI-adjusted `start=16`, `move=32`, `friction=14`, and `stop=3`
- Raw input path, reversed direction, scale `4/75`, and production input stack

Test focus:

- A short fast flick should now produce inertia instead of stopping.
- Compare the initial coast speed of medium and fast flicks separately.
- A brief pause before coast may remain because `release=24` and EMA response
  are intentionally unchanged in this stage.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 20:54.
- Generated devicetree confirms `min-events=4`.
- The left half was not rebuilt because this stage changes only a right-hand
  inertia parameter.

Hardware result:

- Tested on the right half on 2026-07-12.
- Lowering `min-events` made fast flick inertia reliable enough to be usable.
- At very high flick speed, a visible drop to a slower coast remains, although
  the user considers it close to acceptable with deliberate technique.
- This isolates the remaining experiment to EMA response rather than arming.

## Lab 14: Faster EMA Handoff

Lab 14 changes the EMA weighting as one coupled filter parameter:

```dts
gain = <500>;
blend = <500>;
```

The sum remains 1000 as required by the module. Compared with the default
`300/700`, each event contributes more immediately to estimated velocity. A
short high-speed flick should therefore enter `COASTING` closer to its active
speed instead of dropping toward the medium-speed estimate.

Unchanged:

- `min-events=4`, `release=24`, and `limit=600`
- CPI-adjusted thresholds and decay
- Raw input path, reversed direction, scale `4/75`, and production input stack

Test focus:

- Compare the active-to-coast speed step on very fast flicks.
- Check that medium flicks do not become excessively strong.
- Check that small speed fluctuations during normal scrolling do not make
  inertia feel nervous or trigger too aggressively.
- The separate low-speed short-movement dead zone is not targeted by this
  build.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 21:54.
- Generated devicetree confirms `gain=500` and `blend=500`.
- The left half was not rebuilt because this stage changes only a right-hand
  inertia filter parameter.

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
