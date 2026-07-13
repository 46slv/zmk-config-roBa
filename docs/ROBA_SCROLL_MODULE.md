# roBa Unified Scroll Module

## Purpose

`zmk,input-processor-roba-scroll` combines automatic X/Y selection, retained
initial movement, active output scaling, velocity tracking, and inertial coast
in one input processor. It replaces the previous serial combination of
`zmk-scroll-snap`, `zmk-input-processor-scroll-inertia`, and
`zip_scroll_scaler` on layer 11.

The reusable implementation is maintained at
<https://github.com/46slv/zmk-input-processor-roba-scroll>. This config pins
commit `b0c28842c5469972bbe797065ace0e5d9104592b` in `config/west.yml`.

## Processing Contract

```text
PMW3610 raw X/Y
  -> zip_xy_to_scroll_mapper
  -> roba_scroll
       direction inversion
       retained axis selection
       active scaling with per-axis remainder
       active-only low-speed boost with linear taper
       velocity EMA and flick arming
       direct-HID decaying coast
```

The PMW3610 `scroll-layers` property must remain absent. The listener must
receive magnitude-preserving raw motion.

## Attribution

The inertia state machine and fixed-point math were derived from the MIT
licensed `mjmjm0101/zmk-input-processor-scroll-inertia` module, pinned at
commit `f7dadef` during the roBa experiments. Axis-selection behavior was
informed by the MIT licensed `kot149/zmk-scroll-snap` module at `v1`
(`dd21a0f`). The integrated retention and single-scale behavior are local roBa
changes. Source files retain the MIT SPDX identifier.

## Accepted Compatibility Preset

```dts
roba_scroll: roba_scroll {
    compatible = "zmk,input-processor-roba-scroll";
    #input-processor-cells = <0>;

    axis-mode = <0>;
    snap-x-ratio = <5 8>;
    snap-y-ratio = <1 1>;
    snap-events = <2>;
    snap-immediate = <200>;
    snap-lock-ms = <175>;
    snap-lock-events = <8>;
    snap-idle-ms = <175>;
    snap-switch = <1>;
    invert-y;

    axis = <0>;
    layer = <11>;
    scale = <4>;
    scale-div = <60>;
    active-low-speed-threshold = <20>;
    active-low-speed-boost = <250>;
    tick = <8>;
    gain = <500>;
    blend = <500>;
    start = <12>;
    move = <20>;
    min-events = <4>;
    friction = <14>;
    stop = <3>;
};
```

`scale` and `scale-div` control both active and coast output. Do not add a
downstream scroll scaler.

`active-low-speed-threshold` and `active-low-speed-boost` affect only active
events below raw magnitude `20`. The roBa value `250` adds at most 25 percent
at the smallest nonzero delta and tapers to zero at the threshold. Velocity
tracking, flick arming, medium/high active input, and coast remain unchanged.

## Parameter Reference

| Group | Properties | Units / practical range |
| --- | --- | --- |
| Axis choice | `axis-mode` | `0` snap, `1` explicit free 2D |
| Direction ratio | `snap-x-ratio`, `snap-y-ratio` | Positive numerator/denominator pairs |
| Initial decision | `snap-events`, `snap-immediate` | Events and raw movement; roBa `2`, `200` |
| Lock | `snap-lock-ms`, `snap-lock-events`, `snap-idle-ms`, `snap-switch` | Time/events/movement; roBa `175`, `8`, `175`, `1` |
| Direction | `invert-x`, `invert-y` | Boolean, before selection and velocity tracking |
| Velocity EMA | `gain`, `blend` | Permille; sum should be `1000` |
| Flick arm | `start`, `move`, `min-events` | Raw velocity/movement/events; roBa `12`, `20`, `4` |
| Release | `release`, `gesture-timeout`, `handoff-ms` | Milliseconds; defaults `24`, `100`, `100` |
| Deceleration | `decel-samples`, `decel-ratio`, `peak-decay` | Events/permille; defaults `3`, `850`, `990` |
| Coast curve | `fast`, `slow`, `decay-fast/slow/tail` | Velocity boundaries and permille/tick |
| Coast cutoff | `friction`, `stop`, `limit`, `span`, `tick` | Loss, velocity, ms; roBa `14`, `3`, `600`, `6000`, `8` |
| Shared output | `scale`, `scale-div` | One active/coast ratio; accepted `4/60` |
| Low-speed active | `active-low-speed-threshold`, `active-low-speed-boost` | Raw delta and permille; trial `20`, `250` |
| Safety | `layer`, `suppress-limit` | Layer index and absorbed-event cap |
| Optional | `swap-mod`, `unlock-mod`, `exact-magnitude` | Modifier masks and exact-math boolean |

Axis selection thresholds and inertia arming thresholds are separate concepts.
Changing snap sensitivity must not silently change flick arming.

## State Model

Axis selection has `NONE`, `X`, and `Y` states. In snap mode, the processor
retains signed X/Y deltas until it can choose an axis, then flushes all retained
movement on the chosen HID wheel code. Cross-axis movement is retained while
the time/event lock is active and is flushed if a continuous axis switch is
accepted. Idle timeout starts a clean gesture.

Inertia uses `IDLE`, `TRACKING`, and `COASTING`. A selected-axis change cancels
old coast and velocity before the retained new-axis delta is tracked. Layer-off
and endpoint-change events reset axis lock, active remainders, timers, velocity,
and coast together.

Set `axis-mode = <1>` for explicit free two-axis input. It bypasses snap while
keeping active scaling and two-axis inertia.

## Host Tests

From a checkout of the standalone module:

```sh
make -C tests clean test
make -C tests clean
```

The suite covers the inherited fixed-point inertia math and the new retained
axis-selection rules. The verified result is `142/142` inertia/scale checks
plus all axis-lock checks passing.

## Hardware Test Matrix

| Case | Expected result |
| --- | --- |
| Very slow short motion | Initial chosen-axis distance appears; it is delayed, not lost |
| Small flick | Active output and a short same-axis coast |
| Medium/fast flick | No axis jump or large release speed step |
| Horizontal/vertical | Correct axis and accepted Lab 16b direction |
| Diagonal | Stable dominant-axis choice |
| Continuous axis change | Old coast cancels; retained new-axis input appears |
| Reverse direction | User regains control without stale coast |
| Layer 11 release | All state and pending output reset immediately |
| USB/BLE endpoint change | No old pending movement or coast on the new endpoint |

## Rollback

Set both low-speed properties to `0`, or pin the module back to `c06c453`, to
restore the accepted Lab 19/20 output behavior without changing other scroll
parameters.
