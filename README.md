# KeyboardClick — レシピ / Recap

日付: 2026-09-07

## 概要
トラックパッドの左クリック死亡対策として AHK (`keyboard-click.ahk`) を置き換える、
C# (.NET 8 / WinForms) 製のシステムトレイ常駐アプリ。

- **右Ctrl タップ** = 左クリック
- **右Ctrl 押しっぱなし + 移動** = ドラッグ (範囲選択)
- **右Shift** = 右クリック (長押しのリピートは無視 = AHK と同じ挙動)
- 左Ctrl は OS のショートカット用にそのまま
- WH_KEYBOARD_LL フックで右Ctrl/右Shift をアプリ側に渡さず消費

## ビルド
```powershell
cd C:\Users\dance\tools\KeyboardClick
dotnet publish -c Release -o app   # app\KeyboardClick.exe を生成
```
ビルド前に常駐中プロセスを停止すること (DLL ロックで失敗する):
`& "C:\Users\dance\.opencode\skills\startup-ahk-skill\keyboardclick-manage.ps1" -Action stop`

## ファイル
- `Program.cs` — 本体 (NotifyIcon トレイ / フック / ハートビート / バルーン通知 / トレイ自己修復)
- `KeyboardClick.csproj` — net8.0-windows / UseWindowsForms
- `app\KeyboardClick.exe` — 実行ファイル

## 運用 (Task Scheduler)
管理スクリプト: `C:\Users\dance\.opencode\skills\startup-ahk-skill\keyboardclick-manage.ps1`

| タスク | トリガー | 役割 |
|---|---|---|
| `KeyboardClick` | ログオン時 | アプリを起動 |
| `KeyboardClick-Watch` | 1分毎 (無期限) | watchdog: プロセス死/ハートビート停滞を検知し自動復旧 |

### アクション
- `status` — タスク/プロセス/ハートビート鮮度を表示 (既定)
- `install` / `uninstall` — ログオンタスク登録/解除
- `start` / `stop` — 起動/停止
- `watch` — watchdog を1回実行
- `watch-install` / `watch-uninstall` — watchdog タスク登録/解除

### watchdog の仕組み
- アプリが15秒毎に `%TEMP%\keyboard-click.heartbeat` へタイムスタンプ書き込み
- watch が120秒以上 stale (or プロセス無し) → 自動再起動
- メッセージループが固まるとハートビートが止まる = ホットキー失効を検知できる

## 診断
- アプリログ: `%TEMP%\keyboard-click.log` (起動/フック装着/キーイベント/失敗)
- watch ログ: `C:\Users\dance\.opencode\skills\startup-ahk-skill\keyboardclick-watch.log`
- ハートビート: `%TEMP%\keyboard-click.heartbeat`

## トレイアイコン/通知まわり (トラブルメモ)
- 起動時はウィンドウ非表示 (トレイ常駐のみ)。トレイ右クリック → 「操作パネルを表示」「フック再装着」「終了」 / ダブルクリック = パネル表示
- パネルの X / Alt+F4 で閉じてもアプリは継続 (トレイへ隠れる)。終了はメニュー/パネルの「終了」のみ
- アイコン: 青=稼働中 / 赤=停止中 (フック未装着)
- **通知バルーンは廃止 (2026-09-07)** — Do Not Disturb 常時ONのため届かない。状態はアイコン色 + ツールチップのみで確認する
- **アイコンが消える** → オーバーフロー「^」に格納されることがある。explorer再起動 + アプリ再起動で回復。
  アプリ側に30秒毎のアイコン再登録 (NudgeTray) を組み込んで自己修復
- 未解決の可能性あり: トレイにアイコンが全く見えなくなる事象が複数回発生 (要調査)。

## GUI 操作パネル (2026-09-07 追加)
起動時は非表示。トレイ ダブルクリック or 右クリック → 「操作パネルを表示」で画面右下に表示される小さなウィンドウ:
- **「開始する(ON)/停止する(OFF)」** — リマップの有効/無効をボタンで切替 (ボタン色 緑=ON / ピンク=OFF)
- **「フック再装着」** — フックを張り直し
- **「終了」** — アプリ終了
- ウィンドウを閉じる / X ボタン = アプリ終了 (トレイ奥に隠れない)
- 状態表示: 右Ctrl=左/ドラッグ, 右Shift=右クリック (ON時)

## 状態 (最終確認)
- KeyboardClick プロセス稼働中 (GUI パネル表示版 = バルーン有り・起動時表示の旧版)
- ソースはバルーン廃止 + トレイ常駐化 (起動時非表示) 済み・Debug ビルド成功 (Release 再公開は未実施)
- 旧 AHK タスクは解除済み、AHK 停止済み。AHK ファイルは互換のため残置 (`startup-ahk.ps1`)

## 今後の TODO (提案)
1. トレイアイコン非表示の根本原因調査 (UIA プローブ: TrayNotifyWnd が空に見える)
2. 必要なら「トレイに常時表示」位置へのピン留めを自動化
3. 変更をコミットする場合: git リポジトリ未初期化 (要相談)