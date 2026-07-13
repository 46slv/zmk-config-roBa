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

- Tested on the right half on 2026-07-12.
- Overall behavior is close to acceptable.
- Very fast flicks still have a small active-to-coast speed step, but the
  remaining difference is within a range the user can accommodate.
- Proceed to small-flick sensitivity without changing EMA again.

## Lab 15: Smaller Flick Arming Thresholds

Lab 15 changes the two physical arming thresholds as one coupled sensitivity
adjustment:

```dts
start = <12>;
move = <20>;
```

Compared with Lab 14 (`start=16`, `move=32`), this allows a lower peak speed and
shorter cumulative roll to qualify as an intentional flick.

Unchanged:

- EMA `gain=500`, `blend=500`
- `min-events=4`, `release=24`, and `limit=600`
- Scale `4/75`, CPI 400, reversed direction, and production input stack
- Decay, friction, stop, and span

Test focus:

- A small deliberate flick should produce a short inertia tail more reliably.
- Ordinary slow positioning should not trigger unwanted coast.
- Very small noise or accidental ball contact should remain below the combined
  speed, movement, and four-event gates.
- Low-speed active-scroll dead-zone behavior is intentionally unchanged. If it
  remains objectionable, the next isolated stage changes matched scale from
  `4/75` to `4/60`.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 22:17.
- Generated devicetree confirms `start=12` and `move=20`.
- The left half was not rebuilt because this stage changes only right-hand
  inertia arming thresholds.

Hardware result:

- Pending user flash/test.

## Lab 16: Snap Before Two-Axis Inertia

Lab 16 keeps all Lab 15 sensitivity, EMA, scale, direction, CPI, and production
input-stack settings. It adds one integration experiment: restore scroll snap
before inertia and allow inertia to track either selected wheel axis.

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_snap>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 75>;
```

```dts
&zip_scroll_snap {
    require-n-samples = <2>;
    immediate-snap-threshold = <200>;
    lock-duration-ms = <175>;
    lock-for-next-n-events = <8>;
    idle-reset-timeout-ms = <175>;
};

scroll_inertia_v {
    axis = <0>;
};
```

Rationale:

- Snap must run first so inertia measures only the selected vertical or
  horizontal axis. Its direct HID coast output then continues that selected
  axis even though it bypasses downstream input processors.
- Putting snap after inertia would let active input snap while direct-HID coast
  could remain diagonal, producing an incoherent handoff.
- The earlier two-sample snap settings minimize, but do not eliminate, the
  sampling delay before active movement reaches inertia.

Test focus:

- Vertical and horizontal gestures should select the intended axis.
- The coast direction should match the active snapped direction.
- A diagonal gesture should choose one axis rather than coast diagonally.
- Compare initial low-speed and very short movement against Lab 15. Revert Lab
  16 if snap makes the existing dead zone materially worse.
- Check rapid axis reversals and leaving layer 11 for stale direction locks or
  lingering coast.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12.
- Generated devicetree confirms the layer-11 chain is transform, mapper,
  `zip_scroll_snap`, `scroll_inertia_v`, and scaler `4/75` in that order.
- Generated devicetree confirms the low-latency snap settings and inertia
  `axis=0`; all Lab 15 inertia parameters remain unchanged.
- Output:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`
- The left half was not rebuilt because this experiment changes only the
  right-hand trackball input path.

Hardware result:

- Pending user flash/test.

### Lab 16a: Correct Swapped Physical Axes

Initial Lab 16 hardware result:

- Physical right produced up scroll.
- Physical up produced right scroll.
- Physical left produced down scroll.
- Physical down produced left scroll.

This is an X/Y exchange, not an inertia or snap-direction failure. Lab 16a
changes only the transform flags:

```dts
<&zip_xy_transform INPUT_TRANSFORM_X_INVERT>,
```

`INPUT_TRANSFORM_XY_SWAP` is removed. Snap settings, inertia `axis=0`, Lab 15
parameters, scale, and every other input processor remain unchanged.

Expected result:

- Physical right/left controls horizontal scroll.
- Physical up/down controls vertical scroll.
- Snap still selects one of those axes before inertia.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 23:23.
- Generated devicetree confirms transform flags `0x2` (`X_INVERT` only), then
  mapper, snap, inertia, and scaler `4/75`.
- Output:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`

Hardware result:

- Pending user flash/test.

### Lab 16b: Reverse Both Scroll Directions

Lab 16a corrected the physical axis assignment, but hardware testing showed
that both horizontal and vertical directions were opposite the desired result.
Lab 16b changes only the transform flag:

```dts
<&zip_xy_transform INPUT_TRANSFORM_Y_INVERT>,
```

Relative to Lab 16a, removing `X_INVERT` reverses horizontal output and adding
`Y_INVERT` reverses vertical output. XY remains unswapped. Snap, inertia,
scaling, thresholds, and all other processors remain unchanged.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 23:32.
- Generated devicetree confirms transform flags `0x4` (`Y_INVERT` only), then
  mapper, snap, inertia, and scaler `4/75`.
- Output:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`

