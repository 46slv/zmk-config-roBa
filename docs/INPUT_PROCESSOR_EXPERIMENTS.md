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
    <&zip_scroll_snap>;
```

- Right-hand shield config currently has:

```conf
CONFIG_ZMK_POINTING_SMOOTH_SCROLLING=y
CONFIG_PMW3610_SCROLL_TICK=6
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
   - First test `8 -> 4` improved responsiveness but may have increased host-side stutter.
   - Current compromise test: `6`.
   - If still too insensitive at low speed, test `3`.
   - This should make small/slow trackball movement produce scroll events sooner.

2. Test processor order in `scroller`.
   - Applied: map XY movement to wheel events before scaling scroll.
   - Current order:

```dts
input-processors =
    <&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
    <&zip_xy_to_scroll_mapper>,
    <&zip_scroll_scaler 4 1>,
    <&zip_scroll_snap>;
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

- `CONFIG_PMW3610_SCROLL_TICK`: `8 -> 4`, then backed off to `6` after host-side stutter was reported while the keyboard was connected.
- `zip_scroll_snap.require-n-samples`: `3 -> 2`.
- `scroller` processor order now maps XY to wheel before applying
  `zip_scroll_scaler 4 1`.

These are intentionally conservative tuning changes. If the real keyboard still
causes host-side stutter at `6`, restore `CONFIG_PMW3610_SCROLL_TICK=8` before
changing other parameters. If stutter is gone but low-speed scroll is still too
delayed, compare `5` against `6` rather than jumping back to `4`.

### Inertia Assessment

Trackpad-like inertia is probably not achievable with only the current keymap
settings. It would require logic that continues emitting wheel events after
physical movement stops, with decay over time. That likely means a custom input
processor or driver/module change, not just `roBa.keymap` tuning.

Before attempting inertia, confirm whether any used external module has a
momentum/inertia setting. As of this memo, local greps for `inertia` and
`momentum` found no setting in this repo checkout.

### Verification Notes

- Build verification is not enough; this must be judged on real hardware.
- Test in both slow single-finger-style movement and fast flick-like movement.
- Check vertical scroll, horizontal scroll, diagonal movement, and layer exit.
- If `config/roBa.keymap` or `boards/shields/roBa/roBa_R.conf` changes, update:
  - `docs/ROBA_KEYMAP_MAP.md`
  - `docs/ZMK_RESEARCH_NOTES.md`
  - this file
