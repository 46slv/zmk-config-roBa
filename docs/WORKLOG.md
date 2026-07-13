# Worklog

## 2026-07-11: Trackball Scroll Inertia Experiments

### Goal

Improve trackball scrolling so slow movement responds well and fast flicks can
produce trackpad-like inertia.

### Baseline

- `SCROLL` is layer `11`.
- `&trackball` uses `scroll-layers = <11>`.
- Low-speed scroll with `CONFIG_PMW3610_SCROLL_TICK=4` was reported as good.
- The known-working scroll chain before inertia experiments was:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>,
<&zip_scroll_snap>;
```

### Inertia Module Research

Added `mjmjm0101/zmk-input-processor-scroll-inertia` to `config/west.yml`.
The module expects scroll events, should run on the central side of a split
keyboard, and recommends this chain shape:

```dts
<&zip_y_scaler (-1) 1>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 675>;
```

The module guidance says `scale` and `scale-div` on the inertia node should
match the downstream `zip_scroll_scaler` arguments when the processor is placed
before that scaler.

### Experiment 1

Configured article-style vertical inertia on layer `11`:

```dts
axis = <1>;
layer = <11>;
scale = <4>;
scale-div = <675>;
tick = <8>;
```

Result: build and flash succeeded, but hardware did not show inertia.

### Experiment 2

Kept the article-style chain and lowered trigger thresholds:

```dts
start = <10>;
move = <20>;
min-events = <3>;
decel-samples = <1>;
```

Result: hardware still did not scroll. This suggested the issue was not only
strict inertia trigger parameters.

### Experiment 3

Changed to preserve roBa's known-working active scroll amount first, then place
inertia after it:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>,
<&scroll_inertia_v>;
```

Because the inertia processor is now after the active scroll scaler, its output
scale was reset:

```dts
scale = <1000>;
scale-div = <1000>;
```

Trigger thresholds remain intentionally low for testing:

```dts
start = <10>;
move = <20>;
min-events = <3>;
decel-samples = <1>;
```

### Build Results

Local WSL/Nix builds succeeded for both sides after Experiment 3:

```text
roBa_R-seeeduino_xiao_ble.uf2  551K  2026-07-11 22:31
roBa_L-seeeduino_xiao_ble.uf2  350K  2026-07-11 22:31
```

Artifacts are under:

```text
\\wsl.localhost\Ubuntu-24.04\home\shiro\zmk-workspace\firmware\zmk-config-roBa
```

### Experiment 3 Hardware Result

Result: scrolling works, but inertia is still absent.

This means the current chain can pass active scroll events even with
`scroll_inertia_v` at the end, so `scroll_inertia_v` is not simply blocking all
scroll events in this placement. The next likely issue is that this placement
does not give the inertia processor useful velocity information, or that its
own HID output is not visible/effective in this configuration.

### Next Test Direction

- Inspect whether `scroll_inertia_v` is intended to pass through already-scaled
  scroll events when placed after `zip_scroll_scaler`.
- Consider a temporary diagnostic build that makes inertia output unmistakable,
  for example stronger `scale`, lower `stop`, or logging if USB serial logs are
  practical.
- Consider a separate experimental layer that uses the article-style pre-scaler
  placement but increases active scroll scale enough to avoid the no-scroll
  result seen with `zip_scroll_scaler 4 675`.

### Experiment 4

Hypothesis: inertia may be getting cancelled by the scroll layer turning off.
`scroll_inertia_v.layer = <11>` resets inertia immediately when `SCROLL` layer
11 turns off. If the user flicks and then releases the scroll-layer hold key,
this can make inertia appear absent even if it armed correctly.

User clarification: the scroll layer is held while scrolling. Therefore this
hypothesis is lower priority than initially thought, but the build remains a
valid diagnostic for layer-off cleanup.

Only this setting was changed for the test:

```dts
layer = <(-1)>;
```

The rest of Experiment 3 stays the same:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>,
<&scroll_inertia_v>;
```

Expected result:

- If inertia appears after releasing the scroll key, the previous issue was
  layer-off cleanup.
- If inertia is still absent while active scrolling works, continue with output
  visibility or arming diagnostics.

If Experiment 4 is also "scroll OK, inertia absent", the next higher-value test
is to make inertia output unmistakable or instrument arming, because layer-off
cleanup is unlikely to be the main blocker.

Hardware result: no visible change. Scrolling still works, inertia is absent.

### Experiment 5

Purpose: make any inertia output unmistakable and make arming nearly trivial.
This tests whether the issue is simply weak/invisible output or thresholds that
are still too strict.

Changed `scroll_inertia_v` diagnostic values:

```dts
scale = <8000>;
scale-div = <1000>;
start = <1>;
move = <1>;
min-events = <1>;
decel-samples = <1>;
```

The active scroll chain is still the Experiment 3/4 post-scaler placement:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>,
<&scroll_inertia_v>;
```

Expected result:

- If inertia appears, previous settings were too weak or too hard to arm.
- If scrolling works but inertia is still absent, the processor is likely not
  entering COASTING in this post-scaler placement, or its direct HID output is
  not being sent/effective despite arming.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-11 22:49.
- `roBa_L-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-11 22:50.
- Output directory:
  `\\wsl.localhost\Ubuntu-24.04\home\shiro\zmk-workspace\firmware\zmk-config-roBa`

Hardware result: no visible inertia. This strongly suggests the issue is not
ordinary threshold/scale tuning.

### Experiment 6

Purpose: determine whether `scroll_inertia_v` ever reaches
`TRACKING -> COASTING` on roBa's current scroll event stream.

Temporary local-module patch in WSL workspace only:

- File:
  `~/zmk-workspace/modules/zmk-input-processor-scroll-inertia/src/input_processor_scroll_inertia.c`
- Function: `to_coasting`
- Behavior: immediately after `LOG_DBG("Inertia start...")`, emit one direct
  vertical HID scroll report of `+/-12` before scheduling the normal inertia
  tick.

Expected result:

- If a clear one-step scroll jump appears after a flick, `to_coasting()` is
  reached and the remaining problem is normal tick output/decay visibility.
- If no jump appears, `to_coasting()` is not reached; focus on input event
  values, event class, pre/post-scaler placement, and arming conditions.

Build result:

- `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-11 22:57.
  This is the important side for the diagnostic patch because the right-hand
  build includes the central-side trackball/inertia processor.
- `roBa_L-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-11 22:58.
  The left-hand build did not recompile `input_processor_scroll_inertia.c`,
  which is expected for the non-central side.

Follow-up comparison build:

- User could not clearly tell whether the Experiment 6 behavior was inertia or
  delayed diagnostic output.
- The temporary WSL module patch was removed with:
  `git -C ~/zmk-workspace/modules/zmk-input-processor-scroll-inertia checkout -- src/input_processor_scroll_inertia.c`.
- The roBa config remains on the Experiment 5 exaggerated inertia settings.
- Rebuilt both sides without the one-shot diagnostic output:
  - `roBa_R-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 10:27.
  - `roBa_L-seeeduino_xiao_ble.uf2`: built successfully at 2026-07-12 10:27.
- Use this build to compare against Experiment 6. If the delayed/jump behavior
  disappears, the visible motion was the diagnostic one-shot. If it remains,
  normal inertia output or another delayed scroll path is present.

### Related Docs

- `docs/SCROLL_INERTIA_RESEARCH.md`
- `docs/INPUT_PROCESSOR_EXPERIMENTS.md`
- `docs/ROBA_KEYMAP_MAP.md`
- `docs/ZMK_RESEARCH_NOTES.md`
