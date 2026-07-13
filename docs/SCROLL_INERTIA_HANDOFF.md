# Scroll Inertia Handoff

Updated: 2026-07-12

## 2026-07-13 Unified Module Status

Branch `codex/unified-scroll-inertia` proved the combined processor. Its chain
is `zip_xy_to_scroll_mapper -> roba_scroll`; the old
external snap/inertia modules and downstream scaler are absent. Initial
selected-axis input is retained, active/coast use one `4/60` scale, and layer or
endpoint changes reset all state. Host tests and both-half builds pass, and
right-hand feel was accepted on 2026-07-13. The reusable implementation is
published as `46slv/zmk-input-processor-roba-scroll` and pinned to `76addce`.
Lab 22 disables Lab 21's total-distance gain and uses distance-neutral eager
quantization (`threshold=20`, `eager=500`). Per-axis input direction now clears
stale fractional movement on reversal. Velocity tracking and coast are
unchanged. See
`docs/ROBA_SCROLL_MODULE.md` and Lab 19 in `docs/SCROLL_INERTIA_LAB.md`.

Lab 23 adds angular snap sectors without changing inertia: up/down receive 120
degrees each, left/right receive 60 degrees each, and the physical vertical
center rotates 30 degrees toward negative X (left). The offset is evaluated
before `invert-y`, so accepted user-facing directions stay unchanged. Hardware
confirmation of the physical offset sign remains pending. Host tests and clean
right/left WSL builds pass; the right-hand artifact is under
`~/zmk-workspace/firmware/zmk-config-roBa-angular-snap/`.

## Goal

Make roBa's layer 11 trackball scrolling smooth and continuously responsive,
then add trackpad-like inertia without reintroducing input gaps or coarse steps.

## Repository State

- Worktree: `C:\Users\shiro\Documents\GitHub\zmk-config-roBa-inertia-lab`
- Branch: `codex/scroll-inertia-lab`
- Remote: `origin/codex/scroll-inertia-lab`
- Detailed experiment log: `docs/SCROLL_INERTIA_LAB.md`
- Reproducible integration manual: `docs/SCROLL_INERTIA_INTEGRATION_GUIDE.md`
- Current processor map: `docs/ROBA_KEYMAP_MAP.md`
- WSL config: `~/zmk-workspace/config/zmk-config-roBa-inertia-lab`
- Firmware output: `~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab`

The production worktree at `C:\Users\shiro\Documents\GitHub\zmk-config-roBa`
must not be changed until the lab behavior is acceptable.

## Confirmed Results

- Lab 3, commit `86aefb5`: bypassing `scroll_inertia_v` restored scrolling.
- Lab 4, commit `a17b908`: `axis = <0>` restored scrolling with the inertia
  processor present. The earlier `axis = <1>` setting was suppressing roBa's
  effective wheel axis.
- Lab 5, commit `3592698`: placing inertia before `zip_scroll_scaler` preserved
  scrolling, but inertia was not perceptible.
- Lab 6, commit `93162a0`: disabling decay caused scrolling to continue in the
  release direction. This proves release detection, transition to coasting,
  scheduled inertia ticks, and direct HID scroll output all work on the
  driver's quantized `+/-1` wheel event stream. It does not prove correct
  velocity tracking from physical ball motion.
- Lab 7, commit `5fcae6d`: practical decay candidate was built but has not yet
  received a conclusive feel assessment.

## Current Problem

The reduced lab build is not yet a usable scrolling baseline:

- scrolling feels stepped or choppy;
- some trackball movement produces scroll and some does not;
- the trackpoint-style scroll operation has the same intermittent response;
- pointer movement also degraded after production input helpers were removed.

Do not interpret Lab 6's long same-direction scroll as a bug. It was an
intentional non-decaying diagnostic and successfully proved inertia operation.

## Root Integration Mismatch

The modified `kumamuk-git/zmk-pmw3610-driver` and the inertia module currently
both participate in scroll conversion:

1. `&trackball { scroll-layers = <11>; };` makes the PMW3610 driver enter its
   internal `SCROLL` mode.
2. The driver accumulates raw X/Y motion until
   `CONFIG_PMW3610_SCROLL_TICK` is exceeded.
3. It then emits only `INPUT_REL_WHEEL` or `INPUT_REL_HWHEEL` with value `+1`
   or `-1`, and clears both X and Y accumulators.
