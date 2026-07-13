# roBa Keymap Map

Created: 2026-07-03

Source: `config/roBa.keymap`

This is a layer-by-layer working map for the current roBa ZMK configuration.
It keeps bindings close to the source order so future edits can be checked
against the real key positions.

## Reading Rules

- Layer numbers come from the `#define` block in `config/roBa.keymap`.
- `&trans` means transparent: fall through to the lower active layer.
- `&none` means intentionally unbound.
- Combo `key-positions` are physical positions, not letters. When the Onishi
  layout changes, combo positions must be checked separately.
- The source keymap rows are preserved as rows. Row widths differ because roBa
  has thumb keys and split halves.

## Layer Index

| No. | Name | Role |
|---:|---|---|
| 0 | `DEFAULT` | Main Onishi layout, AML-aware thumb keys, zoom, base encoder scroll |
| 1 | `LANG1_EAGER_TAP_DANCE` | Language-1 eager tap-dance helper |
| 2 | `NUMPAD_AND_ARROWS` | Numpad, arrows, copy/paste combos, window shortcuts |
| 3 | `FUNCTIONS_AND_SYMBOLS` | Function keys and symbols |
| 4 | `MISC` | Window/tab/app navigation, screenshots, media-ish commands |
| 5 | `ALT_TAB` | Alt-tab navigation layer |
| 6 | `CTRL_TAB` | Ctrl-tab navigation layer |
| 7 | `MOUSE` | Auto mouse layer and mouse buttons |
| 8 | `MEDIA` | Media controls |
| 9 | `EXTRA_FINCTIONS` | Extra F13-F24 function layer; name is kept as in source |
| 10 | `CONFIGURATION` | Bluetooth, output, reset, bootloader configuration |
| 11 | `SCROLL` | Scroll layer and arrow fallback |

## Layer 0: `DEFAULT`

Purpose: main Onishi layout. This is the most sensitive layer; letter changes
can affect combos and muscle memory.

| Row | Bindings |
|---:|---|
| 1 | `&kp Q` / `&kp L` / `&kp U` / `&mm_exclamation` / `&mm_question` / `&kp F` / `&kp W` / `&kp R` / `&kp Y` / `&kp P` |
| 2 | `&kp E` / `&kp I` / `&kp A` / `&kp O` / `&mm_minus_tilde` / `&kp LS(LG(S))` / `&zoom_in` / `&kp K` / `&kp T` / `&kp N` / `&lt 7 S` / `&kp H` |
| 3 | `&mt LCTRL Z` / `&kp X` / `&kp C` / `&kp V` / `&kp LS(LCTRL)` / `&kp LEFT_SHIFT` / `&zoom_out` / `&kp G` / `&kp D` / `&kp M` / `&lt 11 J` / `&kp B` |
| 4 | `&kp LEFT_GUI` / `&kp TAB` / `&kp LEFT_ALT` / `&tog 10` / `&kp LEFT_CONTROL` / `&kp LEFT_ALT` / `&lt_exit_AML_on_tap 3 INT_MUHENKAN` / `&lt_henkan_to_0 2 INT_HENKAN` / `&kp LEFT_SHIFT` |

Sensor binding:

- `&scroll_up_down` (`zmk,behavior-roba-encoder-scroll` on the encoder lab branch)

Encoder scroll behavior on `codex/encoder-scroll-accel-inertia-lab`:

- Sends finite one-shot wheel reports instead of queued `&msc` press/release taps.
- Uses bounded `1x / 2x / 4x / 6x` acceleration from inter-detent timing.
- Tune 2 uses `240/140/80 ms` boundaries and resets to `1x` after `320 ms`
  idle or any direction change.
- Tune 2 allowed `4x` or `6x` after one matching interval. Tune 4 keeps the
  timing thresholds but caps consecutive fast events at `2x`, then `4x`, then
  `6x`, so acceleration is visible as a ramp instead of a `1x -> 6x` jump.
- Tune 3 keeps the Tune 2 thresholds but evaluates acceleration once per
  accepted sensor event. If that event contains multiple encoder triggers, all
  emitted wheel reports use the same multiplier and acceleration / inertia
  streaks advance only once.
