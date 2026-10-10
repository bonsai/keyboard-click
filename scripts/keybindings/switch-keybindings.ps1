$ErrorActionPreference = 'Continue'
$exe  = 'C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe'
$main = 'C:\Users\dance\keybindings\main.ahk'
$oldS = 'C:\Users\dance\end-to-enter.ahk'

$procs = Get-CimInstance Win32_Process -Filter "name='AutoHotkey64.exe'"
$victims = @($procs | Where-Object { ($_.CommandLine -like '*end-to-enter.ahk*') -or ($_.CommandLine -like '*main.ahk*') })
if ($victims.Count -gt 0) {
  foreach ($p in $victims) {
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    Write-Output ("stopped pid=" + $p.ProcessId)
  }
} else { Write-Output "nothing to stop" }
Start-Sleep -Milliseconds 1000

Start-Process -FilePath $exe -ArgumentList $main | Out-Null
Start-Sleep -Seconds 3

$procs2 = Get-CimInstance Win32_Process -Filter "name='AutoHotkey64.exe'"
$new = @($procs2 | Where-Object { $_.CommandLine -like '*main.ahk*' })
if ($new.Count -gt 0) {
  Write-Output ("OK main pid=" + (($new | ForEach-Object { $_.ProcessId }) -join ','))
} else {
  Write-Output "FAIL main not running -> restart old"
  Start-Process -FilePath $exe -ArgumentList $oldS | Out-Null
}
