param(
    [Parameter(Position = 0)]
    [ValidateSet("Debug", "Release", IgnoreCase = $true)]
    [string]$Configuration = "Release"
)

# Normalize to PascalCase for consistent output paths
$Configuration = (Get-Culture).TextInfo.ToTitleCase($Configuration)

$outDir = "bin\$Configuration"
$out = "$outDir\VRCMic.exe"
$sourceFiles = @("src\Program.cs", "src\SettingsWindow.cs", "src\ColorPickerDialog.cs", "src\AppVersion.cs")

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$found = $false
foreach ($dir in @(
    "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319",
    "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319"
)) {
    $candidate = Join-Path $dir "csc.exe"
    if (Test-Path $candidate) {
        $csc = $candidate
        $refDir = $dir
        $found = $true
        Write-Host "Using compiler: $csc" -ForegroundColor DarkGray
        break
    }
}
if (-not $found) {
    Write-Host "ERROR: csc.exe not found in any .NET Framework 4.0+ directory." -ForegroundColor Red
    Write-Host "Please install .NET Framework 4.0+ SDK or build tools." -ForegroundColor Red
    exit 1
}

foreach ($f in $sourceFiles) {
    if (-not (Test-Path $f)) {
        Write-Host "ERROR: Source file not found: $f" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

$buildArgs = @("/nologo", "/target:winexe", "/unsafe", "/langversion:4", "/platform:anycpu", "/out:$out")

if ($Configuration -eq "Debug") {
    $buildArgs += "/define:DEBUG"
    $buildArgs += "/debug+"
    $buildArgs += "/optimize-"
    Write-Host "  DEBUG symbols: ON" -ForegroundColor DarkGray
    Write-Host "  Optimize: OFF" -ForegroundColor DarkGray
    Write-Host "  Debug info: ON" -ForegroundColor DarkGray
} else {
    $buildArgs += "/optimize+"
    $buildArgs += "/debug-"
    Write-Host "  DEBUG symbols: OFF" -ForegroundColor DarkGray
    Write-Host "  Optimize: ON" -ForegroundColor DarkGray
    Write-Host "  Debug info: OFF" -ForegroundColor DarkGray
}

if (Test-Path "resources\VRCMic.ico") {
    $buildArgs += "/win32icon:resources\VRCMic.ico"
} else {
    Write-Host "WARNING: resources\VRCMic.ico not found, building without icon." -ForegroundColor Yellow
}
$buildArgs += "/reference:$refDir\System.Windows.Forms.dll"
$buildArgs += "/reference:$refDir\System.Drawing.dll"
$buildArgs += "/reference:$refDir\System.dll"
$buildArgs += $sourceFiles

& $csc @buildArgs

if ($LASTEXITCODE -eq 0) {
    $fileSize = (Get-Item $out).Length
    Write-Host ""
    Write-Host "Build OK ($Configuration) -> $out ($fileSize bytes)" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
