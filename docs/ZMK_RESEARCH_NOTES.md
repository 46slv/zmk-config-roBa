# ZMK / roBa Research Notes for Codex

作成日: 2026-07-03

この資料は、`46slv/zmk-config-roBa` を今後編集するときに Codex が最初に読むための作業用ノートです。特に ZMK の input processors と behaviors を重点対象にしています。

## このリポジトリの前提

- 作業リポジトリ: `C:\Users\shiro\Documents\GitHub\zmk-config-roBa`
- 現在のブランチ: `main`
- origin: `https://github.com/46slv/zmk-config-roBa.git`
- upstream: `https://github.com/kumamuk-git/zmk-config-roBa.git`
- 主な設定ファイル:
  - `config/roBa.keymap`: レイヤー、コンボ、behavior、macro、input processor 設定の中心。
  - `config/west.yml`: ZMK 本体と外部モジュールの依存関係。
  - `boards/shields/roBa/roBa.dtsi`: 物理レイアウト、matrix transform、trackball listener、encoder 定義。
  - `build.yaml`: GitHub Actions のビルド対象。
- 既存 README は文字化けしている箇所が多い。編集時は元のエンコーディング由来の破損を不用意に広げないこと。

## roBa のハードウェア文脈

roBa は keyball に影響を受けたワイヤレスキーボードで、ZMK firmware による BLE、分割カラムスタッガード、42キー、トラックボール、水平ロータリーエンコーダを特徴としている。

参照:

- roBa 本体: https://github.com/kumamuk-git/roBa
- roBa README: ZMK firmware / BLE、42キー、トラックボール、ロータリーエンコーダの記述あり。
- roBa デフォルト firmware 情報では、オートマウスレイヤー、スクロールレイヤー、CPI などが明記されている。

このリポジトリでは、roBa の標準 firmware から派生し、さらに `kot149/zmk-config-roBa` の内容を多く取り込んでいる。最終的な公開先は `46slv/zmk-config-roBa`。

## 大西配列との関係

`config/roBa.keymap` の `DEFAULT` レイヤーは、大西配列ベースの文字配置として扱う。今後の変更では以下を守る。

- `DEFAULT` の文字キーは、単なる QWERTY ではなく大西配列の意味を持つ。
- 文字キーの入れ替えは、コンボ、tap-dance、mod-morph、auto mouse layer の発火位置に影響する。
- combo の `key-positions` は物理位置指定なので、配列変更時も「文字」ではなく「位置」で影響を確認する。

## ZMK keymap の基本

ZMK は `<keyboard>.keymap` に devicetree 構文で keymap、behavior、feature configuration を宣言する。キー位置、センサー、combo などに binding を割り当てる。

公式参照:

- Keymaps & Behaviors: https://zmk.dev/docs/keymaps
- Behaviors Overview: https://zmk.dev/docs/keymaps/behaviors
- Keycodes: https://zmk.dev/docs/keymaps/list-of-keycodes

このリポジトリでは、`config/roBa.keymap` に以下が混在している。

- ZMK 標準 include: `behaviors.dtsi`, `dt-bindings/zmk/keys.h`, `dt-bindings/zmk/pointing.h` など。
- input processor include: `input/processors.dtsi`, `dt-bindings/zmk/input_transform.h`。
- 外部モジュール include: `input_gestures_accel.dtsi`, `rgbled_widget.dtsi`, `layout_shift_kp_override.dtsi`, `mouse-gesture.dtsi`, `scroll-snap.dtsi`。
- 日本語 OS / JIS 記号向け keycode alias。
- AML (Auto Mouse Layer)、scroll layer、pointer acceleration、gesture、scroll snap。

## Input Processors の要点

公式定義では、input processors は物理またはエミュレートされた pointing device から生成される input event を処理し、必要に応じて値や種類を変更する小さな処理単位。移動量のスケール、X/Y反転、移動からスクロールへの変換、ポインタ使用中だけ一時レイヤーを有効化する用途が代表例。

公式参照:

