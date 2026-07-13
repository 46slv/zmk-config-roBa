# roBa ロータリーエンコーダー・スクロール最適化案

調査日: 2026-07-13  
対象: `config/west.yml` で固定している ZMK `v0.3`、roBa 左側 EC11  
状態: 調査・実験計画。本文中の設定例はまだ実装していない。

## 結論

最初に試すべきなのは、次の2点だけを変える小さな A/B テストである。

1. `triggers-per-rotation` を `10` から、EC11 の物理設定 `steps = <12>` と同じ `12` にする。
2. smooth scrolling 有効時に小さすぎる可能性がある `SCRL_UP` / `SCRL_DOWN` をやめ、
   エンコーダー専用 behavior で明示的な `MOVE_Y(...)` 速度を使う。

最初の候補値は `MOVE_Y(75)` / `MOVE_Y(-75)`、`tap-ms = <20>` とする。
ただし `75` は確定値ではなく、ZMK v0.3 の16 ms周期で「1回の短いタップから
最低1単位を作りやすい」ことを狙った実験開始値である。実機では `75`、`125`、
`250` を順に比較する。

この段階では回転速度連動の加速、慣性、外部モジュール追加、trackball の
input processor 変更を行わない。

## 最重要の体験と仮定

明示的な追加ヒアリングなしで、次を今回の仮定とする。

- 想定ユーザーは roBa を日常操作に使う本人。
- 最重要なのは、低速で1ノッチ回したときに毎回同じ量だけ、すぐスクロールすること。
- 次に重要なのは、連続して速く回しても入力が遅れて残らないこと。
- 通常レイヤーの縦スクロールを最短にする。
- volume、Page Up/Down、zoom の既存レイヤー別用途は壊さない。
- trackball の `SCROLL` layer、scroll snap、inertia 実験には影響させない。
- 保存データ、Undo、UIサイズ、外部ファイル互換性はこの変更には関係しない。
- v1では横スクロール、速度連動加速、慣性スクロールを実装しない。

## 現行構成

### ハードウェアと発火回数

`boards/shields/roBa/roBa.dtsi`:

```dts
left_encoder: encoder_left {
    compatible = "alps,ec11";
    steps = <12>;
};

sensors: sensors {
    compatible = "zmk,keymap-sensors";
    sensors = <&left_encoder &right_encoder>;
    triggers-per-rotation = <10>;
};
```

- 左側 EC11 だけが `roBa_L.overlay` で有効化されている。
- `steps = 12` は1回転あたりの電気的パルス数として扱われる。
- `triggers-per-rotation = 10` は1回転あたりに keymap behavior を発火する回数。
- 12パルスに対して10回しか発火しないため、物理ノッチが12個のEC11なら、
  1ノッチと1スクロール発火が一致しない可能性が高い。

ZMK公式は、ノッチのあるエンコーダーでは `triggers-per-rotation` をノッチ数に
合わせる考え方を示している。ただし `steps` はノッチ数ではなくデータシート上の
パルス数なので、実物が「12 pulse / 12 detent」かは実機または部品仕様で確認する。

### 通常レイヤーのスクロール behavior

`config/roBa.keymap`:

```dts
scroll_up_down: behavior_sensor_rotate_mouse_wheel_up_down {
    compatible = "zmk,behavior-sensor-rotate";
    #sensor-binding-cells = <0>;
    bindings = <&msc SCRL_UP>, <&msc SCRL_DOWN>;
    tap-ms = <20>;
};
```

`DEFAULT` layer は `sensor-bindings = <&scroll_up_down>;` を使う。

ZMK v0.3 では sensor rotate behavior が各トリガーについて、対象 behavior の
press と release を behavior queue に積む。`tap-ms` はその間隔である。
`&msc` は「1ノッチを直接送る命令」ではなく、押されている間、既定16 ms周期で
scroll速度から相対イベントを生成する two-axis input behavior である。

さらに roBa_R では `CONFIG_ZMK_POINTING_SMOOTH_SCROLLING=y` が有効である。
ZMK公式は smooth scrolling 有効時、既定値10の `SCRL_*` より、大きい
`MOVE_*` 相当の値を `&msc` に渡すことを推奨している。

### レイヤー別の現行用途

| レイヤー | エンコーダー用途 | 今回の扱い |
|---|---|---|
| `DEFAULT` (0) | `&msc SCRL_UP/DOWN` | 最適化対象 |
| `NUMPAD_AND_ARROWS` (2) | 音量 | 変更しない |
| `MISC` (4) | Ctrl+Page Up/Down | 変更しない |
| `SCROLL` (11) | Ctrl+Zoom In/Out | 変更しない |
| sensor binding未定義の層 | 下位の定義へ透過 | 変更しない |

