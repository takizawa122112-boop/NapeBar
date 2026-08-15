$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src'
$outputRoot = Join-Path $projectRoot 'build'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "C# compiler not found: $csc"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$sources = @(
    (Join-Path $sourceRoot 'NapeHid.cs'),
    (Join-Path $sourceRoot 'Program.cs')
)
$references = @(
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
)

$trayOutput = Join-Path $outputRoot 'NapeProBatteryTray.exe'
$probeOutput = Join-Path $outputRoot 'NapeBatteryProbe.exe'

& $csc /nologo /codepage:65001 /platform:x64 /optimize+ /target:winexe "/out:$trayOutput" @references @sources
if ($LASTEXITCODE -ne 0) { throw "Tray build failed: $LASTEXITCODE" }

& $csc /nologo /codepage:65001 /platform:x64 /optimize+ /target:exe /define:PROBE "/out:$probeOutput" @references @sources
if ($LASTEXITCODE -ne 0) { throw "Probe build failed: $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $outputRoot -Force
Copy-Item -LiteralPath $trayOutput -Destination (Join-Path $projectRoot 'NapeProBatteryTray.exe') -Force
Write-Host "Built: $outputRoot"