- Input Processor Overview: https://zmk.dev/docs/keymaps/input-processors
- Input Processor Usage: https://zmk.dev/docs/keymaps/input-processors/usage
- kot149 氏の日本語チートシート: https://zenn.dev/kot149/articles/zmk-input-processor-cheat-sheet

### include

標準 processor を使うには、keymap / overlay 側で以下を include する。

```c
#include <input/processors.dtsi>
```

transformer を使う場合は、変換フラグ用に以下も必要。

```c
#include <dt-bindings/zmk/input_transform.h>
```

このリポジトリでは両方とも `config/roBa.keymap` に入っている。

### listener への割り当て

input processor は input listener に割り当てる。基本形は `input-processors` プロパティ。

```dts
&trackball_listener {
    input-processors = <&zip_xy_scaler 3 2>;
};
```

ZMK 公式では、イベント発生時に `input-processors` に並べた順で処理される。layer-specific override は listener の子ノードとして定義し、`layers` と `input-processors` を持たせる。

重要な挙動:

- 子ノード override は宣言順に評価される。
- デフォルトでは、最初に一致した override が適用されると他の override や親の base processor はスキップされる。
- `process-next;` を付けると、次の override や base processor の評価へ続く。
- override の評価順は `layers` 番号の大小ではなく、devicetree 上の宣言順。

このリポジトリでは `&trackball_listener` の各子ノードに `process-next;` があるため、複数 processor チェーンが合成される前提で読む。

## roBa 現行 input processor チェーン

### `&mkp_input_listener`

```dts
&mkp_input_listener {
    input-processors = <&zip_temp_layer MOUSE 500>;
};
```

mouse button behavior (`&mkp`) 使用時に一時的に `MOUSE` layer を有効化する設定。`MOUSE` は `#define MOUSE 7`。

### `&trackball`

```dts
&trackball {
    status = "okay";
    scroll-layers = <11>;
};
```

trackball を有効化し、scroll layer を `11` に設定している。`SCROLL` は `#define SCROLL 11`。

### `&trackball_listener`

現状の `config/roBa.keymap` では、`&trackball_listener` に base の `input-processors` が2回書かれている。

```dts
&trackball_listener {
    compatible = "zmk,input-listener";
    input-processors = <&zip_mouse_gesture>;
    input-processors = <&pointer_accel>;
    ...
};
```

devicetree では同一プロパティの再定義は後勝ちになる可能性があるため、実際に base として両方効いているかは要確認。意図が「gesture と acceleration の両方」なら、1つの `input-processors` に並べる方が安全。

候補:

```dts
input-processors = <&zip_mouse_gesture>, <&pointer_accel>;
```

ただしこれは挙動変更になるため、実装前にビルドログまたは実機挙動で確認する。

### `auto-mouse-layer`

```dts
auto-mouse-layer {
    layers = <DEFAULT MOUSE>;
    input-processors =
        <&zip_x_scaler 70 100>,
        <&zip_y_scaler 80 100>,
        <&pointer_accel>,
        <&zip_temp_layer MOUSE 50000>;
    process-next;
};
```

役割:

- `DEFAULT` または `MOUSE` layer が対象。
- X を 70%、Y を 80% にスケール。
- pointer acceleration を適用。
- trackball 入力で `MOUSE` layer を 50000ms 一時有効化。
- `process-next;` で次の override / base へ処理を継続。

### `scroller`

```dts
scroller {
    layers = <11>;
        input-processors =
            <&zip_y_scaler (-1) 1>,
            <&zip_xy_to_scroll_mapper>,
            <&scroll_inertia_v>,
            <&zip_scroll_scaler 4 675>;
    process-next;
};
```

役割:

- `SCROLL` layer (`11`) で有効。
- X/Y を入れ替え、X/Y 両方を反転。
- scroll 量を4倍。
- X/Y movement を horizontal wheel / wheel に変換。
- scroll snap を適用。

注意:

