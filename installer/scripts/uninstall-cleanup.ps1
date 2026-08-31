param(
    [Parameter(Mandatory = $true)]
    [string]$AppPath
)

$ErrorActionPreference = "Continue"

$serviceName = "Win11UpdateBlockerService"

sc.exe stop $serviceName 2>$null | Out-Null
sc.exe delete $serviceName 2>$null | Out-Null

$guiCandidates = @(
    (Join-Path $AppPath "Win11UpdateBlocker.exe"),
    (Join-Path $AppPath "gui\Win11UpdateBlocker.exe")
)

foreach ($guiExe in $guiCandidates) {
    if (-not (Test-Path $guiExe)) {
        continue
    }

    $process = Start-Process -FilePath $guiExe -ArgumentList "--restore" -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        Write-Warning "Restore exited with code $($process.ExitCode) for $guiExe."
    }
    break
}

Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
    -Name "Win11 Update Blocker" -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
    -Name "Win11UpdateBlocker" -ErrorAction SilentlyContinue

$configDir = Join-Path $env:ProgramData "Win11UpdateBlocker"
if (Test-Path $configDir) {
    Remove-Item -Path $configDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Uninstall cleanup completed."