## なぜこの順番で調整するか

### 1. 先に物理ノッチと発火回数を合わせる

速度を調整する前に、1ノッチあたりの発火が均一でなければ評価がぶれる。
物理ノッチが12なら `triggers-per-rotation = 12` を第一候補とする。

逆に、実機1回転のノッチを数えて12でなければ、その実測値を使う。
`steps` はパルス数なので、ノッチ数と必ず同じとは限らない。

### 2. エンコーダー専用の速度値を使う

`ZMK_POINTING_DEFAULT_SCRL_VAL` をグローバル変更すると、zoom macro など他の
`&msc` 利用箇所にも影響する。そのため、エンコーダー behavior の bindings にだけ
`MOVE_Y(...)` を明示する。

ZMK v0.3 の既定 `trigger-period-ms` は16 msである。一定速度の場合、概算では
1回のreport量は `速度 × 0.016` になる。したがって候補値は次のように置く。

| 候補 | 16 msあたりの概算値 | 目的 |
|---:|---:|---|
| `75` | `1.2` | 最小寄り。1ノッチの精密操作 |
| `125` | `2.0` | 中間。通常利用の本命候補 |
| `250` | `4.0` | 高速寄り。長いページ向け |

これはhost側のsmooth-scroll解釈、USB/BLE、実際のスケジューリングで体感が変わる。
計算だけで決定せず、同じページと接続方式で比較する。

### 3. `tap-ms` は速度つまみとして先に動かさない

現在の20 msは、16 ms周期のreportを1回発生させる意図として理解できる。
これを長くすると1ノッチの出力量は増えるが、sensor rotate が queue に
press/releaseを積むため、速回しで処理待ちが伸び、手を止めた後もスクロールが
残る可能性がある。これは意図した慣性ではなく入力遅延である。

まず `tap-ms = 20` を固定して速度値だけを比較する。1ノッチが欠ける場合のみ
`20 -> 24`、速回しで尾を引く場合は `20 -> 17` を一変数で試す。

### 4. ZMK標準の加速設定は今回の主解にならない

`&msc` の `time-to-max-speed-ms` と `acceleration-exponent` は、behaviorを
押し続けた時間に対する加速である。エンコーダーは短いtapを繰り返すので、
「ノブを速く回した速度」を直接測る加速ではない。さらに smooth scrolling の
resolution multiplier が有効なwheelでは、v0.3実装が加速指数を0として扱う。

本当の回転速度連動加速には、トリガー間隔を測る専用behaviorまたは外部モジュールが
必要になる。v1では依存追加と保守範囲を増やさない。

## 推奨する最小実験

以下は差分案であり、まだ適用しない。

### A. 発火回数を12へ合わせる

```diff
 sensors: sensors {
     compatible = "zmk,keymap-sensors";
     sensors = <&left_encoder &right_encoder>;
-    triggers-per-rotation = <10>;
+    triggers-per-rotation = <12>;
 };
```

### B. encoder専用の明示速度を使う

向きは現行の `SCRL_UP` / `SCRL_DOWN` と同じになるよう、正負を実機確認する。
ZMK v0.3 の `pointing.h` では `SCRL_UP` が正のY、`SCRL_DOWN` が負のYである。

```diff
 scroll_up_down: behavior_sensor_rotate_mouse_wheel_up_down {
     compatible = "zmk,behavior-sensor-rotate";
     #sensor-binding-cells = <0>;
-    bindings = <&msc SCRL_UP>, <&msc SCRL_DOWN>;
+    bindings = <&msc MOVE_Y(75)>, <&msc MOVE_Y(-75)>;
     tap-ms = <20>;
 };
```

`MOVE_Y` はすでに読み込まれている `dt-bindings/zmk/pointing.h` で定義される。

## 実験の段階

### 最小修正案（推奨）

1. 現行firmwareを戻せるよう、開始commitとUF2を記録する。
2. `triggers-per-rotation: 10 -> 12` だけ変更してbuild・実機確認する。
3. 物理ノッチと発火が揃ったら、`MOVE_Y(75)` を追加する。
4. `75 -> 125 -> 250` を一度に1値だけ変更して比較する。
5. 採用値を `ROBA_KEYMAP_MAP.md` と `ZMK_RESEARCH_NOTES.md` に同期する。