- `zip_scroll_scaler` は wheel / horizontal wheel 向けの scaler。慣性スクロール実験では記事例に合わせ、`zip_y_scaler (-1) 1` -> `zip_xy_to_scroll_mapper` -> `scroll_inertia_v` -> `zip_scroll_scaler 4 675` の順にする。
- 2026-07-11 の低速スクロール改善では、`CONFIG_PMW3610_SCROLL_TICK=4` と `zip_scroll_snap.require-n-samples=<2>` を採用した。その後の慣性スクロール実験では `scroll_inertia_v` を追加し、`zip_scroll_snap` はチェーンから外した。実機で蓄積感、慣性の尾、縦方向の向きを確認する。

### `disable-scroll-x`

```dts
disable-scroll-x {
    layers = <EXTRA_FINCTIONS>;
    input-processors = <&wheel_x_scaler 0 1>;
    process-next;
};
```

`EXTRA_FINCTIONS` layer (`9`) で horizontal wheel を 0 倍にする意図。`wheel_x_scaler` はユーザー定義 processor。

```dts
wheel_x_scaler: wheel_x_scaler {
    compatible = "zmk,input-processor-scaler";
    #input-processor-cells = <2>;
    type = <INPUT_EV_REL>;
    codes = <INPUT_REL_HWHEEL>;
    track-remainders;
};
```

## 主要 input processor 種別

### Scaler

公式参照: https://zmk.dev/docs/keymaps/input-processors/scaler

値を `multiplier / divisor` でスケールする。例:

```dts
&zip_xy_scaler 2 1
&zip_xy_scaler 1 3
```

注意:

- 公式は overflow 回避のため multiplier / divisor は最大 16 程度を推奨している。
- このリポジトリでは `&zip_x_scaler 70 100` のように 16 を超える値が使われている。これは upstream / モジュール / 実ビルドで許容されているか要確認。比率指定としては読みやすいが、公式推奨からは外れる。
- `track-remainders;` は小数相当の余りをイベント間で保持する用途。

### Transformer

公式参照: https://zmk.dev/docs/keymaps/input-processors/transformer

値または event code を変換する。主なフラグ:

- `INPUT_TRANSFORM_XY_SWAP`: X/Y を入れ替える。
- `INPUT_TRANSFORM_X_INVERT`: X を反転。
- `INPUT_TRANSFORM_Y_INVERT`: Y を反転。

このリポジトリでは scroll layer で `XY_SWAP | X_INVERT | Y_INVERT` を使っている。

### Code Mapper

公式参照: https://zmk.dev/docs/keymaps/input-processors/code-mapper

event code を別の code に変換する。代表例:

- `&zip_xy_to_scroll_mapper`: X/Y movement を horizontal wheel / wheel に変換。
- `&zip_xy_swap_mapper`: X と Y の movement code を入れ替える。

Transformer でも X/Y swap は可能だが、code mapper は「別種の event へ写す」用途で読む。

### Temporary Layer

公式参照: https://zmk.dev/docs/keymaps/input-processors/temp-layer

input event を受け取ったときに指定 layer を一時的に有効化し、一定時間 input がなければ解除する。例:

```dts
&zip_temp_layer 2 2000
```

このリポジトリでは AML の中心。

```dts
&zip_temp_layer {
    require-prior-idle-ms = <800>;
    excluded-positions = <18 19>;
};
```

意味:

- 通常キー入力から一定時間空いていないと AML を発火しない。
- 特定 key position は除外。

### Behaviors Input Processor

公式参照: https://zmk.dev/docs/keymaps/input-processors/behaviors

input event、特に `INPUT_EV_KEY` のような二値イベントを受けて、通常の ZMK behavior を発火する processor。物理 pointing device のボタンを key press や mouse click behavior に変換する用途が中心。

重要:

- 主対象は binary on/off event。
- X/Y movement のような vector type には向かない。
- split keyboard で source-specific behavior を発火する場合、central side で発火する点に注意。

このリポジトリで将来「trackball button で paste」「物理ボタンを別 behavior に変換」などをするなら候補になる。

## Behaviors の要点

公式定義では、behaviors は key position、sensor、combo などに binding され、press/release や encoder rotation で何をするかを表す。macro から再帰的に他 behavior を呼ぶこともできる。

公式参照:

- Behaviors Overview: https://zmk.dev/docs/keymaps/behaviors
- Key Press: https://zmk.dev/docs/keymaps/behaviors/key-press
- Layers: https://zmk.dev/docs/keymaps/behaviors/layers
- Hold-Tap: https://zmk.dev/docs/keymaps/behaviors/hold-tap
- Macro: https://zmk.dev/docs/keymaps/behaviors/macros
- Mod-Morph: https://zmk.dev/docs/keymaps/behaviors/mod-morph
- Mouse Emulation: https://zmk.dev/docs/keymaps/behaviors/mouse-emulation

## roBa 現行 behavior / macro の読み方

### Layer behaviors

このリポジトリでは layer locking を明示的に消している。

```dts
&to { /delete-property/ locking; };
&tog { /delete-property/ locking; };
```

公式では `&to` と `&tog` が layer を lock することがあり、`&mo` などで一時有効化した layer が解除されない挙動につながる。ここではその挙動を避ける意図と読む。

主な layer behavior:

- `&mo N`: 押している間だけ N layer。
- `&to N`: N layer に移動し、default 以外を無効化。
- `&tog N`: N layer の on/off。
- `tog_on` / `tog_off`: `toggle-mode = "on"` / `"off"` の独自 behavior。

### Hold-Tap / Layer-Tap

標準 `&mt` と `&lt` は以下。

```dts
&mt {
    tapping-term-ms = <200>;
    quick-tap-ms = <250>;
    flavor = "balanced";
};

&lt {
    tapping-term-ms = <200>;
    quick-tap-ms = <250>;
    flavor = "balanced";
};
```

独自 hold-tap:

- `lt_exit_AML_on_tap`: hold で layer、tap で key press + AML exit。
- `lt_henkan_to_0`: hold で `mo2_with_forced_exit`、tap で `kp_exit_AML`。
- `reset_bootloader`: hold で bootloader、tap 側に reset tap-dance。
- `td_rclick_close_tab_mouse_gesture`: hold で mouse gesture、tap 側に右クリック/タブ閉じ tap-dance。

Hold-tap の変更は文字入力の快適性に直結するため、`flavor`、`tapping-term-ms`、`quick-tap-ms` を変える場合は必ず実機テスト前提にする。

### Macro

公式 macro は、press/tap/release の activation mode と `macro_pause_for_release` で「押下時に前半、release 時に後半」を分けられる。

このリポジトリでは AML 解除や layer 復帰のために macro が多用されている。

重要 macro:

- `exit_AML`: `MOUSE` layer を off。
- `kp_exit_AML`: key press 後に `exit_AML`。
- `mo_exit_AML`: layer hold と AML exit を合成。
- `mo_forced_reset`: hold 中だけ layer、release 後に `&to 0 &exit_AML`。
- `mo2_with_forced_exit`: layer 2 操作用の強制復帰 macro。
- `to_nl`: `exit_AML` 後に指定 layer へ移動。

既存 README にも layer 2 から強制的に layer 0 へ戻す説明がある。文字化けしているが、`mo2_with_forced_exit` が重要な設計要素であることは確実。

### Mod-Morph

Shift など指定 modifier が押されているかで、別 behavior を発火する。

このリポジトリの例:

- `mm_minus_tilde`: 通常 `MINUS`、Shift 時 `JP_TILDE`。
- `mm_exclamation`: 通常 comma、Shift 時 `EXCL`。
- `mm_question`: 通常 period、Shift 時 `QUESTION`。
- `mm_slash`: 通常 slash、Shift 時 backslash。
- `screenshot_copy`: modifier 条件付き screenshot 系。

日本語 OS / JIS 記号 alias と強く結びついているため、記号系変更は OS 側キーボード設定と一緒に確認する。

### Tap-Dance

複数 tap 回数で behavior を変える。

このリポジトリの例:

- `td_lang_chenge`, `td_ZenkakuHankaku`: 言語切替。
- `td_muhenkan_henkan`: 無変換/変換系 layer tap の切替。
- `td_alt_f4`: F4 / Alt+F4。
- `td_bt_clear`, `td_bt_clear_all`: Bluetooth clear 系。
- `td_copy_paste`: copy / paste。
- `td_reset`: none / reset。
- `td_rclick_close_tab`: right click / close tab。

