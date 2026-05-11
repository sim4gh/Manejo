<#
.SYNOPSIS
  Mantiene el kiosko Aramis (y otros Windows kioskos) despiertos sin que entren
  en sleep/hibernate/monitor-off. Run ONCE como admin para configurar.

.DESCRIPTION
  Hace dos cosas:
  1. Deshabilita sleep/hibernate/monitor-timeout via powercfg (permanent, sobrevive reboot)
  2. Agrega scheduled task que corre un keep-alive script en boot — toca una key
     phantom cada 4 min para evitar idle disconnects de Tailscale/RDP/SSH también.

  Run como admin (Right-click PowerShell → Run as administrator).

.EXAMPLE
  PS> Set-ExecutionPolicy Bypass -Scope Process; .\keep-aramis-awake.ps1
#>

$ErrorActionPreference = 'Stop'
Write-Host "=== Keep Aramis Awake — one-time setup ===" -ForegroundColor Cyan

# 1. Disable sleep/hibernate on AC (kiosko siempre en AC, no battery)
Write-Host "Deshabilitando sleep/hibernate/monitor-timeout..."
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0
powercfg /change disk-timeout-ac 0
powercfg /change monitor-timeout-ac 0
powercfg /h off  # disable hibernate file
Write-Host "powercfg config: OK" -ForegroundColor Green

# 2. Crear script de keep-alive en disco
$keepAlivePath = "C:\Tlax2026-RC\scripts\keep-alive-loop.ps1"
$keepAliveDir = Split-Path $keepAlivePath
if (-not (Test-Path $keepAliveDir)) { New-Item -Path $keepAliveDir -ItemType Directory -Force | Out-Null }

@'
# keep-alive-loop.ps1 — corre indefinidamente, evita idle sleep + idle disconnects
Add-Type -AssemblyName System.Windows.Forms
while ($true) {
    # Mover el cursor 1px y devolverlo evita "user idle" status sin afectar UX
    $pos = [System.Windows.Forms.Cursor]::Position
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(($pos.X + 1), $pos.Y)
    Start-Sleep -Milliseconds 100
    [System.Windows.Forms.Cursor]::Position = $pos
    Start-Sleep -Seconds 240  # 4 min entre toques
}
'@ | Set-Content -Path $keepAlivePath -Encoding UTF8
Write-Host "Keep-alive script en: $keepAlivePath" -ForegroundColor Green

# 3. Scheduled Task que corre el script al boot, como SYSTEM
$taskName = "Tlax2026-KeepAlive"
$taskAction = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-WindowStyle Hidden -ExecutionPolicy Bypass -File `"$keepAlivePath`""
$taskTrigger = New-ScheduledTaskTrigger -AtStartup
$taskPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -RunLevel Highest
$taskSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries -StartWhenAvailable

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

Register-ScheduledTask -TaskName $taskName `
    -Action $taskAction `
    -Trigger $taskTrigger `
    -Principal $taskPrincipal `
    -Settings $taskSettings `
    -Description "Tlax2026: previene idle sleep del kiosko y disconnects Tailscale/SSH" | Out-Null
Write-Host "Scheduled task '$taskName' registrada (boot trigger, SYSTEM)" -ForegroundColor Green

# 4. Iniciar el task ahora mismo (sin reboot)
Start-ScheduledTask -TaskName $taskName
Write-Host "Keep-alive corriendo ahora." -ForegroundColor Green

Write-Host ""
Write-Host "=== DONE ===" -ForegroundColor Cyan
Write-Host "Verificar con:"
Write-Host "  powercfg /query   # confirmar sleep=0"
Write-Host "  Get-ScheduledTask -TaskName Tlax2026-KeepAlive"
Write-Host "  Get-Process powershell -ErrorAction SilentlyContinue | Where-Object { \$_.CommandLine -like '*keep-alive-loop*' }"
