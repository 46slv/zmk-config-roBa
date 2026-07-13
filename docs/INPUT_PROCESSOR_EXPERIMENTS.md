# Input Processor Experiments

This file is a working memo for future Codex/AI sessions and for manual tuning.
Keep source-of-truth behavior in `config/roBa.keymap` and hardware/Kconfig
settings under `boards/shields/roBa/`.

## 2026-07-11: Trackball Scroll Responsiveness

### Current Source State

- Scroll layer is `SCROLL` / layer `11`.
- `&trackball` uses `scroll-layers = <11>;`.
- The scroll-layer listener override is `scroller` under `&trackball_listener`.
- Current `scroller` chain in `config/roBa.keymap`:

```dts
input-processors =
    <&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
    <&zip_xy_to_scroll_mapper>,
    <&zip_scroll_scaler 4 1>,
    <&scroll_inertia_v>;
```

- Right-hand shield config currently has:

```conf
CONFIG_ZMK_POINTING_SMOOTH_SCROLLING=y
CONFIG_PMW3610_SCROLL_TICK=4
```

- `&zip_scroll_snap` currently uses:

```dts
require-n-samples = <2>;
immediate-snap-threshold = <200>;
lock-duration-ms = <175>;
lock-for-next-n-events = <8>;
idle-reset-timeout-ms = <175>;
```

### User Goal

- Increase response when scrolling slowly with the trackball.
- Make scrolling feel as smooth as possible.
- Ideally, get trackpad-like inertia after a fast scroll.

### Practical Tuning Order

1. Lower `CONFIG_PMW3610_SCROLL_TICK`.
   - Applied: `8 -> 4`.
   - A later host-side stutter report was traced to using wired mode without updating the matching Bluetooth-side settings, not to this value.
   - If still too insensitive at low speed, test `3`.
   - This should make small/slow trackball movement produce scroll events sooner.

2. Test processor order in `scroller`.
   - Applied: map XY movement to wheel events before scaling scroll.
   - Current order:

```dts
input-processors =
    <&zip_y_scaler (-1) 1>,
    <&zip_xy_to_scroll_mapper>,
    <&zip_scroll_scaler 4 675>;
```

   - This may be more logically correct if `zip_scroll_scaler` only acts on
     wheel / horizontal-wheel relative events.

3. If still too slow overall, increase `zip_scroll_scaler`.
   - Candidate tests: `4 1 -> 5 1`, then `6 1`.
   - This changes the whole scroll speed, not just low-speed sensitivity.

4. If smoothness feels worse than expected, test weakening or temporarily
   removing `zip_scroll_snap`.
   - Snap can improve direction stability, but may reduce trackpad-like
     continuous scrolling feel.
   - Do not remove it permanently without testing diagonal movement and
    accidental horizontal scroll behavior.

### Applied Changes

- `CONFIG_PMW3610_SCROLL_TICK`: `8 -> 4`.
- `zip_scroll_snap.require-n-samples`: `3 -> 2`.
- `scroller` processor order now maps XY to wheel before applying
  scroll scaling.
- A third inertia A/B test is active. The article-style `4 675` chain still did
  not scroll on hardware, so this test keeps the known-working active scroll
  scale first and puts inertia at the end:
  `zip_xy_transform` -> `zip_xy_to_scroll_mapper` -> `zip_scroll_scaler 4 1` ->
  `scroll_inertia_v`.
- Hardware result: active scrolling works in this configuration, but inertia is
  still absent.
- Next test changes only `scroll_inertia_v.layer` from `<11>` to `<(-1)>` to
  check whether releasing the scroll-layer hold key cancels inertia before it
  becomes visible.
- `layer = <(-1)>` produced no visible change. The current diagnostic test
  exaggerates inertia with `scale = <8000>`, `start = <1>`, `move = <1>`, and
  `min-events = <1>`.

These are intentionally conservative tuning changes. The first-pass version with
`CONFIG_PMW3610_SCROLL_TICK=4` was reported to work after the matching BT/wired
configuration state was corrected. If low-speed scroll is still too delayed,
test `CONFIG_PMW3610_SCROLL_TICK=3`. If it becomes too noisy, compare `5` or
`6` against `4`.

### Inertia Assessment

Trackpad-like inertia is probably not achievable with only the current keymap
settings. It would require logic that continues emitting wheel events after
physical movement stops, with decay over time. That likely means a custom input
processor or driver/module change, not just `roBa.keymap` tuning.

Before attempting inertia, confirm whether any used external module has a
momentum/inertia setting. As of this memo, local greps for `inertia` and
`momentum` found no setting in this repo checkout.

See `docs/SCROLL_INERTIA_RESEARCH.md` for the 2026-07-11 research report.
`mjmjm0101/zmk-input-processor-scroll-inertia` has been added as the first
experiment candidate. The initial setup is vertical-only (`axis = <1>`) on
`SCROLL` layer `11`, with `scale = <4>`, `scale-div = <675>`, and `tick = <8>`
to match the downstream `zip_scroll_scaler 4 675` and PMW3610 125 Hz polling.
The module remains in `west.yml`, but the live `SCROLL` layer no longer routes
events through `&scroll_inertia_v`.
For the third hardware test this is temporarily false: `&scroll_inertia_v` is
back in the live chain after `zip_scroll_scaler 4 1`, with `start = <10>`,
`move = <20>`, `min-events = <3>`, `decel-samples = <1>`, and inertia output
scale reset to `1000 / 1000`.

### Verification Notes

- Build verification is not enough; this must be judged on real hardware.
- Test in both slow single-finger-style movement and fast flick-like movement.
- Check vertical scroll, horizontal scroll, diagonal movement, and layer exit.
- If `config/roBa.keymap` or `boards/shields/roBa/roBa_R.conf` changes, update:
  - `docs/ROBA_KEYMAP_MAP.md`
  - `docs/ZMK_RESEARCH_NOTES.md`
  - this file