- Arms inertia only after two consecutive `6x` outputs.
- After an armed gesture stops for `70 ms`, emits at most six coast reports:
  `6x -> 5x -> 4x -> 3x -> 2x -> 1x`, spaced by `28 ms`.
- New encoder input, layer changes, and endpoint changes cancel pending coast.
- `steps = 12` and `triggers-per-rotation = 10` remain unchanged in this lab so
  behavior can be evaluated separately from encoder signal calibration.
- Windows output monitoring is documented in
  `docs/ENCODER_SCROLL_MONITORING.md`; it does not change the firmware protocol.

Special behavior notes:

- `&lt 7 S`: hold enters `MOUSE`, tap sends `S`.
- `&lt 11 J`: hold enters `SCROLL`, tap sends `J`.
- `&lt_exit_AML_on_tap 3 INT_MUHENKAN`: hold layer 3, tap `INT_MUHENKAN` and exits AML.
- `&lt_henkan_to_0 2 INT_HENKAN`: hold uses `mo2_with_forced_exit`, tap `INT_HENKAN` and exits AML.
- `&mm_exclamation`, `&mm_question`, `&mm_minus_tilde` are custom mod-morph behaviors.

## Layer 1: `LANG1_EAGER_TAP_DANCE`

Purpose: mostly transparent helper layer. It adds one eager language tap-dance
binding on row 4.

| Row | Bindings |
|---:|---|
| 1 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 2 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 3 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 4 | `&trans` / `&trans` / `&trans` / `&eager_tap_dance LANG1 1` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |

## Layer 2: `NUMPAD_AND_ARROWS`

Purpose: numpad, arrows, clipboard combos, and navigation. Entry/release is
important because `mo2_with_forced_exit` forces return to layer 0.

| Row | Bindings |
|---:|---|
| 1 | `&kp JP_ASTERISK` / `&kp KP_NUMBER_7` / `&kp KP_NUMBER_8` / `&kp KP_NUMBER_9` / `&kp KP_MINUS` / `&td_alt_f4` / `&none` / `&kp UP_ARROW` / `&none` / `&none` |
| 2 | `&kp KP_SLASH` / `&kp KP_NUMBER_4` / `&kp KP_NUMBER_5` / `&kp KP_NUMBER_6` / `&kp JP_PLUS` / `&none` / `&none` / `&kp LG(LS(S))` / `&kp LEFT_ARROW` / `&kp DOWN_ARROW` / `&kp RIGHT_ARROW` / `&kp LEFT_WIN` |
| 3 | `&mt LEFT_SHIFT KP_NUMBER_0` / `&kp KP_NUMBER_1` / `&kp KP_NUMBER_2` / `&kp KP_NUMBER_3` / `&kp PERCENT` / `&trans` / `&none` / `&none` / `&kp J` / `&kp K` / `&kp L` / `&to_nl 3` |
| 4 | `&trans` / `&kp INT_YEN` / `&kp KP_EQUAL` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&to_nl 4` |

Sensor binding:

- `&inc_dec_kp C_VOLUME_UP C_VOLUME_DOWN`

Layer-specific combos:

- `l2dm_copy`: positions `30 31`, sends `LC(C)`.
- `l2mj_paste`: positions `32 31`, sends `LC(V)`.

## Layer 3: `FUNCTIONS_AND_SYMBOLS`

Purpose: function keys and symbol entry.

| Row | Bindings |
|---:|---|
| 1 | `&trans` / `&kp F1` / `&kp F2` / `&kp F3` / `&td_alt_f4` / `&kp LBKT` / `&kp RBKT` / `&kp BSLH` / `&kp JP_COLON` / `&kp SEMICOLON` |
| 2 | `&trans` / `&kp F6` / `&kp F7` / `&kp F8` / `&kp F5` / `&trans` / `&kp LS(N4)` / `&kp LS(N3)` / `&kp LS(N8)` / `&kp LS(N9)` / `&kp LESS_THAN` / `&kp GREATER_THAN` |
| 3 | `&trans` / `&kp F9` / `&kp F10` / `&kp F11` / `&kp F12` / `&trans` / `&kp JP_UNDERSCORE` / `&kp LS(N6)` / `&kp LS(RBKT)` / `&kp LS(BSLH)` / `&kp JP_DQUOTE` / `&kp JP_QUOTE` |
| 4 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp LS(N4)` |

