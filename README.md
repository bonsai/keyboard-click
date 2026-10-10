# KeyboardClick (monorepo)

キーボードリマップ / 右ボタンクリック代替の個人用ツール群を一括管理する monorepo。

> 注: 本リポジトリは 2026-09 時点で運用終了（C# 版が Task Scheduler 運用されていたが、以降は利用予定なし）。
> 参考用に構成と履歴を残す。詳細レシピは各サブディレクトリの README 参照。

## 構成

```
.
├── apps/
│   └── keyboard-click/    # C# (.NET 8 / WinForms) システムトレイ常駐アプリ
│       ├── KeyboardClick.csproj
│       ├── Program.cs     # 本体 (WH_KEYBOARD_LL フック / 右Ctrl=左クリック, 右Shift=右クリック)
│       └── README.md      # レシピ / Recap
├── scripts/
│   └── keybindings/       # AHK v2 キーバインド (旧方式、互換のため残置)
│       ├── main.ahk               # ローダー (bindings\ を #Include)
│       ├── keyboard-click.ahk     # 旧・左クリック代替
│       ├── end-to-enter.ahk       # 旧・End→Enter
│       └── switch-keybindings.ps1 # AHK 起動切替
└── README.md              # 本ファイル
```

## 各コンポーネント

### apps/keyboard-click
Task Scheduler（`KeyboardClick` ログオン時 + `KeyboardClick-Watch` 1分毎）で運用していた
トレイ常駐アプリ。右Ctrlタップ=左クリック、右Ctrl+移動=ドラッグ、右Shift=右クリック。
管理スクリプト: `~/.opencode/skills/startup-ahk-skill/keyboardclick-manage.ps1`

## git
- 元リポジトリ: `https://github.com/bonsai/keyboard-click.git` (branch: main)
- コミットは UTF-8 日本語メッセージ