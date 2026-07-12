# roBa 慣性スクロール導入マニュアル

更新日: 2026-07-12

## 目的

roBa の右手 PMW3610 から生の X/Y 移動量を受け取り、通常操作中は連続して
スクロールし、ボールを弾いて離した後は減速するソフトウェア慣性を出す。

この手順は、実機で通常スクロールと慣性 tail を確認した Lab 10、速度を上げた
Lab 11、元キーマップの入力機能を統合した Lab 12 を基準にしている。

## 最重要ルール

スクロール変換の所有者を ZMK input-listener だけにする。

```text
PMW3610 raw X/Y
  -> zip_xy_transform
  -> zip_xy_to_scroll_mapper
  -> scroll_inertia_v
  -> zip_scroll_scaler
  -> HID
```

`kumamuk-git/zmk-pmw3610-driver` の `scroll-layers` を有効にすると、ドライバが
先に raw X/Y を蓄積し、`WHEEL/HWHEEL = +/-1` へ量子化する。この状態で慣性
processorを追加してはいけない。速度情報が失われ、誤release、入力吸収、慣性の
不発が起きる。

## 1. Moduleを追加する

`config/west.yml` に慣性moduleを追加する。検証中に実装が変わらないよう、確認済み
commitへ固定する。

```yaml
manifest:
  remotes:
    - name: mjmjm0101
      url-base: https://github.com/mjmjm0101

  projects:
    - name: zmk-input-processor-scroll-inertia
      remote: mjmjm0101
      revision: f7dadef
      path: modules/zmk-input-processor-scroll-inertia
```

## 2. PMW3610をraw X/Yモードにする

`config/roBa.keymap` の `&trackball` から `scroll-layers` を削除する。

正しい状態:

```dts
&trackball {
    status = "okay";
};
```

使用禁止:

```dts
&trackball {
    status = "okay";
    scroll-layers = <11>;
};
```

このドライバでは `scroll-layers` はoptionalで、未指定時は空配列になる。layer 11
でもドライバは `MOVE` 経路から `INPUT_REL_X/Y` を出し、listener側の `scroller`
がscrollへ変換する。

## 3. 右手PMW3610を基準条件へ合わせる

`boards/shields/roBa/roBa_R.conf`:

```conf
CONFIG_ZMK_POINTING=y
CONFIG_ZMK_POINTING_SMOOTH_SCROLLING=y

CONFIG_PMW3610_CPI=400
CONFIG_PMW3610_CPI_DIVIDOR=1
CONFIG_PMW3610_SMART_ALGORITHM=y
CONFIG_PMW3610_POLLING_RATE_125_SW=y
```

`CPI=400` は元キーマップのcursor感を維持する値。慣性module既定値は1000 CPI
基準なので、後述のしきい値を40%へ換算する。`SMART_ALGORITHM=y` はセンサー
追跡を安定させる設定で、scroll加速processorではない。

`CONFIG_PMW3610_SCROLL_TICK` はドライバ内 `scroll-layers` を使わない構成では
active scroll量の決定に使われない。残っていてもよいが、慣性調整値として扱わない。

## 4. 慣性nodeを定義する

`boards/shields/roBa/roBa.dtsi`:

```dts
scroll_inertia_v: scroll_inertia_v {
    status = "disabled";
    compatible = "zmk,input-processor-scroll-inertia";
    #input-processor-cells = <0>;

    axis = <1>;
    layer = <11>;
    scale = <4>;
    scale-div = <75>;
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

- `axis=1`: 縦scroll専用。`axis=0` はfree 2D pan用。
- `layer=11`: layer 11を離れた瞬間に慣性状態をclearする。
- `tick=8`: 125 Hz PMW3610に合わせる。
- `scale/scale-div`: 下流scalerと必ず一致させる。
- `start=12`、`move=20` は小さい意図的flick向けにLab 15で下げた値。
- `friction=14`、`stop=3` は1000 CPI既定値の40%。
- `min-events=4` は短い高速flickを約32 msでarm可能にする。
- `gain=500`、`blend=500` は短い高速flickへの速度追従を速める。
- decayとspanはmodule既定値を使う。

右手だけでnodeを有効にする。

`boards/shields/roBa/roBa_R.overlay`:

```dts
&scroll_inertia_v {
    status = "okay";
};
```

慣性processorはhostへHIDを送るcentral側に置く。roBaでは右手buildで有効にする。

## 5. Input processor順を設定する

`config/roBa.keymap` のlayer 11 scroller:

```dts
&trackball_listener {
    compatible = "zmk,input-listener";

    scroller {
        layers = <11>;
        input-processors =
            <&zip_xy_transform (INPUT_TRANSFORM_XY_SWAP | INPUT_TRANSFORM_X_INVERT)>,
            <&zip_xy_to_scroll_mapper>,
            <&scroll_inertia_v>,
            <&zip_scroll_scaler 4 75>;

        process-next;
    };
};
```

順番を変えない。特に慣性processorはmapperの後、scalerの前へ置く。

慣性出力は下流scalerを通らず直接hostへ送られる。このため、nodeの
`scale=4 / scale-div=75` と `zip_scroll_scaler 4 75` が一致していないと、
active scrollから慣性へ移る瞬間に速度が変わる。

`INPUT_TRANSFORM_Y_INVERT` を外したLab 12は、Lab 10/11から縦方向を反転する。

## 6. Buildする

production worktreeではなく、専用lab configを使う。

```sh
cd ~/zmk-workspace
nix develop -c just init config/zmk-config-roBa-inertia-lab
nix develop -c just build roBa_R
```

出力:

```text
~/zmk-workspace/firmware/zmk-config-roBa-inertia-lab/roBa_R-seeeduino_xiao_ble.uf2
```

右手へUF2を書き込む。この変更だけなら左手buildは不要。

## 7. 実機確認

1. layer 11でゆっくり回し、停止や入力抜けなく連続scrollする。
2. 普通の速度で回し、active scroll量が扱いやすい。
3. 素早く弾いて離し、減速する慣性tailが出る。
4. 慣性中に逆方向へ回し、即座に操作を取り戻せる。
5. layer 11を離すと慣性が停止する。
6. 横方向の物理成分で不意に横scrollしない。

## 8. Scroll速度を調整する

速度はnodeと下流scalerを同時に同じ値へ変更する。

```text
4/675  Lab 10。慣性成立確認済みだが低速。
4/225  Lab 11。Lab 10の正確に3倍。
4/75   Lab 12。Lab 11の3倍。低速時のHID出力頻度を改善。
```

より速くする場合は分母を小さくする。例:

```dts
scale = <4>;
scale-div = <150>;

<&zip_scroll_scaler 4 150>;
```

一度に変更するのはscale比だけにする。CPI、`start`、`move`、decayを同時に変えない。

## 9. 加速を追加する場合

既存の `pointer_accel` をscroll chainの慣性processorより前へ追加しない。slow入力を
大きな値へ変換し、通常操作をflickと誤認させる可能性がある。

まず線形scaleと慣性で必要な移動範囲を満たせるか確認する。それでも加速が必要なら、
次の条件を満たすscroll専用出力加速として設計する。

- 慣性の速度推定はraw mapped deltaを見る。
- active出力だけを速度依存で拡大する。
- software coastにも同じ出力カーブを適用し、handoff速度を一致させる。
- cursor用 `pointer_accel` とは別instanceまたは別実装にする。

現在のmoduleはcoastを直接HID出力するため、単に下流へ加速processorを置くだけでは
active側にしか効かない。加速対応はmodule側の出力scale拡張として実装する方が安全。

## Troubleshooting

### 慣性が出ない

- `scroll-layers = <11>` がPMW3610 nodeに残っていないか確認する。
- `scroll_inertia_v` がmapperの後にあるか確認する。
- 右手overlayでnodeが `okay` か確認する。
- `axis=1` で縦wheelを追跡しているか確認する。
- layerを離す前にtailを確認する。`layer=11` はlayer offで即停止する。

### 通常scrollが途切れる

- driver内 `scroll-layers` が復活していないか確認する。
- Lab 6用の `start=0`、`move=1`、`min-events=1`、`decel-samples=1` が残って
  いないか確認する。
- `pointer_accel` や `zip_scroll_snap` をscroll chainへ戻していないか確認する。

### Activeは速いが慣性だけ遅い、またはその逆

- inertia nodeと `zip_scroll_scaler` の分子・分母を完全に一致させる。

### 低速tailがカクつく

- `stop` を下げすぎない。整数scroll単位の出力間隔が広がる。
- 先にscale比を確定し、その後で `stop` とdecayを一要素ずつ調整する。

## 確認済み基準

- Lab 10: raw input、`CPI=1000`、module defaults、scale `4/675`で連続scrollと
  software inertiaを実機確認。
- Lab 11: 同じ成立条件を維持し、active/coast出力を `4/225`へ揃えて3倍化。
- Lab 12: 元キーマップのcursor acceleration、AML、mouse gesture、横scroll制御を
  復帰。CPI 400換算しきい値、scale `4/75`、上下反転を適用。
- Lab 13: 高速flickが既定10イベントgateを満たせず停止する問題に対し、
  `min-events=4`を適用。
- Lab 14: 高速時のactive-to-coast速度段差に対し、EMAを `500/500`へ変更。
- Lab 15: 小さいflick向けに `start=12`、`move=20`へ変更。active scaleは維持。
- 慣性module revision: `f7dadef`
- PMW3610 driver revision at build time: `5e04553`

詳細な調査経緯は `docs/SCROLL_INERTIA_LAB.md`、原因と引き継ぎは
`docs/SCROLL_INERTIA_HANDOFF.md` を参照する。