## Layer 4: `MISC`

Purpose: window/tab navigation, app switching, screenshots, volume, mute, and
misc commands.

| Row | Bindings |
|---:|---|
| 1 | `&to_kp 6 TAB` / `&kp LC(LG(LEFT))` / `&kp LG(TAB)` / `&kp LG(LC(RIGHT))` / `&td_alt_f4` / `&td_alt_f4` / `&kp LC(W)` / `&kp LS(LC(T))` / `&kp LC(T)` / `&to_kp 6 TAB` |
| 2 | `&to_kp 7 TAB` / `&kp LG(LS(LEFT_ARROW))` / `&kp LG(UP_ARROW)` / `&kp LG(LS(RIGHT_ARROW))` / `&kp A` / `&trans` / `&kp C_VOLUME_UP` / `&kp F5` / `&kp LC(PAGE_UP)` / `&kp A` / `&kp LC(PAGE_DOWN)` / `&to_kp 7 TAB` |
| 3 | `&kp LA(LC(LG(DOWN)))` / `&kp LG(LEFT_ARROW)` / `&kp LG(DOWN)` / `&kp LG(RIGHT_ARROW)` / `&screenshot_copy` / `&screensot_save` / `&kp C_VOLUME_DOWN` / `&kp K_MUTE` / `&kp LA(LEFT)` / `&kp DOWN` / `&kp LA(RIGHT)` / `&trans` |
| 4 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp DELETE` / `&trans` / `&trans` |

Sensor binding:

- `&inc_dec_kp LC(PAGE_UP) LC(PAGE_DOWN)`

## Layer 5: `ALT_TAB`

Purpose: navigation while Alt-tab mode is active.

| Row | Bindings |
|---:|---|
| 1 | `&kp TAB` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp TAB` |
| 2 | `&kp LS(TAB)` / `&trans` / `&kp UP` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp UP` / `&trans` / `&kp LS(TAB)` |
| 3 | `&trans` / `&kp LEFT` / `&kp DOWN` / `&kp RIGHT` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp LEFT` / `&kp DOWN` / `&kp RIGHT` / `&trans` |
| 4 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |

## Layer 6: `CTRL_TAB`

Purpose: navigation while Ctrl-tab mode is active.

| Row | Bindings |
|---:|---|
| 1 | `&kp LS(TAB)` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp LS(TAB)` |
| 2 | `&kp TAB` / `&trans` / `&kp UP` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp UP` / `&trans` / `&kp TAB` |
| 3 | `&kp LS(TAB)` / `&kp LEFT` / `&kp DOWN` / `&kp RIGHT` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp LEFT` / `&kp DOWN` / `&kp RIGHT` / `&kp LS(TAB)` |
| 4 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |

## Layer 7: `MOUSE`

Purpose: auto mouse layer. Mouse buttons are placed on the right home area.

| Row | Bindings |
|---:|---|
| 1 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 2 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&mkp MB1` / `&mkp MB2` / `&none` / `&trans` |
| 3 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 4 | `&trans` / `&trans` / `&trans` / `&tog_off 7` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |

Layer-specific combos:

- `ai_space`: positions `11 12`, sends `SPACE`.
- `ns_enter`: positions `19 20`, sends `ENTER`.
- `sh_bs`: positions `20 21`, sends `BACKSPACE`.
- `jb_unsc`: positions `32 33`, sends `JP_UNDERSCORE`.
- `middle_click`: positions `18 19`, sends `MCLK`.
- `ql_escape`: positions `0 1`, sends `ESCAPE`.

## Layer 8: `MEDIA`

Purpose: media playback and volume controls.

