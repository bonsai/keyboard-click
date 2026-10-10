#Requires AutoHotkey v2.0
#SingleInstance Force
; ============================================================
; keybindings\main.ahk — 唯一の起動ファイル（ローダー）
; ------------------------------------------------------------
; 構成:  このフォルダに main.ahk、bindings\ に 1バインディング1ファイル
; 追加:  bindings\ に .ahk を作り、下に #Include を1行足す
; 反映:  保存 -> トレイの Reload Script（または起動し直す）
; 正:    WSL 側 ~/.keymap/ を編集し、このフォルダへ同期する
; ============================================================

; --- バインディング（1ファイル = 1機能） ----------------------
#Include bindings\end-to-enter.ahk
; #Include bindings\capslock.ahk
; #Include bindings\launcher.ahk
; ------------------------------------------------------------

; 起動通知
TrayTip("keybindings active", "C:\Users\dance\keybindings\main.ahk", 1)

; ---- watchdog ハートビート（30秒毎） ----
global heartbeatFile := A_Temp "\keybindings.ahk.heartbeat"
SetTimer(RebootHeartbeat, 30000)

RebootHeartbeat() {
    f := FileOpen(heartbeatFile, "w")
    f.Write(A_Now)
    f.Close()
}
RebootHeartbeat()
