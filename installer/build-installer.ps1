param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$root = Split-Path -Parent $installerDir
$guiOut = Join-Path $root "publish\gui"
$serviceOut = Join-Path $root "publish\service"

function Test-PublishOutput {
    param(
        [string]$Directory,
        [string[]]$RequiredFiles
    )

    foreach ($file in $RequiredFiles) {
        $path = Join-Path $Directory $file
        if (-not (Test-Path $path)) {
            throw "Publish verification failed. Missing file: $path"
        }
    }

    $fileCount = (Get-ChildItem -Path $Directory -Recurse -File).Count
    if ($fileCount -lt 50) {
        throw "Publish verification failed. Too few files in ${Directory}: $fileCount"
    }

    Write-Host "Verified $Directory ($fileCount files)"
}

if (-not $SkipPublish) {
    foreach ($dir in @($guiOut, $serviceOut)) {
        if (Test-Path $dir) {
            Write-Host "Cleaning $dir..."
            Remove-Item -Path $dir -Recurse -Force
        }
    }

    Write-Host "Publishing GUI..."
    dotnet publish (Join-Path $root "src\Win11UpdateBlocker\Win11UpdateBlocker.csproj") `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $guiOut
    if ($LASTEXITCODE -ne 0) { throw "GUI publish failed." }

    Write-Host "Publishing Service..."
    dotnet publish (Join-Path $root "src\Win11UpdateBlocker.Service\Win11UpdateBlocker.Service.csproj") `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $serviceOut
    if ($LASTEXITCODE -ne 0) { throw "Service publish failed." }
}

Test-PublishOutput $guiOut @(
    "Win11UpdateBlocker.exe",
    "Win11UpdateBlocker.dll",
    "Win11UpdateBlocker.Core.dll",
    "Assets\icon.ico",
    "Assets\icon.png"
)

Test-PublishOutput $serviceOut @(
    "Win11UpdateBlocker.Service.exe",
    "Win11UpdateBlocker.Service.dll",
    "Win11UpdateBlocker.Core.dll"
)

$iconPath = Join-Path $root "assets\icon.ico"
if (-not (Test-Path $iconPath)) {
    throw "Missing installer icon: $iconPath"
}

$isccCandidates = @(
    (Join-Path $root "tools\inno-setup\ISCC.exe"),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Host "Compiling installer with ISCC..."
    & $iscc (Join-Path $installerDir "setup.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC compilation failed with exit code $LASTEXITCODE."
    }

    $setupExe = Join-Path $installerDir "output\Win11 Update Blocker Setup.exe"
    if (-not (Test-Path $setupExe)) {
        throw "Installer output missing: $setupExe"
    }

    $sizeMb = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
    Write-Host "Installer built: $setupExe ($sizeMb MB)"
}
else {
    Write-Warning "Inno Setup Compiler (ISCC) not found. setup.iss is ready but was not compiled."
    Write-Warning "Install Inno Setup 6 from https://jrsoftware.org/isinfo.php and re-run this script."
}