4. The listener chain receives this already quantized wheel stream, although
   the inertia module is designed to follow `zip_xy_to_scroll_mapper` and infer
   velocity from magnitude-preserving scroll deltas.

This is a data-contract mismatch, not evidence that pointer acceleration or
normal cursor behavior conflicts with inertia. Outside the driver's scroll
layer, cursor motion follows the separate `MOVE` path and can be diagnosed
independently.

Lab 6 could still coast for nearly six seconds because its diagnostic gates
accepted one `+/-1` event (`start=0`, `move=1`, `min-events=1`), its 24 ms stop
detector entered `COASTING`, and no decay or stop threshold could reduce the
stored velocity before `span=6000` expired.

## Lab 7 And Lab 8 Chain

Layer 11 uses:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 1>;
```

Lab 7 currently uses `axis = <0>`, scale `4/1`, tick `8`, start `1`, move `1`,
release `24`, decay `995`, friction `5`, stop `1`, and span `2000`.

Lab 9 kept the same sensor settings but temporarily removed
`&scroll_inertia_v` from the active chain, leaving:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&zip_scroll_scaler 4 1>;
```

Lab 10 is the current raw-input reference integration. It removes the PMW3610
driver's optional `scroll-layers` property and uses:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 675>;
```

Lab 10 uses `CPI=1000`, PMW3610 smart behavior enabled, `axis=1`, `layer=11`,
scale `4/675`, tick `8`, and the module's default arming/decay parameters.

Lab 12 is the current production-like integration candidate. It restores the
original cursor acceleration, AML, mouse gesture, and horizontal suppression
while preserving raw X/Y scroll ownership. It uses reversed vertical direction,
`CPI=400`, scale `4/75`, `start=16`, `move=32`, `friction=14`, and `stop=3`.

Lab 13 keeps that integration and lowers only `min-events` from the default 10
to 4. Its purpose is to let short fast flicks arm; EMA response and release
delay remain unchanged for a later isolated test if fast coasts still appear
capped near medium speed.

Lab 14 is the current candidate. It keeps Lab 13 and changes only the EMA pair
from `300/700` to `500/500` to reduce the very-fast active-to-coast speed drop.

Lab 15 is the current candidate. It keeps Lab 14 and lowers only the coupled
small-flick gates from `start=16 / move=32` to `start=12 / move=20`. Active
low-speed sensitivity remains at scale `4/75`; test `4/60` only after evaluating
small-flick arming separately.

## Next Work

Priority 1 is a smooth and consistently responsive active scroll path. Inertia
tuning is secondary until that baseline is restored.

Lab 8 hardware evidence narrows the issue: increasing PMW3610 CPI to `3200`
raised speed but smooth physical movement still produced complete pauses or
only tiny HID movement. The cause must be sought among the current lab changes
from the comfortable production configuration, including the active inertia
processor path; it should not be assumed to be a mechanical sensor fault.

1. Complete the Lab 9 bypass test as a baseline for the driver's current
   quantized scroll mode.
2. In the next isolated experiment, remove only
   `&trackball { scroll-layers = <11>; };`. The property is optional in this
   driver, so layer 11 should remain in the raw X/Y `MOVE` path while the ZMK
   input-listener's `scroller` child performs transform and mapping.
3. Verify the raw-listener baseline without inertia before adding the processor
   back after `zip_xy_to_scroll_mapper`.
4. For normal vertical scrolling, return to `axis=1`; reserve `axis=0` for the
   module's documented free-2D pan mode.
5. Only after active raw-input scrolling is continuous, tune inertia thresholds
   and decay. Keep pointer acceleration out of the scroll chain; cursor-chain
   behavior is a separate concern.

## Safety And Recovery

- All lab stages are committed and pushed, so they can be checked out by
  commit without rewriting history.
- Lab 6 proof state: `93162a0`.
- Current Lab 7 state: `5fcae6d` plus any later handoff-only commit.
- Do not use destructive reset commands in either worktree.
- WSL is currently initialized to the lab config. To restore production builds:

```sh
cd ~/zmk-workspace
nix develop -c just init config/zmk-config-roBa
```

## Build Commands

```sh
cd ~/zmk-workspace
nix develop -c just init config/zmk-config-roBa-inertia-lab
nix develop -c just build roBa_R
nix develop -c just build roBa_L
```

The user normally flashes the generated UF2 files manually, starting with the
right half.