| Row | Bindings |
|---:|---|
| 1 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp C_PREV` / `&kp C_PLAY_PAUSE` / `&kp C_NEXT` / `&trans` / `&trans` |
| 2 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp C_VOLUME_DOWN` / `&kp C_MUTE` / `&kp C_VOLUME_UP` / `&trans` / `&trans` |
| 3 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp LS(LA(LC(M)))` / `&trans` / `&trans` / `&trans` |
| 4 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |

## Layer 9: `EXTRA_FINCTIONS`

Purpose: extra function key layer. The source name appears to contain a typo,
but it is used consistently and should not be renamed casually.

| Row | Bindings |
|---:|---|
| 1 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp F13` / `&kp F14` / `&kp F15` / `&kp F16` / `&trans` |
| 2 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp F17` / `&kp F18` / `&kp F19` / `&kp F20` / `&trans` |
| 3 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&kp F21` / `&kp F22` / `&kp F23` / `&kp F24` / `&trans` |
| 4 | `&trans` / `&trans` / `&trans` / `&eager_tap_dance LANGUAGE_1 9` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |

Input processor interaction:

- `disable-scroll-x` applies on this layer and scales `INPUT_REL_HWHEEL` by `0/1`.

## Layer 10: `CONFIGURATION`

Purpose: Bluetooth, output selection, reset, bootloader, and safe-ish
configuration actions. This layer contains destructive or disruptive actions.

| Row | Bindings |
|---:|---|
| 1 | `&to 0` / `&none` / `&none` / `&none` / `&none` / `&bt BT_SEL 1` / `&bt BT_SEL 0` / `&none` / `&out OUT_TOG` / `&to 0` |
| 2 | `&none` / `&none` / `&none` / `&none` / `&tog_on 0` / `&trans` / `&td_bt_clear` / `&bt BT_SEL 0` / `&to 0` / `&bt BT_SEL 2` / `&bt BT_SEL 3` / `&bt BT_SEL 4` |
| 3 | `&none` / `&none` / `&none` / `&none` / `&tog_off 0` / `&reset_bootloader 0 0` / `&reset_bootloader 0 0` / `&bt BT_SEL 0` / `&none` / `&none` / `&none` / `&none` |
| 4 | `&none` / `&none` / `&none` / `&none` / `&none` / `&none` / `&none` / `&none` / `&td_bt_clear_all` |

Risk notes:

- `&td_bt_clear_all` clears all Bluetooth pairings after tap-dance activation.
- `&reset_bootloader 0 0` can reset or enter bootloader depending on hold/tap.
- This layer should not be made easier to enter without an explicit decision.

## Layer 11: `SCROLL`

Purpose: scroll-mode trackball layer with arrow fallback keys.

| Row | Bindings |
|---:|---|
| 1 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 2 | `&trans` / `&trans` / `&kp UP_ARROW` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 3 | `&kp LEFT_GUI` / `&kp LEFT_ARROW` / `&kp DOWN_ARROW` / `&kp RIGHT_ARROW` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |
| 4 | `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` / `&trans` |

Sensor binding:

- `&inc_dec_kp LC(C_AC_ZOOM_IN) LC(C_AC_ZOOM_OUT)`

Input processor interaction:

- The PMW3610 `scroll-layers` property is intentionally omitted so the driver
  emits raw X/Y on layer `11`; the input-listener owns scroll conversion.
- `scroller` input processor override applies on layer `11`.
- The complete accepted scroller chain is:
  - `&zip_xy_to_scroll_mapper`
  - `&roba_scroll`
- `roba_scroll` owns accepted `Y_INVERT`, snap `2/200/175/8/175`, active and
  coast scale `4/60`, inertia `axis=0`, layer reset `11`, EMA `500/500`, arming
  `start=12 / move=20 / min-events=4`, friction `14`, and stop `3`. Lab 22 uses
  distance-neutral low-speed eager quantization `threshold=20 / boost=0 /
  eager=500` and clears same-axis fractional state on physical reversal;
  medium/high input, velocity tracking, and coast are unchanged.
- `zmk-input-processor-roba-scroll` is an external module pinned to `0c7a8fe` in
  `config/west.yml`. The old scroll-snap/inertia projects and downstream scaler
  are absent; one module setting scales both active and coast paths.
