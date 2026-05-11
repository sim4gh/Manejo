<#
.SYNOPSIS
  Migración manual one-time de la PC del kiosko a una build nueva del simulador.

.DESCRIPTION
  Las PCs corriendo Tlax2026MVP <= 1.0.8 tienen un AutoUpdater bug-eado
  internamente: su update.bat usa `for /d %D in ("staging\*")` que solo
  itera subdirectorios y NUNCA copia los archivos al root del ZIP
  (Tlax2026MVP.exe, UnityPlayer.dll). Resultado: el .exe nuevo nunca llega
  al folder de instalación, la PC sigue corriendo el viejo, y el admin UI
  miente diciendo que todo se instaló.

  Este script rompe el ciclo. Una vez que el kiosko corre 1.0.10+ (con el
  bat arreglado), todos los OTA siguientes se instalan correctamente sin
  necesidad de volver a correr este script.

.PARAMETER Version
  Versión a instalar. Ej: "1.0.10". Debe existir en S3 bajo
  s3://simtabasco-firmware-ota/unity-builds/dev/<version>/.

.PARAMETER InstallDir
  Path al folder de instalación de Tlax2026MVP. Si no se da, intenta
  detectarlo via "C:\Program Files\Tlax2026MVP", "C:\Tlax2026MVP", y
  el cwd actual.

.PARAMETER Env
  dev | stage | prod. Default dev.

.EXAMPLE
  # Setup típico
  .\bootstrap-install.ps1 -Version 1.0.10 -InstallDir "C:\Tlax2026MVP"

.EXAMPLE
  # Auto-detect del install dir
  .\bootstrap-install.ps1 -Version 1.0.10

.NOTES
  Requiere PowerShell 5.1+ (incluido en Windows 10+).
  No requiere admin a menos que el InstallDir esté en Program Files.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$InstallDir,

    [Parameter(Mandatory = $false)]
    [ValidateSet('dev', 'stage', 'prod')]
    [string]$Env = 'dev'
)

$ErrorActionPreference = 'Stop'

# ── Resolver InstallDir si no fue dado ───────────────────────────────
# v1.7.0: default canónico es C:\Tlax2026-RC (no Tlax2026MVP).
if (-not $InstallDir) {
    $candidates = @(
        "C:\Tlax2026-RC",
        "$env:ProgramFiles\Tlax2026MVP",
        "C:\Tlax2026MVP",
        "$PWD"
    )
    foreach ($c in $candidates) {
        if (Test-Path "$c\Tlax2026MVP.exe") {
            $InstallDir = $c
            Write-Host "InstallDir detectado: $InstallDir" -ForegroundColor Cyan
            break
        }
    }
    if (-not $InstallDir) {
        throw "No pude detectar el folder de instalación. Pasa -InstallDir explícitamente."
    }
}

if (-not (Test-Path "$InstallDir\Tlax2026MVP.exe")) {
    throw "InstallDir '$InstallDir' no contiene Tlax2026MVP.exe — verifica el path."
}

# ── Variables ────────────────────────────────────────────────────────
$cdnDomain = "cdn.$Env.simuladores.mexicalab.com"
$zipUrl    = "https://$cdnDomain/unity-builds/$Env/$Version/Tlax2026MVP-v$Version.zip"
$zipPath   = Join-Path $env:TEMP "Tlax2026MVP-$Version.zip"
$staging   = Join-Path $env:TEMP "Tlax2026MVP-staging-$Version"

Write-Host "=== Bootstrap install Tlax2026MVP v$Version ($Env) ===" -ForegroundColor Green
Write-Host "InstallDir : $InstallDir"
Write-Host "ZIP URL    : $zipUrl"
Write-Host "Temp ZIP   : $zipPath"
Write-Host "Staging    : $staging"
Write-Host ""

# ── 1. Cerrar Tlax2026MVP si está corriendo ──────────────────────────
Write-Host "[1/6] Cerrando Tlax2026MVP si está corriendo..." -ForegroundColor Cyan
try {
    Get-Process -Name 'Tlax2026MVP' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  Matando PID $($_.Id)..."
        $_ | Stop-Process -Force
    }
    Start-Sleep -Seconds 2
} catch {
    Write-Warning "No pude cerrar el proceso (continuo de todos modos): $_"
}

# ── 2. Descargar ZIP del CDN ─────────────────────────────────────────
Write-Host "[2/6] Descargando $Version desde $cdnDomain..." -ForegroundColor Cyan
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "  Descargado: $sizeMB MB"

# ── 3. Limpiar staging y extraer ─────────────────────────────────────
Write-Host "[3/6] Extrayendo + limpiando Mark-of-the-Web..." -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
Expand-Archive -Path $zipPath -DestinationPath $staging -Force
Get-ChildItem -Path $staging -Recurse -File | Unblock-File
$fileCount = (Get-ChildItem -Path $staging -Recurse -File).Count
Write-Host "  Extraídos $fileCount archivos"

# ── 4. Copiar a InstallDir (root + subdirs preservando estructura) ──
Write-Host "[4/6] Copiando archivos a $InstallDir..." -ForegroundColor Cyan
# Copy-Item con wildcard `*` copia todo lo que está en staging — archivos
# root y subdirs por igual — preservando nombres. Equivalente a
# `xcopy /s /e /y "staging\*" "InstallDir\"` que es la fix para el bat
# bug que motiva este script.
Copy-Item -Path "$staging\*" -Destination $InstallDir -Recurse -Force
Write-Host "  Copia OK"

# ── 5. Cleanup temp ──────────────────────────────────────────────────
Write-Host "[5/6] Limpiando temporales..." -ForegroundColor Cyan
Remove-Item -Path $staging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue

# ── 6. Lanzar simulador ──────────────────────────────────────────────
Write-Host "[6/6] Lanzando $InstallDir\Tlax2026MVP.exe..." -ForegroundColor Cyan
Start-Process -FilePath "$InstallDir\Tlax2026MVP.exe"

Write-Host ""
Write-Host "Done. Revisa que Tlax2026MVP arranque y reporte v$Version en el admin." -ForegroundColor Green
Write-Host "Si Windows SmartScreen pide click ESTA VEZ, dale 'Ejecutar de todos modos'."
Write-Host "De ahora en adelante, OTA debería funcionar solo (este es el último click manual)."
