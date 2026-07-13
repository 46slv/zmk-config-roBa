# roBa ロータリーエンコーダー・スクロール最適化案

調査日: 2026-07-13  
対象: `config/west.yml` で固定している ZMK `v0.3`、roBa 左側 EC11  
状態: `codex/encoder-scroll-accel-inertia-lab`でTune 2をbuild済み。初回実機で永久scroll解消、
加速は弱く、慣性は未確認。Tune 2の実機評価とOS側ログ収集待ち。

## 結論

最初に行うべきなのは、`triggers-per-rotation` の変更ではなく、実物の
quadrature信号に対して `steps` が正しいかの確認である。関連するX投稿では
EVQWGD001に対して `steps = <48>` を使っており、ZMK v0.3のEC11 driverも
A/Bの有効な各状態遷移を1 stepとして数える。部品仕様の「12 pulse」が
1 quadrature cycleを指すなら、ZMK上の48 state transitionsと対応する可能性がある。

その後に、次の2点を小さな A/B テストとして行う。

1. 実測したdetent数に `triggers-per-rotation` を合わせる。
2. smooth scrolling 有効時に小さすぎる可能性がある `SCRL_UP` / `SCRL_DOWN` をやめ、
   エンコーダー専用 behavior で明示的な `MOVE_Y(...)` 速度を使う。

最初の候補値は `MOVE_Y(75)` / `MOVE_Y(-75)`、`tap-ms = <20>` とする。
ただし `75` は確定値ではなく、ZMK v0.3 の16 ms周期で「1回の短いタップから
最低1単位を作りやすい」ことを狙った実験開始値である。実機では `75`、`125`、
`250` を順に比較する。

初期校正では回転速度連動の加速、慣性、外部モジュール追加、trackball の
input processor 変更を行わない。低速の1ノッチが安定した後の第2段階として、
ノッチ間隔に応じて1倍、2倍、4倍、6倍へ切り替える専用one-shot behaviorを
加速の本命候補とする。さらに任意の第3段階として、最大6倍が継続した場合だけ
短い減衰tailを出す、条件付き慣性を追加できる。

## 最重要の体験と仮定

明示的な追加ヒアリングなしで、次を今回の仮定とする。

- 想定ユーザーは roBa を日常操作に使う本人。
- 最重要なのは、低速で1ノッチ回したときに毎回同じ量だけ、すぐスクロールすること。
- 次に重要なのは、連続して速く回しても入力が遅れて残らないこと。
- 通常レイヤーの縦スクロールを最短にする。
- volume、Page Up/Down、zoom の既存レイヤー別用途は壊さない。
- trackball の `SCROLL` layer、scroll snap、inertia 実験には影響させない。
- 保存データ、Undo、UIサイズ、外部ファイル互換性はこの変更には関係しない。
- 初期校正フェーズでは横スクロール、速度連動加速、慣性スクロールを実装しない。
- 第2段階の速度連動加速は`DEFAULT` layerの縦スクロールだけを対象にする。
- 第2段階までは、手を止めた後に動き続ける慣性を付けない。
- 第3段階の慣性は、最大加速が継続した場合だけ発動する任意機能とする。

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
- 現行設定は `steps = 12` だが、ZMK v0.3のEC11 driverはA/Bの有効な各状態遷移で
  `delta`を増減するため、部品資料のpulse表記をそのまま入れてよいとは限らない。
- `triggers-per-rotation = 10` は1回転あたりに keymap behavior を発火する回数。
- `steps`が誤って小さい場合、1回の物理回転を実際より大きな角度として解釈し、
  behavior発火数とqueue流入量が過剰になる。

ZMK公式は、ノッチのあるエンコーダーでは `triggers-per-rotation` をノッチ数に
合わせる考え方を示している。ただし先に `steps` を信号遷移数へ正しく校正する。
実物のpulse、detent、1 pulseあたりのA/B遷移数は部品仕様と実機ログで確認する。

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

## 関連X投稿の事例と永久回転の分析

### 投稿から確認できた症状