Hardware result:

- Tested by the user on the right half on 2026-07-12.
- Horizontal and vertical axes and directions are correct.
- Scroll snap and inertia operate together as intended.
- No material low-speed regression, coast-axis jump, stale axis lock, or other
  blocking defect was observed in the acceptance check.
- Lab 16b is the accepted baseline for the next experiment.

## Lab 17: Matched Low-Speed Output Scale

Lab 17 keeps the accepted Lab 16b snap, axes, directions, inertia thresholds,
EMA, and decay. It changes only the matched active/coast output ratio:

```dts
scroll_inertia_v {
    scale = <4>;
    scale-div = <60>;
};

<&zip_scroll_scaler 4 60>;
```

Compared with `4/75`, `4/60` produces 25 percent more output from the same
input. Matching both values preserves the active-to-coast speed relationship.
This does not lower the sensor or snap detection thresholds; it helps small
accumulated values reach a host-visible HID scroll unit sooner.

Test focus:

- Slow and short scroll movements should respond more consistently.
- Normal vertical and horizontal scrolling should not become too fast.
- Active-to-coast handoff should remain as smooth as Lab 16b.
- Snap direction, short-flick arming, and very fast flick behavior should not
  regress.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 23:55.
- Generated devicetree confirms both inertia scale and downstream scaler are
  `4/60`; snap, transform, and all inertia thresholds remain unchanged.
