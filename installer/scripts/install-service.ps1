param(
    [Parameter(Mandatory = $true)]
    [string]$AppPath
)

$ErrorActionPreference = "Stop"

$serviceName = "Win11UpdateBlockerService"
$displayName = "Win11 Update Blocker"
$binPath = Join-Path $AppPath "service\Win11UpdateBlocker.Service.exe"

if (-not (Test-Path $binPath)) {
    throw "Service executable not found: $binPath"
}

Get-Process -Name "Win11UpdateBlocker" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

sc.exe stop $serviceName 2>$null | Out-Null
Start-Sleep -Milliseconds 750
sc.exe delete $serviceName 2>$null | Out-Null
Start-Sleep -Milliseconds 500

$result = sc.exe create $serviceName binPath= "`"$binPath`"" start= auto DisplayName= "`"$displayName`""
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe create failed: $result"
}

$result = sc.exe config $serviceName obj= LocalSystem start= auto
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe config failed: $result"
}

$result = sc.exe description $serviceName "Hält die Update-Einstellungen von Win11 Update Blocker dauerhaft aktiv."
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe description failed: $result"
}

$result = sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe failure failed: $result"
}

$started = $false
for ($attempt = 1; $attempt -le 6; $attempt++) {
    $result = sc.exe start $serviceName
    if ($LASTEXITCODE -ne 0 -and $attempt -lt 6) {
        Start-Sleep -Seconds 2
        continue
    }

    Start-Sleep -Seconds 2

    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($null -ne $service -and $service.Status -eq "Running") {
        $started = $true
        break
    }

    if ($attempt -lt 6) {
        Start-Sleep -Seconds 2
    }
}

if (-not $started) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    $status = if ($null -eq $service) { "missing" } else { $service.Status }
    throw "Service $serviceName is not running after install (status: $status)."
}

Write-Host "Service '$displayName' installed and started without reboot."
