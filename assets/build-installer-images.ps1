param(
    [string]$AssetsDir = $PSScriptRoot
)

Add-Type -AssemblyName System.Drawing

function Save-Bmp {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $Bitmap.Dispose()
}

$iconPath = Join-Path $AssetsDir "icon-source.png"
$logoPath = Join-Path $AssetsDir "logo.png"

# Installer sidebar: 164 x 314 (Inno Setup standard)
$sidebar = New-Object System.Drawing.Bitmap 164, 314
$g = [System.Drawing.Graphics]::FromImage($sidebar)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Point]::new(0, 0),
    [System.Drawing.Point]::new(164, 314),
    [System.Drawing.Color]::FromArgb(255, 11, 61, 110),
    [System.Drawing.Color]::FromArgb(255, 0, 120, 212)
)
$g.FillRectangle($brush, 0, 0, 164, 314)
$brush.Dispose()

$icon = [System.Drawing.Image]::FromFile($iconPath)
$g.DrawImage($icon, 42, 90, 80, 80)
$icon.Dispose()

$font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$g.DrawString("Win11", $font, $textBrush, 52, 185)
$font.Dispose()

$font2 = New-Object System.Drawing.Font("Segoe UI", 7.5)
$g.DrawString("Update Blocker", $font2, $textBrush, 38, 205)
$font2.Dispose()
$textBrush.Dispose()
$g.Dispose()

Save-Bmp $sidebar (Join-Path $AssetsDir "installer-sidebar.bmp")

# Installer small image: 55 x 55
$small = New-Object System.Drawing.Bitmap 55, 55
$g2 = [System.Drawing.Graphics]::FromImage($small)
$g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$icon2 = [System.Drawing.Image]::FromFile($iconPath)
$g2.DrawImage($icon2, 0, 0, 55, 55)
$icon2.Dispose()
$g2.Dispose()

Save-Bmp $small (Join-Path $AssetsDir "installer-small.bmp")

Write-Host "Created installer-sidebar.bmp and installer-small.bmp"
