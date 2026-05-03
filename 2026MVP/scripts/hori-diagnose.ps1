# HORI Truck Control System (HPC-044U) — Diagnostico de pedales en Unity
#
# Compara enumeracion HID/PnP/INF entre la PC del usuario (donde funciona)
# y los kioskos remotos (donde Unity no detecta el throttle).
#
# Uso: copiar este archivo a la PC objetivo, click derecho > Run with PowerShell.
# El ZIP final se guarda en el Escritorio. Mandalo por SMB o email a Miguel.

$ErrorActionPreference = 'Continue'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportRoot = Join-Path $env:TEMP "hori-diagnose-$stamp"
New-Item -ItemType Directory -Force -Path $reportRoot | Out-Null

Write-Host "=== HORI Truck Diagnostic ===" -ForegroundColor Cyan
Write-Host "Computer: $env:COMPUTERNAME"
Write-Host "User:     $env:USERNAME"
Write-Host "Output:   $reportRoot"
Write-Host ""

function Save-Section {
    param([string]$Name, [scriptblock]$Block)
    $out = Join-Path $reportRoot "$Name.txt"
    Write-Host "  -> $Name" -ForegroundColor DarkGray
    try {
        & $Block 2>&1 | Out-String | Set-Content -Path $out -Encoding UTF8
    } catch {
        "ERROR: $_" | Set-Content -Path $out
    }
}

# 1) Version de Windows
Save-Section 'winver' {
    [System.Environment]::OSVersion.VersionString
    "ComputerName: $env:COMPUTERNAME"
    Get-ComputerInfo |
        Select-Object WindowsProductName, WindowsVersion, WindowsBuildLabEx, OsHardwareAbstractionLayer, OsArchitecture |
        Format-List
}

# 2) Devices HORI por VID 0F0D y por nombre
$horiDevs = Get-PnpDevice -PresentOnly | Where-Object {
    $_.InstanceId -match 'VID_0F0D' -or $_.FriendlyName -match 'HORI|Truck Control'
}

if (-not $horiDevs) {
    Write-Warning "No se encontraron devices HORI. Verifica que el wheel este conectado y encendido."
    "NO HORI DEVICES FOUND" | Set-Content (Join-Path $reportRoot 'NO_HORI_FOUND.txt')
}

Save-Section 'hori-devices' {
    if ($horiDevs) { $horiDevs | Format-List Status, Class, FriendlyName, InstanceId, Manufacturer }
    else { "(none)" }
}

# 3) Propiedades PnP completas de cada device HORI
$allProps = @()
foreach ($d in $horiDevs) {
    $props = Get-PnpDeviceProperty -InstanceId $d.InstanceId -ErrorAction SilentlyContinue
    $allProps += [pscustomobject]@{
        InstanceId   = $d.InstanceId
        FriendlyName = $d.FriendlyName
        Class        = $d.Class
        Status       = $d.Status
        Manufacturer = $d.Manufacturer
        Properties   = $props | Select-Object KeyName, Type, Data
    }
}
$allProps | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportRoot 'hori-properties.json') -Encoding UTF8

# 4) pnputil — drivers instalados (busca INF residuales de HORI)
Save-Section 'pnputil-drivers' { pnputil /enum-drivers }
Save-Section 'pnputil-drivers-hori-only' { pnputil /enum-drivers | Select-String -Context 0,8 -Pattern 'HORI|hpc|truck' }
Save-Section 'pnputil-hid-devices' { pnputil /enum-devices /connected /class HIDClass }

# 5) Registry exports — Enum\HID y Enum\USB para los devices HORI
& reg export 'HKLM\SYSTEM\CurrentControlSet\Enum\HID' (Join-Path $reportRoot 'enum-hid.reg') /y *> $null
& reg export 'HKLM\SYSTEM\CurrentControlSet\Enum\USB' (Join-Path $reportRoot 'enum-usb.reg') /y *> $null