tap-dance は誤爆リスクがあるため、dangerous behavior (`BT_CLR_ALL`, reset, bootloader など) は tapping term と tap 回数を維持する。

### Mouse Emulation

`&mkp`, `&mmv`, `&msc` は mouse button / move / scroll behavior。公式 docs では、それぞれ input listener に processor を追加できる。

このリポジトリでは:

- `&mkp_input_listener` に `zip_temp_layer` を割り当て、mouse click で AML を使う。
- `&msc` は zoom macro や scroll sensor bindings で使われている。
- `mkp_with_mod` は modifier と mouse click を同時 press/release する macro。

## 外部モジュール

`config/west.yml` で ZMK `v0.3` と複数 module を指定している。

- `kumamuk-git/zmk-pmw3610-driver`: roBa の trackball sensor 系。
- `ssbb/zmk-listeners`: listener 拡張。
- `caksoylar/zmk-rgbled-widget`: LED widget。
- `4mplelab/zmk-feature-charge-indicator`: charge indicator。
- `kot149/zmk-mouse-gesture`: mouse gesture。
- `kot149/zmk-layout-shift`: layout shift / key override。
- `kot149/zmk-scroll-snap`: scroll snap。
- `oleksandrmaslov/zmk-pointing-acceleration`: pointer acceleration。
- `mjmjm0101/zmk-input-processor-scroll-inertia`: trackball scroll inertia.

ZMK input processor / behavior の公式仕様だけでは説明できない behavior があるため、問題調査時は該当 module の README と devicetree binding を確認する。

## 実装前チェックリスト

ZMK / roBa 設定を変更する前に必ず確認する。

1. `git status --short --branch`
2. `config/roBa.keymap` の対象 layer / behavior / macro / combo。
3. `config/west.yml` の module revision。
4. `boards/shields/roBa/roBa.dtsi` の physical layout と key position。
5. `keymap-drawer/roBa.yaml` / `keymap-drawer/roBa.svg` への更新要否。
6. Notion `roBa custom` へ反映すべき仕様メモがあるか。

## 現時点の注意点 / 調査候補

- `README.md` と `config/roBa.keymap` の日本語コメントに文字化けがある。修正するなら、元テキストの復元方針を決めてから行う。
- `&trackball_listener` の base `input-processors` が2回定義されている。後勝ちで `&zip_mouse_gesture` が消えている可能性がある。
- `codex/scroll-inertia-lab` では、慣性スクロール実装可否の切り分けのため、trackball 入力経路から AML、pointer acceleration、mouse gesture、scroll snap、horizontal wheel suppression を外した。`scroller` は `zip_y_scaler (-1) 1`、`zip_xy_to_scroll_mapper`、`scroll_inertia_v`、`zip_scroll_scaler 4 675` の最小構成。
- `zip_x_scaler 70 100` や `zip_y_scaler 80 100` は公式推奨の最大 16 を超える。実際に問題が出ていないなら現状維持でよいが、将来の ZMK 更新時に警戒する。
- `EXTRA_FINCTIONS` は typo らしき名前だが、define と参照が一致しているため、単純修正は破壊的変更になり得る。
- ZMK `v0.3` 固定なので、development docs と挙動差がある可能性がある。必要に応じて `v0.3` docs も確認する。

## 推奨する次の資料

必要になったら以下を追加する。

- `docs/ROBA_KEYMAP_MAP.md`: layer / combo / behavior の現行仕様一覧。
- `docs/INPUT_PROCESSOR_EXPERIMENTS.md`: trackball, AML, scroll snap, acceleration の実験ログ。
- `docs/SCROLL_INERTIA_RESEARCH.md`: trackball inertia scroll の外部モジュール調査と実験方針。
- `docs/BUILD_AND_FLASH_NOTES.md`: GitHub Actions build、local west build、左右 firmware flash 手順。
- `docs/NOTION_SYNC_NOTES.md`: Notion `roBa custom` と GitHub repo の同期方針。