### 堅牢化案

- 物理部品の型番、1回転のノッチ数、パルス数をshield資料に記録する。
- 将来右エンコーダーを実装する場合、`&sensors` のordered child nodeで左右別の
  `triggers-per-rotation` を設定する。
- 実機試験表にUSB/BLE、Windowsアプリ別の差を残す。
- 速度候補ごとのUF2を別名で保存し、同一条件で比較する。

### 将来拡張案

- 回転間隔を測り、低速は1単位、高速は2～4単位にする専用sensor behavior。
- modifierまたは専用layerで横スクロールを選択。
- trackball慣性とは別系統のencoder加速モジュール。

これらは新規コードまたは依存追加を伴うため、実装前に別途仕様化・確認する。

## 実機確認手順

同じhost、同じ接続方式、同じアプリ・ページ位置で比較する。

1. 1ノッチずつ12回回し、各ノッチでスクロールが発生するか記録する。
2. 1回転させ、発火回数が12回相当で均一か確認する。
3. 非常にゆっくり回し、無反応ノッチや2回発火がないか確認する。
4. 1秒程度で1回転、続いてできるだけ速く2回転させる。
5. 手を止めた直後にスクロールが止まるか確認する。
6. 上下方向が現行と同じか確認する。
7. USBとBluetoothを分けて確認する。
8. `NUMPAD_AND_ARROWS` の音量、`MISC` のタブ移動、`SCROLL` のzoomが
   変わっていないことを確認する。
9. trackballの通常移動、scroll layer、AML、scroll snap／inertia実験の挙動が
   変わっていないことを確認する。

### 合格基準

- 低速12ノッチで欠け・二重発火がない。
- 速回し後、意図しないqueue遅延が体感で残らない。
- 上下方向が正しい。
- USB/BLEの少なくとも普段使う接続で違和感がない。
- 他レイヤーのencoder用途とtrackball挙動に回帰がない。

## エラー時の戻し方

- ノッチが欠ける／二重になる: `triggers-per-rotation` を元の`10`へ戻し、
  実物のノッチ数と部品仕様を再確認する。
- 1ノッチが大きすぎる: `MOVE_Y` を `250 -> 125 -> 75` の順で下げる。
- 1ノッチが出ない: `75 -> 125` を先に試し、それでも欠ける場合だけ
  `tap-ms = 24` を試す。
- 速回し後に尾を引く: `tap-ms` を長くしない。まず`20`へ戻す。
- 方向が逆: 正負だけを入れ替え、速度値を同時変更しない。
- 問題が切り分けられない: 現行commitへ戻し、1変数ずつ再開する。

## 既知リスクと未確認

- 実物EC11の型番、ノッチ数、パルス数の資料はリポジトリ内で未確認。
- `steps = 12` と物理ノッチ12個が一致するかは実機未確認。
- `MOVE_Y(75/125/250)` の体感は実機未確認。
- Windowsのアプリごとにhigh-resolution wheelの扱いが異なる可能性がある。
- BLE hostがHID resolution multiplierをどう設定するかで出力量が変わり得る。
- ZMKは`v0.3`固定なので、development版の最新ドキュメントと差があり得る。

## 参照

ZMK公式:

- [Encoder Configuration](https://zmk.dev/docs/config/encoders)
- [EC11 Encoders](https://zmk.dev/docs/hardware-integration/encoders)
- [Sensor Rotation](https://zmk.dev/docs/keymaps/behaviors/sensor-rotate)
- [Mouse Emulation Behaviors](https://zmk.dev/docs/keymaps/behaviors/mouse-emulation)
- [Pointing Device Configuration](https://zmk.dev/docs/config/pointing)
- [Major Encoder Refactor](https://zmk.dev/blog/2023/06/18/encoder-refactors)

ZMK v0.3 実装:

- [`behavior_sensor_rotate_common.c`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/src/behaviors/behavior_sensor_rotate_common.c)
- [`behavior_input_two_axis.c`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/src/behaviors/behavior_input_two_axis.c)
- [`pointing.h`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/include/dt-bindings/zmk/pointing.h)
- [`mouse_scroll.dtsi`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/dts/behaviors/mouse_scroll.dtsi)

roBa内の関連資料:

- `docs/ROBA_KEYMAP_MAP.md`
- `docs/ZMK_RESEARCH_NOTES.md`
- `docs/INPUT_PROCESSOR_EXPERIMENTS.md`
- `docs/SCROLL_INERTIA_RESEARCH.md`

