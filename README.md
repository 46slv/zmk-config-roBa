# zmk-config-roBa

## 大西配列とCombo
大西配列かつコンボを多用するスタイルを目指しています。
人差し指から小指までが極力移動しない、理想的なキー配列を模索しております…。

### Combo
右手側


| キー | 入力 |
|---------|-------|
| 右手側 |        |
| ns | enter |
| sh | backspace |
| lb | _(underscore) |
| wr | [] |
| tn | () |
| dm | {} |
| mj | / |
|  |  |
| 左手側 |        |
| ia | space |
| ql | escape |
| zx | Ctrl Z |
|  |  |
|  |  |
|  |  |

## レイヤー2を経由してさらにレイヤーに潜る。

ホールド、リリース時にそれぞれ、
1．レイヤー2へ移動
2．レイヤー0へ移動
を設定する。
リリース時にレイヤー0へ移動することで、
レイヤー2の中でトグルレイヤーを実行してさらにレイヤーを潜ることができる。


        mo2_with_forced_exit: mo2_with_forced_exit 
            compatible = "zmk,behavior-macro";
            #binding-cells = <0>;
            wait-ms = <1>; 
            tap-ms = <1>;
            bindings =
                <&macro_tap>,
                <&exit_AML>,
                <&macro_press>,
                <&mo 2>,
                <&macro_pause_for_release>,
                <&macro_release>,
                <&mo 2>,
                <&macro_tap>,
                <&to 0 &exit_AML>;

            // レイヤー0へ強制帰還 + AML解除 
        };

また、一応AMLから脱出も仕込む。
これによって、レイヤーを切り替える＝抑えたまま のキーを1つのままあらゆるレイヤーに移動できる。


## keymap drawer
keymap drawerの描画を、
keymap_drawer.config.yamlで上書きして、jis対応描写させています。


## リンク集

### 自分用
| 項目名 | リンク |
|---------|-------|
| zmk-config-roBa | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kot149/zmk-config-roBa) |
| zmk-config-moNa2 | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kot149/zmk-config-moNa2) |
| zmk-pmw3610-driver | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kot149/zmk-pmw3610-driver) |
| Keyballキーマップ | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kot149/keyball/blob/master/qmk_firmware/keyboards/keyball/keyball39/keymaps/viax/keymap.c) |

### roBa

| 項目名 | リンク |
|---------|-------|
| Booth | <img src="https://booth.pm/favicon.ico" width="16" height="16"> [Booth](https://kumamuk.booth.pm) |
| ビルドガイド(v2) | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kumamuk-git/roBa/blob/main/doc/v2/buildguide_v2.md) |
| zmk-config-roBa | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kumamuk-git/zmk-config-roBa) |
| zmk-pmw3610-driver | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kumamuk-git/zmk-pmw3610-driver) |


### ZMK

| 項目名 | リンク |
|---------|-------|
| GitHub | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/zmkfirmware/zmk) |
| ドキュメント | <img src="https://raw.githubusercontent.com/zmkfirmware/zmk/refs/heads/main/docs/static/img/zmk_logo.svg" width="16" height="16"> [zmk.dev/docs](https://zmk.dev/docs) |
| ZMK physical layouts converter | [ZMK physical layouts converter](https://zmk-physical-layout-converter.streamlit.app) |
| Keymap Editor | [Keymap Editor](https://nickcoutsos.github.io/keymap-editor/) |
| zmk-nix | <img src="https://github.com/favicon.ico" width="16" height="16"> [zmk-nix](https://github.com/lilyinstarlight/zmk-nix) |
| zmk-rgbled-widget | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/caksoylar/zmk-rgbled-widget) |
| zmk-battery-center | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/kot149/zmk-battery-center) |

### QMK

| 項目名 | リンク |
|---------|-------|
| GitHub | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/qmk/qmk_firmware) |
| keycodes | <img src="https://github.com/favicon.ico" width="16" height="16"> [GitHub](https://github.com/qmk/qmk_firmware/blob/master/quantum/keycodes.h) |
| ドキュメント | <img src="https://docs.qmk.fm/favicon.ico" width="16" height="16"> [docs.qmk.fm](https://docs.qmk.fm) |

## キーマップ

![キーマップ画像](keymap-drawer/roBa.svg)

<img src="keymap-drawer/roBa.svg" >