# 6) HID Report Descriptors crudos del registro (CLAVE para diagnostico)
$hidDescriptors = @()
Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Enum\HID' -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'VID_0F0D' } |
    ForEach-Object {
        $node = $_
        $dp = Join-Path $node.PsPath 'Device Parameters'
        $entry = [ordered]@{
            FullPath        = $node.Name
            ChildName       = $node.PSChildName
        }
        if (Test-Path $dp) {
            $p = Get-ItemProperty -Path $dp -ErrorAction SilentlyContinue
            if ($p.'HidReportDescriptor') {
                $bytes = $p.'HidReportDescriptor'
                $entry.HidReportDescriptorHex = ($bytes | ForEach-Object { $_.ToString('X2') }) -join ' '
                $entry.HidReportDescriptorLen = $bytes.Length
            }
            $entry.OtherDeviceParams = $p.PSObject.Properties |
                Where-Object { $_.Name -notmatch '^PS' -and $_.Name -ne 'HidReportDescriptor' } |
                ForEach-Object { @{ Name = $_.Name; Value = $_.Value } }
        }
        $hidDescriptors += [pscustomobject]$entry
    }
$hidDescriptors | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportRoot 'hid-descriptors.json') -Encoding UTF8

# 7) USB selective suspend / power management para devices HORI
$usbProps = @()
foreach ($d in ($horiDevs | Where-Object { $_.Class -in @('USB','HIDClass') })) {
    $key = "HKLM:\SYSTEM\CurrentControlSet\Enum\$($d.InstanceId)\Device Parameters"
    if (Test-Path $key) {
        $kp = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        $usbProps += [pscustomobject]@{
            InstanceId                       = $d.InstanceId
            FriendlyName                     = $d.FriendlyName
            Class                            = $d.Class
            SelectiveSuspendEnabled          = $kp.SelectiveSuspendEnabled
            EnhancedPowerManagementEnabled   = $kp.EnhancedPowerManagementEnabled
            DeviceSelectiveSuspended         = $kp.DeviceSelectiveSuspended
        }
    }
}
$usbProps | ConvertTo-Json | Set-Content (Join-Path $reportRoot 'usb-power.json') -Encoding UTF8

# 8) Power scheme actual
Save-Section 'powercfg' { powercfg /q SCHEME_CURRENT }

# 9) Game controllers visibles (DirectInput / joy.cpl)
Save-Section 'game-controllers' {
    Get-PnpDevice -PresentOnly |
        Where-Object { $_.PNPClass -eq 'HIDClass' } |
        Where-Object { $_.FriendlyName -match 'game|joystick|controller|wheel|HORI|Truck' -or $_.InstanceId -match 'VID_0F0D' } |
        Format-List FriendlyName, InstanceId, Status, Manufacturer
}

# 10) Servicios HID-related
Save-Section 'hid-services' {
    Get-Service | Where-Object { $_.Name -match 'HID|hidserv|HidUsb|usbhub' } |
        Format-Table Name, Status, StartType, DisplayName -AutoSize
}

# 11) Drivers HID cargados (para detectar Logitech G HUB / Hori Manager)
Save-Section 'driverquery-hid' {
    driverquery /v /fo TABLE | findstr /I "HID HORI Logitech racing wheel"
}

# 12) Composicion USB completa del wheel (para ver MI_xx)
Save-Section 'usb-composition' {
    Get-PnpDevice -PresentOnly |
        Where-Object { $_.InstanceId -match 'VID_0F0D' } |
        Sort-Object InstanceId |
        Format-List InstanceId, Class, FriendlyName, Status, Service
}

# 13) DirectInput-style enumeration via WMI (Win32_PnPEntity)
Save-Section 'wmi-pnp' {
    Get-CimInstance Win32_PnPEntity |
        Where-Object { $_.PNPDeviceID -match 'VID_0F0D' -or $_.Name -match 'HORI|Truck Control' } |
        Format-List Name, PNPClass, PNPDeviceID, Manufacturer, Service, Status
}

# 14) Comprimir todo
$zipPath = Join-Path $env:USERPROFILE "Desktop\hori-diagnose-$env:COMPUTERNAME-$stamp.zip"
Compress-Archive -Path (Join-Path $reportRoot '*') -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  DIAGNOSTIC COMPLETE" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "ZIP report:" -ForegroundColor Yellow
Write-Host "  $zipPath" -ForegroundColor White
Write-Host ""
Write-Host "Mandalo a Miguel por:" -ForegroundColor Yellow
Write-Host "  - Email" -ForegroundColor White
Write-Host "  - SMB share" -ForegroundColor White
Write-Host "  - WhatsApp" -ForegroundColor White
Write-Host ""

Read-Host "Press Enter to close"
