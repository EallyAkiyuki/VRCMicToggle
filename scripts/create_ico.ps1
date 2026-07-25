Add-Type -AssemblyName System.Drawing

$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $baseDir
$srcPath = Join-Path $rootDir "resources\VRCMic.png"
$icoPath = Join-Path $rootDir "resources\VRCMic.ico"

$src = [System.Drawing.Image]::FromFile($srcPath)
$pngPaths = @()
try {
    $sizes = @(16, 32, 48, 256)

    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($s, $s)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.DrawImage($src, 0, 0, $s, $s)
        $pngPath = Join-Path $env:TEMP "vrcmic_icon_$s.png"
        $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngPaths += $pngPath
        $bmp.Dispose()
        $g.Dispose()
    }
}
finally {
    $src.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
$imageData = @()
foreach ($pngPath in $pngPaths) {
    $imageData += ,[System.IO.File]::ReadAllBytes($pngPath)
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$imageData[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $imageData[$i].Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bw.Write($imageData[$i])
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$bw.Close()
$ms.Close()

foreach ($pngPath in $pngPaths) {
    Remove-Item $pngPath -Force
}

Write-Host "ICO created -> $icoPath"
