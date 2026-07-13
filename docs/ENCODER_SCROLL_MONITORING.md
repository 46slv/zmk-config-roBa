# roBa Encoder Scroll Monitoring

## 目的

ロータリーエンコーダーの加速と慣性を、体感だけでなくWindowsへ届いたwheel eventの
時刻とdeltaで確認する。ファームウェア内部の診断通信はまだ追加せず、最初はOS側の
出力だけを記録する。

この監視で確認できること:

- 低速・中速・高速でevent間隔がどう変わるか。
- 高速回転時に大きなdeltaまたは短い間隔のeventが現れるか。
- 手を離した後に有限の追加eventが出ているか。
- 無限または長時間継続する出力がないか。

確認できないこと:

- firmware内部で計算した生の`dt`。
- `quick_streak`、`max_streak`、`inertia_armed`の内部値。
- 複数のmouse deviceを自動で識別すること。

OS側ログだけで原因を特定できない場合は、次段階として診断専用USB HID reportへ
内部値を出す。既存のroBa status protocolは変更しない。

## 前提

- Windows 11の通常PowerShellで実行する。
- 外部moduleやPython packageは不要。
- capture中はroBaのエンコーダーだけを回す。mouse wheelやtrackball scrollを触ると
  同じログへ記録される。
- 出力先の`artifacts/`はGit管理対象外。

## 単独テスト

repository rootから実行する。

```powershell
.\tools\encoder_scroll_monitor.ps1 -Phase slow -DurationSeconds 10
```

開始表示の直後から10秒間、一定の低速で回す。出力は次の場所へ保存される。

```text
artifacts\encoder-scroll-monitor\encoder-scroll-YYYYMMDD-HHMMSS.jsonl
```

既存ファイルへ別phaseを追記する場合:

```powershell
$log = '.\artifacts\encoder-scroll-monitor\manual-test.jsonl'
.\tools\encoder_scroll_monitor.ps1 -Phase slow -DurationSeconds 10 -OutputPath $log
.\tools\encoder_scroll_monitor.ps1 -Phase medium -DurationSeconds 10 -OutputPath $log -Append
.\tools\encoder_scroll_monitor.ps1 -Phase fast -DurationSeconds 10 -OutputPath $log -Append
.\tools\encoder_scroll_monitor.ps1 -Phase max-release -DurationSeconds 10 -OutputPath $log -Append
```

`max-release`では、開始後に大きく速く回し、途中で完全に手を離す。残り時間は
触らない。これにより、回転中のeventと有限tailを同じphaseで比較できる。

## 推奨テスト順

1. `slow`: 1ノッチずつ明確に止めながら回す。
2. `medium`: 普段の文書スクロール程度で連続回転する。
3. `fast`: 指で速く連続回転する。
4. `max-release`: 最大速度で回して手を離す操作を複数回行う。

各phaseは上方向と下方向を分けると解析しやすい。最初の測定では方向より速度差を
優先し、一方向だけでもよい。

## JSONL形式

各実行の先頭に`record_type=session`、続いてwheel eventごとに
`record_type=wheel`を記録する。

主なfield:

- `phase`: `slow` / `medium` / `fast` / `max-release` / `custom`。
- `elapsed_ms`: capture開始からの経過時間。
- `interval_ms`: 同じaxisの直前eventからの時間。最初は`-1`。
- `axis`: `vertical`または`horizontal`。
- `delta`: Windows low-level mouse hookが受けたwheel delta。
- `flags`: Windowsが通知したmouse hook flag。

## Codexとの解析手順

1. 上記4 phaseを同じJSONLへ保存する。
2. 保存先をCodexへ伝える。
3. Codexがphase別のevent数、interval分布、delta分布、停止後のtailを比較する。
4. 実測値を根拠に`two-x-ms`、`four-x-ms`、`six-x-ms`、streak、tailだけを
   一度に1項目ずつ調整する。

OSがwheel eventを合成して内部状態を判断できない場合だけ、診断用firmwareへ進む。