- [2025-06-22の投稿](https://x.com/Abashiri_Life_C/status/1936679054901752308)
  では、ロータリーを速く回すとスクロールが回り続け、roBaの電源を落とすまで
  止まらない症状が報告されている。
- 投稿画像の対策例は `steps = <48>`、`ZMK_POINTING_DEFAULT_SCRL_VAL 30`、
  `tap-ms = <100>` の組み合わせだった。
- この対策で起こりにくくなったが、回し続けると再発すると報告されている。
- [2025-06-24の投稿](https://x.com/Abashiri_Life_C/status/1937482878071566519)
  では、一定数を超えた入力を遮断する案が仮説として挙げられている。

投稿の設定と現行roBa設定は同一ではないため、投稿だけから原因を断定しない。
ただしZMK v0.3実装と症状を照合すると、behavior queue飽和が最有力候補になる。

### 最有力仮説: pressは入ったがreleaseだけqueueから落ちる

ZMK v0.3では、sensor rotateが1 triggerごとに次を別々のqueue itemとして追加する。

1. `&msc` press。次へ進むまで `tap-ms` 待つ。
2. `&msc` release。待ち時間0。

behavior queueの既定容量は64 item、つまり空の状態から最大約32 tap pairである。
queue追加はwaitなしで失敗し得るが、sensor rotate側は戻り値を確認していない。
したがってqueueの残りが1 itemのとき、pressだけ成功してreleaseが失敗する経路が
存在する。この場合 `&msc` の内部speedが0へ戻らず、入力が止まった後も16 ms周期で
scroll eventを出し続ける。電源断やresetでだけ止まる症状と一致する。

これはソースから導いた推論であり、roBa実機ログによる確定診断ではない。
ただし次の実装がこの経路を裏付ける。

- [`behavior_sensor_rotate_common.c` (v0.3)](https://github.com/zmkfirmware/zmk/blob/v0.3/app/src/behaviors/behavior_sensor_rotate_common.c):
  press/releaseを別々に追加し、戻り値を無視する。
- [`behavior_queue.c` (v0.3)](https://github.com/zmkfirmware/zmk/blob/v0.3/app/src/behavior_queue.c):
  容量固定のmessage queueへ`K_NO_WAIT`で追加し、満杯ならエラーを返す。
- [`app/Kconfig` (v0.3)](https://github.com/zmkfirmware/zmk/blob/v0.3/app/Kconfig):
  `CONFIG_ZMK_BEHAVIORS_QUEUE_SIZE`の既定値は64。

現行main branchでも2026-07-13確認時点の該当処理は同じ形なので、ZMKをmainへ
更新するだけでこの経路が自動解決するとは判断できない。

### なぜ投稿の対策で発生しにくくなったのか

- `steps: 12 -> 48` は、同じ物理回転をより細かな角度として解釈し、
  `triggers-per-rotation`へ到達するまでの入力遷移数を増やす。値が実物の
  quadrature仕様と合っているなら、これは遮断ではなく正しい校正である。
- scroll値`30`は、1 triggerあたりの出力量を増やし、少ないtriggerでも
  実用速度を得やすくする。
- `tap-ms = 100` は1 triggerの出力量を増やす一方、queueが1 tapを処理する時間も
  長くする。queueの処理能力は概算10 tap/sまで下がるため、速回し時の飽和には
  不利である。

つまり `steps = 48` とscroll値増加が流入量を減らす方向に働き、長い`tap-ms`が
queue排出を遅くする方向に働く。通常操作では改善し、激しい連続回転では再発する
という報告と整合する。

### 接点チャタリングとの切り分け

機械式encoderにはchattering/bouncingがあり、短いノイズをdebounceする設計は
一般的である。ZMK v0.3のEC11 driverは有効なquadrature状態遷移だけを増減するが、
時間ベースのdebounce設定は持たない。したがって接点ノイズが余分なtriggerを作り、
queue飽和を早める可能性はある。

一方、接点ノイズだけでは「手を離した後も永久に同方向scrollを出し続ける」状態を
直接説明しにくい。永久回転はrelease欠落、通常より多いtriggerはsteps不整合や
chatteringとして、別々に観測する。

市販マウスも一律に「一定数を超えたら全入力を遮断」しているとは限らない。
一般的には、部品品質、hardware/software debounce、quadratureの有効遷移判定、
report頻度の制御を組み合わせる。入力上限を設ける場合も、既に受理したpressの
releaseを捨てないことが必須である。

### 「一定数を超えた入力を遮断する」案の評価

単純な件数cutoffは推奨しない。pressをqueueへ入れた後にreleaseを遮断すると、
今回疑っている永久回転をむしろ作れる。

安全なrate limitにするなら、次の条件が必要になる。

- queue投入前にtap pair全体を受理するか捨てるか決める。
- pressとreleaseをatomicに予約する。
- 満杯時は新しいtap全体をdropし、既存のreleaseを優先する。
- encoder停止後に`&msc` speedが必ず0へ戻るwatchdogまたはforced releaseを持つ。

標準devicetree設定だけでは、このatomic pair制御は追加できない。ZMK core patchか、
queueを使わず1回の有限scroll eventを直接出すencoder専用behaviorが必要になる。

### 解決案の優先順位

1. **信号校正**: EVQWGD001の正確なpulse/detent仕様を確認し、ZMK driverが数える
   state transitionsに`steps`を合わせる。
2. **再現ログ**: queue追加失敗をログ出力する診断buildで、永久回転時に
   queue fullが出るか確認する。
3. **設定内の緩和**: `tap-ms = 100`は採用せず、現行20 ms付近を維持する。
   scroll量は`MOVE_Y(...)`側で調整する。
4. **一時的診断**: `CONFIG_ZMK_BEHAVIORS_QUEUE_SIZE`を128へ増やし、再発までの
   時間だけが延びるか比較する。延びるならqueue飽和仮説を支持するが、恒久対策にはしない。
5. **堅牢な本命**: queueを使わないone-shot encoder scroll behavior、または
   press/releaseをatomicに扱うbatch enqueue APIを実装する。
6. **補助対策**: 実機で余分な方向反転や多重triggerが観測された場合に限り、
   debounceまたは有効遷移filterを追加する。

## なぜこの順番で調整するか

### 1. 先にpulse、state transition、物理ノッチを合わせる

速度を調整する前に、1ノッチあたりの発火が均一でなければ評価がぶれる。
まず `steps` をdriverが数えるstate transition数へ合わせ、その後、物理ノッチが
12なら `triggers-per-rotation = 12` を候補とする。

逆に、実機1回転のノッチを数えて12でなければ、その実測値を使う。
部品資料のpulse数、ZMKが数えるstate transition数、detent数は同じとは限らない。

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
必要になる。初期校正とは分けて実装・評価する。

## 回転速度連動加速の設計案

### 目指す操作感

実現可能であり、roBaのスクロール用途とも相性がよい。ここでいう「大きく回す」は
回転角の総量ではなく、短時間に複数ノッチを連続入力した状態として判定する。

- 1ノッチずつゆっくり回す: 常に1倍。狙った位置へ細かく合わせられる。
- 素早く連続回転する: 2倍、4倍、6倍へ段階的に上げ、長いページを速く移動する。
- 回転を止める: 即時停止する。慣性や処理待ちを残さない。
- 逆方向へ回す: 即座に1倍へ戻し、行き過ぎを細かく戻せるようにする。
- 音量、Page Up/Down、zoom: 現行behaviorを維持し、加速させない。

ノッチ間隔で倍率を決める設計には先例がある。Android Automotiveの公式ロータリー
実装も、今回と対象機器は異なるが、前ノッチとの時間間隔を基準に2倍・3倍へ
加速する。この方式なら、単なる累積入力数よりユーザーの回す速さを直接反映できる。

### 初期候補の速度テーブル

以下は実装確定値ではなく、実機A/Bテストの開始値である。

| 前ノッチからの時間 `dt` | 倍率 | 意図 |
|---:|---:|---|
| `180 ms`以上 | 1倍 | 精密操作 |
| `100–179 ms` | 2倍 | やや速い連続回転 |
| `55–99 ms` | 4倍 | 明確な速回し |
| `55 ms`未満 | 6倍 | 最大加速。出力量を上限固定 |

判定の急変とチャタリング誤検出を抑えるため、次の状態管理を加える。

1. 前回ノッチの時刻と方向を記録する。
2. 同方向の速い入力が2回続いてから、4倍以上へ上げる。
3. `280 ms`以上入力が空いたら1倍へresetする。
4. 方向が変わったら、間隔に関係なくその最初のノッチを1倍にする。
5. 1ノッチあたりの倍率は6倍を上限とし、入力回数に応じて無制限に増やさない。

概念上の処理は次のとおりである。

```text
dt = current_time - previous_time

if direction_changed or dt >= reset_ms:
    multiplier = 1
else:
    multiplier = select_from_dt(dt)
    multiplier = apply_fast_streak_guard(multiplier)

emit_one_finite_scroll(direction * base_delta * multiplier)
```

### 永久スクロールを避ける実装境界

この加速を現行のsensor rotate → queued `&msc` tapの延長で作らない。
`tap-ms`を長くしたり、速回し時に同じtapを多数queueへ積んだりすると、見かけ上は
加速しても、停止後の遅延とpress/release欠落リスクが増える。

堅牢化案では、encoder triggerを受けた専用behaviorが次を1回で行う。

- trigger時刻から倍率を決める。
- 倍率を掛けた有限のwheel差分を1 reportとして送る。
- 押下中speedやrelease待ちを保持しない。
- 異常な入力でも倍率と1 reportの出力量を上限固定する。
- backpressureが必要な場合は、press/releaseの片方ではなくone-shot event全体を捨てる。

これにより、加速と永久スクロール対策を同じ設計で両立できる。ZMK標準設定だけで
近似する最小案は、`tap-ms`を20 ms付近に固定し`MOVE_Y(...)`を大きくする方法だが、
これは全ノッチが同じ量になるため真の速度連動加速ではない。

### 実装案の3段階

1. **最小修正案**: `steps`、`triggers-per-rotation`、固定`MOVE_Y(...)`を校正する。
   追加コードなし。ただし速度連動加速はできない。
2. **堅牢化案**: repo内にone-shot encoder scroll behaviorを追加し、上の速度テーブル、
   reset、方向反転、倍率上限を実装する。`DEFAULT` layerだけを置き換える。
3. **将来拡張案**: 閾値、倍率、reset時間、最大出力量をdevicetree/Kconfigから調整可能にし、
   USB/BLE別の実測ログを残せる診断機能を追加する。

現時点の推奨は、初期校正を終えてから2へ進むこと。固定速度の土台が不安定なまま
加速を加えると、欠け、二重発火、queue飽和の原因を切り分けにくくなる。

## 最大加速時だけの条件付き慣性

### 実現できることと判定できないこと

実現可能である。ただし「最大スクロール」には2通りの意味があるため、区別する。

- **最大加速段階に達した**: firmware内部で6倍を選んだこととして確実に判定できる。
- **ページ最下端／最上端に達した**: keyboardは相対wheel eventを送るだけで、host側の
  ページ位置が返ってこないため判定できない。

したがって、ここでは「6倍の最大加速が同方向へ数ノッチ続いたときだけ慣性を予約する」
方式を採用候補とする。1倍、2倍、4倍の操作や、6倍へ一瞬触れただけの操作では
慣性を出さない。

### 推奨する発動条件

初期候補は次のとおり。すべて実機調整前の開始値である。

| 項目 | 初期候補 | 目的 |
|---|---:|---|
| 慣性を予約する倍率 | 6倍のみ | 精密・中速操作へ影響させない |
| 6倍の連続回数 | 同方向に3ノッチ | 1回だけの速い入力やbounceを除外 |
| 停止判定 | 最後のノッチから`80 ms`無入力 | 次の物理ノッチを待ってからcoast開始 |
| tailの間隔 | `24 ms` | report数を抑えつつ短い減衰を見せる |
| tailの倍率列 | `4 -> 3 -> 2 -> 1` | 最大入力より弱い速度から減衰する |
| 最大tail時間 | `96 ms` | 行き過ぎを抑え、永久出力を防ぐ |
| 最大tail総量 | base deltaの10倍 | 異常時も有限量に制限する |

この短いtailで効果が弱い場合も、最初から時間を延ばさない。まず倍率列を
`5 -> 4 -> 3 -> 2 -> 1`へ変更し、その後にtick間隔または最大時間を一変数ずつ調整する。

### 状態遷移

```text
IDLE
  -> TRACKING        1/2/4倍、または6倍が3回未満
  -> MAX_ARMED       同方向の6倍が3ノッチ継続
  -> COASTING        80 ms無入力後、4/3/2/1倍を順にone-shot出力
  -> IDLE            tail完了
```

新しいencoder入力は、同方向でも逆方向でも、まず予定済みtailをcancelしてから処理する。
逆方向入力、encoder用途が変わるlayerへの移動、USB/BLE切断、suspendでも即cancelする。
これにより、古い方向の慣性と新しい手動入力が重なることを防ぐ。

### 永久スクロール対策との両立

条件付き慣性も、現行のqueued `&msc` pressを長く保持して作ってはいけない。
one-shot加速behaviorの内部でZephyr delayed work相当を使い、各tail tickを有限の
wheel reportとして直接出す。

- tail開始時に残りtick数と最大終了時刻を固定する。
- tickごとに残りtick数を減らし、0なら必ず終了する。
- cancel時はscheduled workを無効化し、世代番号を更新して古いcallbackを無視する。
- 異常経路でも`96 ms`または4 tickを超えて出力しない。
- press/release状態や`&msc`の内部speedを慣性状態として利用しない。

既存のtrackball慣性研究で扱っている「停止検出、遅延tick、減衰wheel出力」という
考え方は再利用できる。ただしtrackball用input processorのnodeや状態をencoderと
共有しない。encoderはdetent時刻と方向が明確なので、専用behavior内で独立して
実装した方が判定とcancel条件を単純にできる。

### 推奨順序

1. 固定速度で`steps`と物理detentを校正する。
2. 慣性なしの1/2/4/6倍加速を実装し、停止時に完全に止まることを確認する。
3. 6倍が3ノッチ続いたかをlogだけで観測し、誤判定がないか確認する。
4. `4 -> 3 -> 2 -> 1`の短いtailを有効化する。
5. USB/BLEと複数アプリで行き過ぎを比較し、一変数ずつ調整する。

加速と慣性を同時に初回実装すると、tailなのかqueue遅延なのか切り分けにくい。
必ず慣性なしの加速を基準buildとして残す。

## テストブランチへの実装結果

### ブランチと変更範囲

2026-07-13に`main`から`codex/encoder-scroll-accel-inertia-lab`を作成し、
外部moduleを追加せず、このrepository自身のZephyr moduleへ専用behaviorを実装した。

- `src/behavior_roba_encoder_scroll.c`: sensor値の受理、有限HID report、停止検出、coast。
- `src/roba_encoder_scroll_math.h`: 速度段階、streak、方向反転、idle reset、有限tailの純粋ロジック。
- `dts/bindings/behaviors/zmk,behavior-roba-encoder-scroll.yaml`: devicetree binding。
- `config/roBa.keymap`: `DEFAULT`の`&scroll_up_down`だけを新behaviorへ置換。
- `tests/encoder_scroll/`: firmwareに依存しないhost test。
- `zephyr/*`: local moduleのbinding/sourceをbuildへ公開。

次は変更していない。

- `boards/shields/roBa/roBa.dtsi`の`steps = <12>`。
- `triggers-per-rotation = <10>`。
- layer 2の音量、layer 4のPage Up/Down、layer 11のencoder zoom。
- trackballの`mapper -> roba_scroll` processor chainと、その慣性設定。
- `config/west.yml`のmodule revision。

信号校正前にbehaviorを実装したのは、このlabでは一度にhardware解釈まで変えず、
現行入力に対するqueue除去・加速・有限tailだけを分離評価するためである。
実機で欠けや二重発火があれば、加速値より先に`steps`とtrigger数を再調査する。

### 実装した動作

```text
遅い／最初のdetent       1x
dt < 180 ms             2x
dt < 100 ms             4x（fast intervalが2回続いてから）
dt < 55 ms              6x（fast intervalが2回続いてから）
idle 280 ms／方向反転    1xへreset
6xが3回継続             inertia arm
arm後80 ms無入力         4x -> 3x -> 2x -> 1xの有限tail
```

active scrollもtailも`&msc`をpressしない。各deltaを1回のmouse wheel reportとして送り、
送信直後にreport値を0へ戻す。tailは最大4 reportで、layer変更またはendpoint変更時に
pending workと加速状態をresetする。新しい手動入力はpending stop/tailを先にcancelする。

### 自動確認結果

- `make -C tests/encoder_scroll clean test`: 成功。
  - 1x精密操作。
  - 180/100/55 msの境界。
  - 4x/6x前の2 interval guard。
  - 6x 3回でだけinertia arm。
  - 方向反転と280 ms idle reset。
  - `4/3/2/1`の有限tail。
- `just build roBa_R`: 成功。新sourceのcompile/link、生成DTS、
  `CONFIG_ROBA_ENCODER_SCROLL=y`を確認。
- `just build roBa_L`: 成功。peripheral側でも新sourceとEC11をcompile/linkし、
  central専用HID APIを呼ばないことを確認。
- 生成物:
  - `~/zmk-workspace/firmware/zmk-config-roBa/roBa_R-seeeduino_xiao_ble.uf2`
  - `~/zmk-workspace/firmware/zmk-config-roBa/roBa_L-seeeduino_xiao_ble.uf2`
  - 加速のみ: `roBa_R-encoder-accel-only.uf2` / `roBa_L-encoder-accel-only.uf2`
  - 加速＋条件付き慣性: `roBa_R-encoder-accel-inertia.uf2` /
    `roBa_L-encoder-accel-inertia.uf2`

比較用4ファイルは同じsourceと閾値から作成し、差は`inertia-enabled;`の有無だけにした。
最終working treeと通常名UF2は「加速＋条件付き慣性」の状態である。

まだ確認していないのは、実機の1ノッチ量、速度段階の体感、USB/BLE差、
最大速回し後の有限停止、既存encoder用途とtrackballへの回帰である。

### A/B方法

- **加速だけ**: `roBa_R-encoder-accel-only.uf2`と
  `roBa_L-encoder-accel-only.uf2`を使用する。
- **加速＋条件付き慣性**: 現行lab設定のままbuildする。
  保存済み比較用ファイルは`roBa_R-encoder-accel-inertia.uf2`と
  `roBa_L-encoder-accel-inertia.uf2`。
- **main baseline**: `main`の`zmk,behavior-sensor-rotate`＋`&msc`を使う。

比較時は`steps`、`triggers-per-rotation`、base deltaを同時に変えない。

### 2026-07-13 Tune 2

初回実機評価では永久scroll対策が機能した。一方、加速感が弱く、条件付き慣性を
確認できなかった。閾値またはstreak条件へ実際のencoder eventが到達していない可能性を
優先し、base delta、`steps`、`triggers-per-rotation`は維持したまま次だけを変更する。

| 項目 | 初回 | Tune 2 | 狙い |
|---|---:|---:|---|
| 2x境界 | `180 ms` | `240 ms` | 通常の連続回転で加速へ入りやすくする |
| 4x境界 | `100 ms` | `140 ms` | 中速と速回しの差を感じやすくする |
| 6x境界 | `55 ms` | `80 ms` | 分割通信を含む実eventで最大段へ届きやすくする |
| idle reset | `280 ms` | `320 ms` | 広げた2x境界との間を確保する |
| fast streak | `2` | `1` | 最初の一致intervalから4x/6xを許可する |
| inertia streak | `3` | `2` | 最大速回しの短いgestureでもarm可能にする |
| stop | `80 ms` | `70 ms` | 手を離した後の待ちを短くする |
| tail | `4/3/2/1`, `24 ms` | `6/5/4/3/2/1`, `28 ms` | 有限のまま認識できる強さと長さにする |

Windows側では`tools/encoder_scroll_monitor.ps1`を使い、低速・中速・高速・最大速から
手を離す操作をphase付きJSONLへ記録する。最初はOSへ届いたwheel eventだけを測り、
既存USB status protocolへ診断fieldを追加しない。詳細は
`docs/ENCODER_SCROLL_MONITORING.md`を参照する。

Tune 2のhost testと左右firmware buildは成功した。生成DTSで選択値と
`inertia-enabled;`、左右`.config`で`CONFIG_ROBA_ENCODER_SCROLL=y`を確認した。

- `~/zmk-workspace/firmware/zmk-config-roBa/roBa_R-encoder-tune2.uf2`
- `~/zmk-workspace/firmware/zmk-config-roBa/roBa_L-encoder-tune2.uf2`

### 2026-07-13 Tune 3

Tune 2を書き込んだ後、`tools/encoder_scroll_monitor.ps1`でslow / medium / fast /
max-releaseをJSONL記録した。max-releaseでは最後に
`45 -> 37 -> 30 -> 22 -> 15 -> 7`の有限tailが出ており、Windows側のwheel delta換算で
firmwareの`6 -> 5 -> 4 -> 3 -> 2 -> 1` tailが動作していることを確認できた。

一方でslow phaseにも最大deltaとtail signatureが多く出たため、Tune 2は最大速度と慣性に入り
やすすぎる。最有力の原因は、1回のaccepted sensor eventに複数triggerが含まれた場合に、
Tune 2がtriggerごとに疑似時刻を作って`quick_streak` / `max_streak`を複数回進めていたこと。
この場合、実際には1回のセンサーbatchでも、firmware内部では高速な連続detentのように扱われる。

Tune 3ではTune 2の閾値を変えず、速度評価の単位だけを変更する。

- `base-delta=1`、`steps=12`、`triggers-per-rotation=10`は維持。
- `240/140/80 ms`、`reset=320 ms`、`fast-streak=1`、`inertia-streak=2`は維持。
- `process_sensor_data()`はaccepted sensor eventごとに1回だけ速度判定する。
- event内に複数triggerがある場合も、出力report数は維持し、全reportに同じmultiplierを使う。
- acceleration / inertia streakはtrigger数ではなくaccepted event数で進む。

これにより、低速や不規則な操作で偶発的に`6x`とtailへ入る可能性を下げる。高速に複数eventが
届く操作では、実event間隔に基づく加速は残る。Tune 3の実機評価では、同じ4 phaseのJSONLを再取得し、
slow phaseの`45` delta比率とtail signature回数が下がるかを確認する。

### 2026-07-13 Tune 3再計測とTune 4

Tune 3の再計測では、slow 276 event（interval中央値28 ms）、medium 253 event（27 ms）、
fast 952 event（3 ms）を取得した。max-releaseは50 eventで、最後に
`45 -> 37 -> 30 -> 22 -> 15 -> 7`が約28 ms間隔で並んだ。したがって有限慣性は動作しており、
tailの長さや停止判定を変更する必要はない。

一方、active入力は多くのgestureで`7 -> 45`、firmware倍率では`1x -> 6x`へ直接上がっていた。
中間倍率は主に停止後tailとして現れ、加速の立ち上がりとして見えにくい。Tune 4はこの一点だけを
修正し、同方向のfast eventを次の順で上限設定する。

1. 最初のfast interval: `2x`
2. 次のfast interval: `4x`
3. 以降: timing判定の上限まで、最大`6x`

`240/140/80 ms`、reset `320 ms`、sensor batch単位の評価、6x 2回での慣性arm、
70 ms停止と`6/5/4/3/2/1x` tailは変更しない。方向反転・idle・新規入力によるreset/cancelも維持する。

## 推奨する最小実験

以下は差分案であり、まだ適用しない。

### A. `steps` を実信号へ合わせる

EVQWGD001が1回転12 quadrature cyclesで、ZMK driverが4 state transitionsずつ
数えることをログで確認できた場合の候補。型番・信号仕様を確認せず適用しない。

```diff
 left_encoder: encoder_left {
     compatible = "alps,ec11";
-    steps = <12>;
+    steps = <48>;
 };
```

### B. 発火回数をdetent数へ合わせる

以下の`12`は例である。実物のdetent数を使う。

```diff
 sensors: sensors {
     compatible = "zmk,keymap-sensors";
     sensors = <&left_encoder &right_encoder>;
-    triggers-per-rotation = <10>;
+    triggers-per-rotation = <12>;
 };
```

### C. encoder専用の明示速度を使う

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
2. debug logで1回転のstate transitionsと物理detent数を数える。
3. `steps`だけを実測値へ合わせてbuild・実機確認する。
4. `triggers-per-rotation`だけを実測detent数へ合わせる。
5. 物理ノッチと発火が揃ったら、`MOVE_Y(75)` を追加する。
6. `75 -> 125 -> 250` を一度に1値だけ変更して比較する。
7. 10秒程度の速回し後に完全停止することを確認する。
8. 採用値を `ROBA_KEYMAP_MAP.md` と `ZMK_RESEARCH_NOTES.md` に同期する。

### 堅牢化案

- 物理部品の型番、1回転のノッチ数、パルス数をshield資料に記録する。
- 将来右エンコーダーを実装する場合、`&sensors` のordered child nodeで左右別の
  `triggers-per-rotation` を設定する。
- 実機試験表にUSB/BLE、Windowsアプリ別の差を残す。
- 速度候補ごとのUF2を別名で保存し、同一条件で比較する。

### 将来拡張案

- 初期校正後、前節の1/2/4/6倍one-shot sensor behaviorを実装する。
- modifierまたは専用layerで横スクロールを選択。
- trackball慣性とは別系統のencoder加速設定として管理する。

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

### 加速behavior実装後の追加確認

1. 低速で12ノッチ回し、全ノッチが同じ1倍量であることを確認する。
2. 同方向へ4～8ノッチ素早く回し、1倍から上限6倍まで段階的に増えることを確認する。
3. 速回しの直後に停止し、最後のreport後にスクロールが残らないことを確認する。
4. 加速中に逆方向へ1ノッチ回し、そのノッチが1倍へresetされることを確認する。
5. 10秒間できるだけ速く回し、永久スクロール、queue増大、resetが起きないことを確認する。
6. `179/180 ms`など閾値付近を繰り返し、倍率が過度に行き来しないことを確認する。
7. USBとBluetoothで倍率の立ち上がりと最大量を比較する。

### 条件付き慣性実装後の追加確認

1. 1倍、2倍、4倍で止め、tailが一切出ないことを確認する。
2. 6倍へ1～2ノッチだけ入り、tailが出ないことを確認する。
3. 同方向の6倍を3ノッチ続けて止め、約80 ms後に`4 -> 3 -> 2 -> 1`だけ出ることを確認する。
4. `MAX_ARMED`中に逆回転し、旧方向のtailが出ず、逆方向の最初が1倍になることを確認する。
5. `COASTING`中に新しく回し、残りtailが即cancelされて手動入力が優先されることを確認する。
6. `MAX_ARMED`または`COASTING`中にencoder用途が異なるlayerへ移動し、tailが止まることを確認する。
7. 10秒間の連続速回しを10回繰り返し、各回のtailが4 tick、96 ms、総量10倍以内で終了することを確認する。

### 合格基準

- 低速12ノッチで欠け・二重発火がない。
- 速回し後、意図しないqueue遅延が体感で残らない。
- 上下方向が正しい。
- USB/BLEの少なくとも普段使う接続で違和感がない。
- 他レイヤーのencoder用途とtrackball挙動に回帰がない。
- 慣性なしの加速behaviorでは、低速が1倍、逆転が1倍reset、停止が即時である。
- 10秒の連続速回し後も入力が残らず、最大倍率が6倍を超えない。
- 条件付き慣性では6倍3ノッチ未満にtailがなく、発動時も4 tick／96 ms以内で終了する。

## エラー時の戻し方

- ノッチが欠ける／二重になる: `steps`と`triggers-per-rotation`を元へ戻し、
  pulse、state transition、detentを別々に再確認する。
- 1ノッチが大きすぎる: `MOVE_Y` を `250 -> 125 -> 75` の順で下げる。
- 1ノッチが出ない: `75 -> 125` を先に試し、それでも欠ける場合だけ
  `tap-ms = 24` を試す。
- 速回し後に尾を引く: `tap-ms` を長くしない。まず`20`へ戻す。
- 方向が逆: 正負だけを入れ替え、速度値を同時変更しない。
- 問題が切り分けられない: 現行commitへ戻し、1変数ずつ再開する。

## 既知リスクと未確認

- Tune 4はmain統合を許容できる操作感だが、極端な連続速回しでは有限のwheel reportが
  一時的に蓄積したように感じる場合がある。永久スクロールは再発していないため、現時点では
  入力遮断を追加せず、再現条件とJSONLを揃えてからrate limitまたはreport coalescingを検討する。
- 実物encoderが投稿画像と同じEVQWGD001かはリポジトリ内で未確認。
- EVQWGD001のexact variantにおけるpulse、detent、state transition数は未確定。
- 現行`steps = 12`と候補`steps = 48`のどちらが実信号に一致するかは実機未確認。
- behavior queue fullと永久回転の同時発生は実機ログで未確認。
- `MOVE_Y(75/125/250)` の体感は実機未確認。
- 加速閾値`180/100/55 ms`、reset`280 ms`、最大6倍は実機未調整の開始値。
- 条件付き慣性の6倍3ノッチ、停止判定80 ms、tail 4 tick／96 msも実機未調整。
- encoderの短いtailでも、アプリ側smooth scrollingと重なって長く感じる可能性がある。
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
- [Zephyr Workqueue Threads: delayable workとcancel](https://docs.zephyrproject.org/latest/kernel/services/threads/workqueue.html)

ZMK v0.3 実装:

- [`behavior_sensor_rotate_common.c`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/src/behaviors/behavior_sensor_rotate_common.c)
- [`behavior_queue.c`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/src/behavior_queue.c)
- [`ec11.c`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/module/drivers/sensor/ec11/ec11.c)
- [`behavior_input_two_axis.c`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/src/behaviors/behavior_input_two_axis.c)
- [`pointing.h`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/include/dt-bindings/zmk/pointing.h)
- [`mouse_scroll.dtsi`](https://github.com/zmkfirmware/zmk/blob/v0.3/app/dts/behaviors/mouse_scroll.dtsi)

関連事例・hardware資料:

- [永久回転と設定画像の投稿（2025-06-22）](https://x.com/Abashiri_Life_C/status/1936679054901752308)
- [入力遮断案の投稿（2025-06-24）](https://x.com/Abashiri_Life_C/status/1937482878071566519)
- [Android Automotive: ノッチ間隔によるロータリー加速](https://source.android.com/docs/automotive/hmi/rotary_controller/oem_integration#rotation)
- [Microchip AN2805: encoderを例にしたdebouncing解説](https://onlinedocs.microchip.com/oxy/GUID-7FD12F93-9B28-4EF0-B36F-3A147484BEA1-en-US-3/GUID-5B5C8194-1C66-43F0-A557-ADC76635525B.html)
- [Panasonic EVQW series](https://industry.panasonic.com/global/en/products/control/encoders-potentiometers/encoders/number/evqw)

roBa内の関連資料:

- `docs/ROBA_KEYMAP_MAP.md`
- `docs/ZMK_RESEARCH_NOTES.md`
- `docs/INPUT_PROCESSOR_EXPERIMENTS.md`
- `docs/SCROLL_INERTIA_RESEARCH.md`
