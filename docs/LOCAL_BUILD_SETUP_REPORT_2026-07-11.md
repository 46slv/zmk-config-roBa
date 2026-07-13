# roBa Local Build Setup Report - 2026-07-11

## 目的

roBa の ZMK ファームウェアを、手元の Windows 環境から安定してローカルビルドできる状態にする。

今回の目的は「まずビルドできる土台を作る」こと。キーマップ、レイヤー、トラックボール、スクロール、Auto Mouse Layer などの挙動は変更していない。

## 今回作った構成

ビルド環境は Windows 直下ではなく、WSL Ubuntu 24.04 の中に作った。

```text
Windows
  C:\Users\shiro\Documents\GitHub\zmk-config-roBa
    編集用チェックアウト

WSL Ubuntu 24.04
  /home/shiro/zmk-workspace
    zmk-workspace 本体
    config/zmk-config-roBa
      ビルド用チェックアウト
    firmware/zmk-config-roBa
      生成された UF2
```

この形にした理由は、`zmk-workspace` が Windows では WSL ネイティブ領域での利用を推奨しており、`.west`、`zmk/`、`modules/`、`.build/`、`firmware/` などの大量の生成物を普段編集している Git 作業ツリーに混ぜないため。

重要な注意点として、Windows 側の作業ツリーと WSL 側のビルド用クローンは別物。Windows 側で未コミットの変更をしただけでは、WSL 側のビルドには入らない。

## 実施したセットアップ

1. WSL に `Ubuntu-24.04` をインストールした。
2. Ubuntu 内に通常ユーザー `shiro` を作成した。
3. Ubuntu で `systemd` を有効にした。
4. Ubuntu 内に Nix をインストールし、`nix-command` と `flakes` を有効にした。
5. `/home/shiro/zmk-workspace` に `kot149/zmk-workspace` をクローンした。
6. `/home/shiro/zmk-workspace/config/zmk-config-roBa` に `46slv/zmk-config-roBa` をクローンした。
7. `just init config/zmk-config-roBa` を実行し、ZMK 本体と外部モジュールを取得した。
8. `just list` で roBa 用ターゲットを確認した。
9. `roBa_R` と `roBa_L` のローカルビルドを実行した。
10. `zmk-pmw3610-driver/` が `zmk-workspace` 直下に取得されるため、WSL 側の `zmk-workspace/.git/info/exclude` にだけ追加し、`zmk-workspace` 本体の `git status` ノイズを消した。

## 確認できたツール

WSL Ubuntu 24.04 の Nix 開発シェル内で確認した。

```text
just 1.40.0
west v1.3.0
yq 3.4.3
Python 3.12.9
Nix 2.34.6
```

## 確認できたターゲット

`just list` で次のターゲットを確認した。

```text
seeeduino_xiao_ble,roBa_L
seeeduino_xiao_ble,roBa_R,studio-rpc-usb-uart
seeeduino_xiao_ble,settings_reset
```

通常使うファームウェアは `roBa_R` と `roBa_L`。`settings_reset` は設定消去用なので、通常ファームウェアとしては扱わない。

## ビルド結果

`roBa_R` と `roBa_L` はどちらも成功した。

### roBa_R

```text
Target: roBa_R
Board: seeeduino_xiao_ble
Shield: roBa_R
Snippet: studio-rpc-usb-uart
UF2: /home/shiro/zmk-workspace/firmware/zmk-config-roBa/roBa_R-seeeduino_xiao_ble.uf2
Size: 559616 bytes
FLASH: 279592 B / 788 KB = 34.65%
RAM: 82674 B / 256 KB = 31.54%
```

### roBa_L

```text
Target: roBa_L
Board: seeeduino_xiao_ble
Shield: roBa_L
Snippet: none
UF2: /home/shiro/zmk-workspace/firmware/zmk-config-roBa/roBa_L-seeeduino_xiao_ble.uf2
Size: 358400 bytes
FLASH: 179032 B / 788 KB = 22.19%
RAM: 33804 B / 256 KB = 12.90%
```

## 出ていた警告

ビルドは成功しており、以下は今回のセットアップを止めるエラーではない。

- devicetree の `label` property が deprecated と表示された。
- `pixart,pmw3610` の vendor prefix が未登録と表示された。
- 一部 node で duplicate unit-address warning が出た。
- `RGBLED_WIDGET_*` 系の設定が、`RGBLED_WIDGET=n` のため無視された。
- `roBa_L` では左手側 peripheral 扱いのため、USB や central battery 系の一部設定が無視された。
- Nordic / Zephyr 側の deprecated Kconfig warning が出た。
- `input_processor_temp_layer.c` で format 指定と未使用変数の warning が出た。

警告の整理は別タスクでよい。今回の目的である「ローカルビルド環境を作って UF2 を出す」は達成済み。

## 今後ビルドするときの最短手順

PowerShell から WSL に入る。

```powershell
wsl.exe -d Ubuntu-24.04
```

WSL 内でビルドする。

```bash
cd ~/zmk-workspace
nix develop -c just list
nix develop -c just build roBa_R
nix develop -c just build roBa_L
```

生成物を確認する。

```bash
ls -lh ~/zmk-workspace/firmware/zmk-config-roBa
```

## フラッシュについて

今回はフラッシュしていない。

フラッシュ前に確認すること。

- 右手側には `roBa_R-seeeduino_xiao_ble.uf2` を使う。
- 左手側には `roBa_L-seeeduino_xiao_ble.uf2` を使う。
- `settings_reset` は通常ファームウェアではないので、設定を消したいとき以外は使わない。
- Windows 側の未コミット変更を試したい場合は、WSL 側のビルド用クローンにもその変更を反映してからビルドする。
- WSL 側の `zmk-workspace` 本体は、`west` が取得した `zmk-pmw3610-driver/` をローカル exclude している。これは WSL 内の作業用設定であり、この roBa 設定リポジトリの内容は変えていない。

## 参照元

- `zmk-workspace` 解説: <https://zenn.dev/kot149/articles/zmk-workspace>
- `zmk-workspace` GitHub: <https://github.com/kot149/zmk-workspace>
- ZMK official local toolchain container setup: <https://zmk.dev/docs/development/local-toolchain/setup/container>
- ZMK official build and flash: <https://zmk.dev/docs/development/local-toolchain/build-flash>