- Historically, the Lab 12-18 scroller chain followed the inertia
  module's documented placement and reverses vertical direction:
  - `&zip_xy_transform INPUT_TRANSFORM_Y_INVERT`
  - `&zip_xy_to_scroll_mapper`
  - `&zip_scroll_snap`
  - `&scroll_inertia_v`
  - `&zip_scroll_scaler 4 60`
- Lab 16 restores scroll snap before inertia and changes inertia to `axis=0`.
  Snap uses the earlier low-latency settings (`require-n-samples=2`, immediate
  threshold `200`, lock `175 ms` / `8` events, idle reset `175 ms`) so vertical
  or horizontal intent is selected before inertia measures the gesture.
- Lab 16a removes only `INPUT_TRANSFORM_XY_SWAP` after hardware testing showed
  right mapped to up, up to right, left to down, and down to left. Keeping
  `X_INVERT` preserves the established direction convention while routing
  physical horizontal movement to HWHEEL and vertical movement to WHEEL.
- Lab 16b reverses both user-facing directions after Lab 16a hardware testing.
  Reversing both relative to the `X_INVERT` state requires replacing it with
  `Y_INVERT`: horizontal loses its inversion and vertical gains one.
  The user accepted this direction mapping and the combined snap/inertia
  behavior on right-hand hardware on 2026-07-12.
- Lab 17 changes the matched active/coast scale from `4/75` to `4/60`, making
  both outputs 25 percent faster while preserving their handoff ratio. Snap,
  directions, arming thresholds, EMA, and decay are unchanged.
  The user accepted this scale on right-hand hardware on 2026-07-13.
- In the historical chain, `scroll_inertia_v` bound cleanup to layer `11` and
  mirrors the downstream `4/60` scale. At restored `CPI=400`, `start=16`,
  `move=32`, `friction=14`, and `stop=3` preserve the approximate physical
  thresholds of the 1000 CPI defaults. Lab 13 lowers `min-events` from the
  default 10 to 4 so short fast flicks can arm within about 32 ms. Lab 14 uses
  `gain=500` and `blend=500` so short high-speed input affects the coast seed
  more quickly. Lab 15 lowers the small-flick arming pair to `start=12` and
  `move=20` without changing active scroll scale.
- Cursor pointer acceleration, AML, mouse gesture, and horizontal-wheel
  suppression are restored. Pointer acceleration is limited to the
  `DEFAULT/MOUSE` conditional path and does not precede layer 11 scrolling.
  The unified processor also remains confined to layer 11. Lab 19 was accepted
  on right-hand hardware on 2026-07-13.
- Lab 18 approximates the earlier effective two-stage pointer acceleration with
  one pointer-only curve: factor `1.0..7.0`, threshold `300`, max speed `3500`,
  exponent `1`. X/Y base scalers remain `70/100` and `80/100`. This does not
  affect layer-11 snap, scroll scale, or inertia.
- Scroll tuning notes for future Codex/AI sessions are in
  `docs/INPUT_PROCESSOR_EXPERIMENTS.md`.

## Global Combos

Combos with explicit layers:

| Combo | Positions | Layers | Binding | Notes |
|---|---|---|---|---|
| `ai_space` | `11 12` | `0 7` | `&kp SPACE` | Prior idle 80ms, timeout 80ms |
| `ns_enter` | `19 20` | `0 7` | `&kp ENTER` | Main / mouse layer |
| `sh_bs` | `20 21` | `0 7` | `&kp BACKSPACE` | Timeout 80ms |
| `jb_unsc` | `32 33` | `0 7` | `&kp JP_UNDERSCORE` | Timeout 80ms |
| `middle_click` | `18 19` | `7 11` | `&mkp MCLK` | Mouse / scroll layers |
| `ql_escape` | `0 1` | `0 7` | `&kp ESCAPE` | Prior idle 300ms |
| `caps_word` | `22 33` | `0` | `&caps_word` | Prior idle 300ms |
| `wr_sq_brkt` | `6 7` | `0` | `&mc_sq_brkt` | Types `[]` then left |
| `tn_par_brkt` | `18 19` | `0` | `&mc_par_brkt` | Types `()` then left |
| `dm_cur_brkt` | `30 31` | `0` | `&mc_cur_brkt` | Types `{}` then left |
| `l2dm_copy` | `30 31` | `2` | `&kp LC(C)` | Layer 2 only |
| `l2mj_paste` | `32 31` | `2` | `&kp LC(V)` | Layer 2 only |
| `zx_ctrl_z` | `22 23` | `0` | `&kp LC(Z)` | Undo |