- Output:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`

Hardware result:

- Tested by the user on the right half on 2026-07-13.
- No problem was observed with low-speed response, normal speed, snap,
  active-to-coast handoff, or inertia behavior.
- Lab 17 is accepted; matched scale `4/60` becomes the new baseline.

## Lab 18: Approximate Original Pointer Acceleration

Lab 18 keeps the accepted Lab 17 scroll path unchanged and adjusts only the
normal pointer acceleration used on `DEFAULT` and `MOUSE`:

```dts
&pointer_accel {
    min-factor = <1000>;
    max-factor = <7000>;
    speed-threshold = <300>;
    speed-max = <3500>;
    acceleration-exponent = <1>;
};
```

The older production-like source placed the same acceleration processor in the
base listener and again in the `DEFAULT/MOUSE` child path. If both stages were
effective, their maximum multiplier composed to `2.8 * 2.8 = 7.84`. Restoring
that ambiguous two-stage topology would also risk accelerating layer-11 scroll
before snap and inertia.

This one-stage approximation caps at `7.0` and shifts the linear range to
`300..3500`. It stays near 1x for deliberate slow movement while approaching
the older composed response through medium and fast movement. The X/Y base
scalers remain `70/100` and `80/100`.

Test focus:

- Slow pointer positioning should remain controllable.
- Medium movement should cover more distance without requiring repeated rolls.
- A fast roll should approach the remembered maximum speed without jumping or
  becoming uncontrollable.
- Direction reversal should not produce an unexpected lurch.
- Scroll layer behavior must remain identical to accepted Lab 17.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-13 09:44.
- Generated devicetree confirms pointer factor `1000..7000`, threshold `300`,
  max speed `3500`, and exponent `1`.
- Generated devicetree also confirms the accepted Lab 17 scroll chain and
  matched `4/60` scale are unchanged.
- Output:
  `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2`

Hardware result:

- Pending user flash/test.

## Lab 19: Unified Axis-Lock Inertia Processor

Lab 19 implements the previously documented future candidate as a ZMK module
on branch `codex/unified-scroll-inertia`.

```dts
<&zip_xy_to_scroll_mapper>,
<&roba_scroll>;
```

Integration changes:

- The old snap/inertia projects are removed from `config/west.yml`.
- The unified implementation was published as
  `46slv/zmk-input-processor-roba-scroll` and is pinned to commit `c06c453`.
- Initial X/Y values are retained during selection and chosen-axis movement is
  flushed instead of discarded.
- Selection, `invert-y`, active scaling, velocity, and direct-HID coast share
  one device state and one `4/60` scale.
- Horizontal and vertical active remainders are independent.
- Axis changes cancel old coast before retained new-axis movement is tracked.
- Layer 11 off and endpoint changes reset pending input, remainders, velocity,
  timers, and coast.
- `axis-mode=1` provides explicit free two-axis input.
- `snap-switch` controls cross-axis movement required after lock expiry;
  `unlock-mod` resets into free 2D while held and cleanly re-enters snap mode.

Host verification:

- Inherited inertia math plus active-scale tests pass: `131/131`.
- New tests cover initial X/Y flush, immediate selection, cross-axis retention
  and switch threshold, idle reset, and free mode; all axis-lock tests pass.

Build result:

- Right and left UF2 builds succeed without the old external scroll modules.
- Generated right devicetree confirms `mapper -> roba_scroll`, accepted snap
  values, `invert-y`, `axis=0`, layer `11`, and scale `4/60`.
- Output directory:
  `~/zmk-workspace/firmware/zmk-config-roBa-unified`
- `roBa_R-seeeduino_xiao_ble.uf2`: `568320` bytes, built at
  2026-07-13 11:31 JST.
- `roBa_L-seeeduino_xiao_ble.uf2`: `357376` bytes, built at
  2026-07-13 11:34 JST.

Hardware result:

- Right-hand operation and feel accepted by the user on 2026-07-13.
- Lab 17 remains the pre-unified rollback baseline.

## Lab 20: Production Integration And External Pin

The accepted Lab 19 configuration was merged with
`codex/manage-existing-changes-20260713` on integration branch
`codex/main-unified-scroll`. This preserves the Windows status companion and
USB status transport while replacing the old production scroll experiment.

Packaging change:

- The reusable source, binding, Kconfig, and host tests moved to public module
  `46slv/zmk-input-processor-roba-scroll`.
- `config/west.yml` pins commit
  `c06c4530ed382f2aed5c7f19f006517e7d88fb7d`.
- The config repo keeps only the roBa node, listener chain, preset, and docs.
- Existing RGB widget, charge indicator, EC11, status transport, and Windows
  companion configuration are preserved from the production branch.
- Generated build input confirms both external
  `input_processor_roba_scroll.c` and local `src/roba_status.c` are linked into
  the right-hand firmware.

Verification:

- Standalone host tests: inertia/scale `131/131`; all axis-lock tests pass.
- `west list` resolves the external module to `c06c453`.
- Right and left pristine builds pass.
- Generated right DTS confirms `mapper -> roba_scroll`, `invert-y`, layer 11,
  scale `4/60`, snap `2/200/175/8/175`, EMA `500/500`, and arming `12/20/4`.
- The standalone module's invalid `board_root` metadata was removed before the
  final builds; that warning no longer appears.

Artifacts:

- Output: `~/zmk-workspace/firmware/zmk-config-roBa-main-unified`
- Right: `571392` bytes, SHA-256
  `864bcfdfcefacf96cbfd474edae73e9d255c43fbb1bdf91b387c05d7be12f8d6`
- Left: `358400` bytes, SHA-256
  `78bca9351193d2cbb529d472a255ca4511339b6e0608d312bd006e1562253e56`

Hardware result:

- The scroll module and preset are hardware accepted from Lab 19.
- This production-integrated build has not yet been flashed. Its additional
  differences are restored production peripherals/status features, not scroll
  tuning.

## Lab 21: Active-Only Low-Speed Boost

Lab 21 targets only deliberate slow scrolling. It keeps the accepted Lab 20
axis selection, direction, `4/60` base scale, EMA, flick arming, decay, and
coast settings unchanged.

```dts
active-low-speed-threshold = <20>;
active-low-speed-boost = <250>;
```

The maximum extra active scale is 25 percent at the smallest nonzero raw
delta. It decreases linearly and reaches zero at magnitude 20. The processor
applies this after axis selection and after inertia has consumed the original
raw delta for velocity tracking, so it does not change medium/high scrolling,
flick detection, or coast speed. Fractional boosted output remains accumulated
per axis instead of being discarded.

Module packaging:

- Standalone module PR: `46slv/zmk-input-processor-roba-scroll#1`.
- Pinned merge commit: `b0c28842c5469972bbe797065ace0e5d9104592b`.
- Host verification: `142/142` fixed-point checks and all axis-lock checks.
- `west list` resolves the module to the exact pinned merge commit.
- Pristine right and left ZMK builds pass.
- Generated right DTS confirms `threshold=20`, `boost=250`, and all accepted
  Lab 20 snap/inertia values unchanged.

Artifacts:

- Output: `~/zmk-workspace/firmware/zmk-config-roBa-low-speed`
- Right: `571904` bytes, SHA-256
  `ac1f007a823fb4669bb5647e48df37dbee6788f057d9e00c1a159dd38f161fe1`
- Left: `358400` bytes, SHA-256
  `78bca9351193d2cbb529d472a255ca4511339b6e0608d312bd006e1562253e56`

Hardware focus:

- Very slow continuous rolling should reach the first HID unit sooner.
- Medium/fast active speed and active-to-coast handoff should match Lab 20.
- Inertia duration, axis selection, and direction must remain unchanged.
- This exact firmware builds successfully but remains unverified on hardware
  until the right-half UF2 is flashed.

## Lab 22: Distance-Neutral Low-Speed Response And Reversal Fix

Hardware feedback on Lab 21 reported an over-input error sound and a pause
when weak input reversed an existing scroll. The two symptoms have separate
numeric causes:

- Lab 21's `boost=250` increases continuous active distance by up to 25 percent.
  It is disabled rather than reduced so the accepted `4/60` total is restored.
- Active fractional remainders were signed but did not track physical input
  direction. Reverse low-speed input first cancelled the old direction's
  remainder, producing several zero-output events. The module now records the
  direction per axis and clears only that axis's stale remainder on reversal.

Lab 22 uses:

```dts
active-low-speed-threshold = <20>;
active-low-speed-boost = <0>;
active-low-speed-eager = <500>;
```

`eager=500` uses nearest-unit quantization below the threshold. At the accepted
`4/60` scale, repeated raw `1` input reaches its first whole unit after about
8 events instead of 15. That early unit is borrowed: the remainder becomes
negative and later same-direction input repays it. Continuous total distance
therefore remains `4/60` rather than the increased Lab 21 gain.

The current ZMK `v0.3` smooth-scrolling layer also owns a host-profile-dependent
wheel remainder after input processors. Its profile was not observable in this
investigation, and it predates Lab 21, so no ZMK core or smooth-scroll setting
is changed here. If the error sound remains with Lab 22, capture whether it
occurs on USB, BLE, or both before changing that second layer.

Verification before hardware:

- Standalone module PR: `46slv/zmk-input-processor-roba-scroll#2`.
- Pinned merge commit: `0c7a8fe243d1182c090a138005d275795d7b206b`.
- Host verification: `162/162` fixed-point checks and all axis-lock checks.
- Clean WSL/Nix builds succeeded for both `roBa_R` and `roBa_L`.
- Generated right-half Devicetree contains `scale=4`, `scale-div=60`,
  `threshold=20`, `boost=0`, and `eager=500`.
- Medium/high active scale, snap, EMA, arming, friction, stop, and coast are
  unchanged from Lab 20.

Flash artifacts:

- Right: `/home/shiro/zmk-workspace/firmware/zmk-config-roBa-low-speed-reversal/roBa_R-seeeduino_xiao_ble.uf2`
  (`572416` bytes, SHA-256
  `bfc82d9ee3a5853f7acb667cd1addc1774f2a69909e7913f896febd47e624e87`).
- Left: `/home/shiro/zmk-workspace/firmware/zmk-config-roBa-low-speed-reversal/roBa_L-seeeduino_xiao_ble.uf2`
  (`358400` bytes, SHA-256
  `78bca9351193d2cbb529d472a255ca4511339b6e0608d312bd006e1562253e56`).
- Hardware flashing and live USB/BLE verification remain unperformed.

Hardware focus:

- Repeated very slow same-direction rolls must not trigger the error sound.
- A weak reverse roll must begin in the new direction without a dead interval.
- Medium/fast active scrolling and active-to-coast handoff must match Lab 20.
- Repeat the first two checks on both USB and BLE if possible.

## Superseded Design Rationale

Keep a purpose-built combined module on the development backlog. Its goal is
automatic vertical/horizontal selection with inertia, without the behavioral
gap created by simply chaining `zmk-scroll-snap` and the current inertia
processor.

Required behavior:

- Preserve low-speed and very short active input while deciding the intended
  axis; axis sampling must not silently consume the first movement events.
- Select vertical or horizontal from the original XY gesture, then carry that
  selected axis and its measured velocity into coast as one state machine.
- Keep active output and coast output on the same scale so release does not
  introduce a speed step.
- Support configurable axis ratios, lock hysteresis, unlock/idle timing,
  flick arming, decay, and per-axis direction inversion.
- Reset all lock and coast state when layer 11 is released or the endpoint
  changes.
- Keep diagonal/free-scroll mode available as an explicit option rather than
  an accidental result of bypassing snap during direct HID coast output.

Reason for considering a new module:

- `scroll-snap -> inertia(axis=0)` can make the coast follow the selected axis,
  but scroll-snap suppresses events during its sampling phase. That can worsen
  the existing low-speed/small-motion response issue.
- `inertia(axis=0) -> scroll-snap` is not coherent because the inertia timer
  sends synthetic HID reports directly; those reports do not pass through a
  downstream scroll-snap processor.
- A shared state machine can make axis choice, velocity estimation, active
  output, and coast handoff from the same event history.

Lab 19 implements this candidate. The rationale remains to explain why the
former serial processor chain was replaced.

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
