param(
    [string]$SourcePath,
    [string]$IcoPath
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 256)
$source = [System.Drawing.Image]::FromFile($SourcePath)

$pngDataList = New-Object System.Collections.Generic.List[byte[]]
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()

    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngDataList.Add($pngMs.ToArray())
    $pngMs.Close()
    $bmp.Dispose()
}

$source.Dispose()

$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $pngData = $pngDataList[$i]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$pngData.Length)
    $writer.Write([uint32]$offset)
    $offset += $pngData.Length
}

foreach ($pngData in $pngDataList) {
    $writer.Write($pngData)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($IcoPath, $ms.ToArray())
$ms.Close()
$writer.Close()

Write-Host "Created $IcoPath"
