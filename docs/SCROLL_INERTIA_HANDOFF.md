# Scroll Inertia Handoff

Updated: 2026-07-12

## Goal

Make roBa's layer 11 trackball scrolling smooth and continuously responsive,
then add trackpad-like inertia without reintroducing input gaps or coarse steps.

## Repository State

- Worktree: `C:\Users\shiro\Documents\GitHub\zmk-config-roBa-inertia-lab`
- Branch: `codex/scroll-inertia-lab`
- Remote: `origin/codex/scroll-inertia-lab`
- Detailed experiment log: `docs/SCROLL_INERTIA_LAB.md`
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
  scheduled inertia ticks, and direct HID scroll output all work.
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

## Current Chain

Layer 11 uses:

```dts
<&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT | INPUT_TRANSFORM_Y_INVERT)>,
<&zip_xy_to_scroll_mapper>,
<&scroll_inertia_v>,
<&zip_scroll_scaler 4 1>;
```

Lab 7 currently uses `axis = <0>`, scale `4/1`, tick `8`, start `1`, move `1`,
release `24`, decay `995`, friction `5`, stop `1`, and span `2000`.

## Next Work

Priority 1 is a smooth and consistently responsive active scroll path. Inertia
tuning is secondary until that baseline is restored.

Lab 8 hardware evidence narrows the issue: increasing PMW3610 CPI to `3200`
raised speed but smooth physical movement still produced complete pauses or
only tiny HID movement. The cause must be sought among the current lab changes
from the comfortable production configuration, including the active inertia
processor path; it should not be assumed to be a mechanical sensor fault.

1. Compare the current lab input path with the first known comfortable
   low-speed production version and identify which removed processor or PMW3610
   setting supplied continuous fine-grained events.
2. Restore one input-path component at a time while keeping the proven Lab 6
   inertia placement and `axis = <0>`.
3. Test both layer-11 trackball scrolling and trackpoint-style scrolling after
   each build. Record active-scroll continuity separately from coast behavior.
4. Once active scrolling is stable, tune decay from the Lab 6 proof toward a
   short natural tail. Do not change axis, placement, scale, and decay together.

Likely comparison points include PMW3610 scroll tick, the original scaler
ratio/remainder behavior, low-speed scaling, and scroll snap. Pointer
acceleration and AML should only be restored when evaluating pointer quality;
they must not obscure the active-scroll test.

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
