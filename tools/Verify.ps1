<#
.SYNOPSIS
  Stop, build, launch and photograph the desktop head in one pass.
.NOTES
  The running app holds a lock on Measures.exe, so it has to go before build.
#>
param(
    [string] $Name = 'latest',
    [int] $Width = 1100,
    [int] $Height = 1180,
    [int] $SettleMs = 3000,
    [switch] $NoBuild,
    [switch] $Keep,
    [int] $CropX = 0,
    [int] $CropY = 0,
    [int] $CropW = 0,
    [int] $CropH = 0
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Get-Process Measures -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (-not $NoBuild) {
    $build = dotnet build (Join-Path $root 'Measures\Measures.csproj') -f net10.0-desktop -v q -nologo 2>&1
    $errors = $build | Select-String -Pattern ': error '
    if ($errors) {
        $errors | Select-Object -First 12 | ForEach-Object { $_.Line }
        throw 'Build failed.'
    }
    'build ok'
}

$exe = Join-Path $root 'Measures\bin\Debug\net10.0-desktop\Measures.exe'
$log = Join-Path $root "tools\shots\$Name.log"
$p = Start-Process $exe -PassThru -RedirectStandardOutput $log -RedirectStandardError (Join-Path $root "tools\shots\$Name.err")

& (Join-Path $PSScriptRoot 'Capture-Window.ps1') `
    -ProcessId $p.Id `
    -Out (Join-Path $root "tools\shots\$Name.png") `
    -Width $Width -Height $Height -SettleMs $SettleMs `
    -CropX $CropX -CropY $CropY -CropW $CropW -CropH $CropH

$unexpected = Get-Content $log -ErrorAction SilentlyContinue |
    Where-Object { $_ -match 'fail:|error' -and $_ -notmatch 'RemoteControl|DevServer|HotDesign' }
if ($unexpected) { 'app log:'; $unexpected | Select-Object -First 15 }

if (-not $Keep) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
