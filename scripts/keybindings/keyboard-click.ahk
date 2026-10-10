; keyboard-click.ahk — トラックパッド左クリック死対策
; 右 Ctrl タップ = 左クリック / 右 Ctrl 押しっぱなし + 移動 = ドラッグ（範囲選択）
; 右 Shift = 右クリック
; ショートカット（Ctrl+C 等）は左 Ctrl を使用
#Requires AutoHotkey v2.0
#SingleInstance Force

RCtrl::Send("{Click down}")
RCtrl up::Send("{Click up}")
RShift::Click("right")

; ---- watchdog ハートビート ----
; 30秒毎にファイルへタイムスタンプを書き込む。
; これが stale のまま止まると watch タスク (startup-ahk.ps1 -Action watch) が再起動する。
global heartbeatFile := A_Temp "\keyboard-click.ahk.heartbeat"
SetTimer(RebootHeartbeat, 30000)

RebootHeartbeat() {
    f := FileOpen(heartbeatFile, "w")
    f.Write(A_Now)
    f.Close()
}
RebootHeartbeat()
