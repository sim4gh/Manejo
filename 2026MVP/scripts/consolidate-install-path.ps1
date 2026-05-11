<#
.SYNOPSIS
  v1.7.0: consolida el install path del kiosko a C:\Tlax2026-RC\.

.DESCRIPTION
  Mueve la carpeta actual del simulador (típicamente
  C:\Users\simul\Downloads\Tlax2026MVP-v1.2.2\Tlax2026MVP\) a C:\Tlax2026-RC\,
  actualiza shortcuts y registry auto-start, deja backup de shortcuts para
  rollback.

.PARAMETER NewPath
  Path destino. Default C:\Tlax2026-RC.

.EXAMPLE
  .\consolidate-install-path.ps1

.NOTES
  Run once por kiosko. AutoUpdater de Tlax2026-RC funciona con cualquier path
  (usa Application.dataPath dinámico) — este script solo cambia DÓNDE vive el exe.
#>

[CmdletBinding()]
param(
    [string]$NewPath = 'C:\Tlax2026-RC'
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Consolidate install path -> $NewPath ===" -ForegroundColor Cyan

# 1. Kill juego si está corriendo
$proc = Get-Process -Name 'Tlax2026-RC' -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "Killing Tlax2026-RC..."
    Stop-Process -Name 'Tlax2026-RC' -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}

# 2. Detectar path actual (busca glob común)
$oldPath = $null
$exes = Get-ChildItem 'C:\Users\simul\Downloads\Tlax2026MVP-*\Tlax2026MVP\Tlax2026-RC.exe' -ErrorAction SilentlyContinue
if ($exes) { $oldPath = Split-Path ($exes | Select-Object -First 1).FullName }

if (-not $oldPath) {
    # Fallback: try other common locations
    $candidates = @(
        'C:\Tlax2026MVP',
        'C:\Users\simul\Downloads\Tlax2026-RC'
    )
    foreach ($c in $candidates) {
        if (Test-Path "$c\Tlax2026-RC.exe") { $oldPath = $c; break }
    }
}

if (-not $oldPath) {
    throw "No se pudo localizar la instalación actual. Verifica manualmente."
}
Write-Host "Old path: $oldPath" -ForegroundColor Yellow

if ($oldPath -eq $NewPath) {
    Write-Host "Ya está en $NewPath, nada que hacer." -ForegroundColor Green
    exit 0
}

# 3. Backup de shortcuts existentes
$backupDir = "$env:APPDATA\Tlax2026-RC\shortcuts-backup-$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

$shortcutPaths = @(
    "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Tlax2026-RC.lnk",
    "$env:USERPROFILE\Desktop\Tlax2026-RC.lnk"
)
foreach ($sc in $shortcutPaths) {
    if (Test-Path $sc) {
        Copy-Item $sc -Destination $backupDir -Force
        Write-Host "Backup: $sc"
    }
}

# 4. Move actual install
Write-Host "Moving $oldPath -> $NewPath ..."
Move-Item -Path $oldPath -Destination $NewPath -Force
Write-Host "Move OK" -ForegroundColor Green

# 5. Update shortcuts
$wsh = New-Object -ComObject WScript.Shell
foreach ($sc in $shortcutPaths) {
    if (Test-Path $sc) {
        $lnk = $wsh.CreateShortcut($sc)
        $lnk.TargetPath = "$NewPath\Tlax2026-RC.exe"
        $lnk.WorkingDirectory = $NewPath
        $lnk.Save()
        Write-Host "Updated shortcut: $sc"
    }
}

# 6. Update auto-start registry
$autoStartKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$existing = Get-ItemProperty $autoStartKey -ErrorAction SilentlyContinue
if ($existing -and $existing.Tlax2026) {
    Set-ItemProperty $autoStartKey -Name Tlax2026 -Value "$NewPath\Tlax2026-RC.exe"
    Write-Host "Updated auto-start registry"
}

# 7. Relaunch
Write-Host "Relaunching from new path..."
Start-Process "$NewPath\Tlax2026-RC.exe"

Write-Host "=== DONE ===" -ForegroundColor Cyan
Write-Host "Backup shortcuts en: $backupDir"