Combos without explicit `layers` property:

| Combo | Positions | Binding | Notes |
|---|---|---|---|
| `yp_del` | `8 9` | `&kp DEL` | Active wherever combo defaults apply |
| `ei_tab` | `11 10` | `&kp_exit_AML TAB` | Sends Tab and exits AML |
| `mj_slash` | `31 32` | `&kp SLASH` | Active wherever combo defaults apply |
| `layer10` | `0 9` | `&tog 10` | Toggles configuration layer |

## Custom Behaviors And Macros Referenced By Layers

| Name | Type | Used by | Meaning |
|---|---|---|---|
| `exit_AML` | macro | many AML-aware macros | Turns off `MOUSE` layer via `tog_off MOUSE` |
| `kp_exit_AML` | macro one-param | combos, hold-taps | Sends a key, then exits AML |
| `mo2_with_forced_exit` | macro | `lt_henkan_to_0` | Enters layer 2 and forces return to layer 0 on release |
| `to_nl` | macro one-param | layer 2 | Exits AML, then switches to a target layer |
| `mc_sq_brkt` | macro | combo | Types `[]`, moves cursor left |
| `mc_par_brkt` | macro | combo | Types `()`, moves cursor left |
| `mc_cur_brkt` | macro | combo | Types `{}`, moves cursor left |
| `zoom_in` | macro | layer 0 | Holds Ctrl, scrolls up, releases Ctrl |
| `zoom_out` | macro | layer 0 | Holds Ctrl, scrolls down, releases Ctrl |
| `eager_tap_dance` | macro two-param | layers 1 and 9 | Holds first key until release, then sticky-layer/tap helper |
| `lt_exit_AML_on_tap` | hold-tap | layer 0, `td_muhenkan_henkan` | Hold layer, tap key and exit AML |
| `lt_henkan_to_0` | hold-tap | layer 0 | Hold layer 2 with forced reset, tap `INT_HENKAN` |
| `mm_exclamation` | mod-morph | layer 0 | Comma normally, exclamation with Shift |
| `mm_question` | mod-morph | layer 0 | Period normally, question with Shift |
| `mm_minus_tilde` | mod-morph | layer 0 | Minus normally, JP tilde with Shift |
| `screenshot_copy` | mod-morph | layer 4 | Screenshot shortcut behavior |
| `td_alt_f4` | tap-dance | layers 2, 3, 4 | F4 / Alt+F4 |
| `td_bt_clear` | tap-dance | layer 10 | Bluetooth clear after tap count |
| `td_bt_clear_all` | tap-dance | layer 10 | Bluetooth clear all after tap count |
| `reset_bootloader` | hold-tap | layer 10 | Reset tap-dance or bootloader |

## Windows Status Companion

The optional `CONFIG_ROBA_STATUS` firmware service observes the existing layer
state without changing bindings, layer numbers, combo behavior, AML, SCROLL, or
trackball processing.

- The service reports the full active-layer bitmask.
- `zmk_keymap_highest_layer_active()` supplies the prominent Windows layer.
- Layer 7 remains `MOUSE`; layer 11 remains `SCROLL`.
- No keycodes, positions, or typed content are transmitted.
- Disabling `CONFIG_ROBA_STATUS` restores the previous firmware composition.

The v1.1 wired extension adds a second read-only USB HID interface on the right
half. It sends the same state snapshot and does not change any layer definition,
key position, combo, AML transition, scroll processor, or trackball path. ZMK
Studio remains on its existing USB CDC/BLE transports.

## Follow-Up Checks

- Confirm whether the source row lengths match the intended physical key count.
- Confirm actual devicetree behavior for duplicate `input-processors` in `&trackball_listener`.
- Decide whether `keymap-drawer/roBa.yaml` should be treated as generated output or maintained source.
- If this table is used for Notion sync, use this file as the Git-side source of truth and keep Notion layer sections in the same order.
