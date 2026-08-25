<#
.SYNOPSIS
  Captures the Measures window to PNG.

.NOTES
  PrintWindow(PW_RENDERFULLCONTENT) is occlusion-proof but can return TRUE and
  still hand back a blank bitmap on a GL-composited window, so the capture is
  sampled and retried before falling back to a foreground screen copy.
#>
param(
    [Parameter(Mandatory = $true)][int] $ProcessId,
    [Parameter(Mandatory = $true)][string] $Out,
    [int] $TimeoutSeconds = 40,
    [int] $SettleMs = 3000,
    [int] $Width = 0,
    [int] $Height = 0,
    [int] $CropX = 0,
    [int] $CropY = 0,
    [int] $CropW = 0,
    [int] $CropH = 0,
    [int] $Zoom = 1
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'WindowLib.ps1')

$hwnd = Get-AppWindow -ProcessId $ProcessId -TimeoutSeconds $TimeoutSeconds

# AppWindow.MoveAndResize is a silent no-op on net10.0-desktop; SetWindowPos on
# the host HWND is the one that actually takes.
if ($Width -gt 0 -and $Height -gt 0) {
    [void][Native.Win32]::SetWindowPos($hwnd, [IntPtr]::Zero, 40, 40, $Width, $Height, 0x0004)
    Start-Sleep -Milliseconds 900
}

# let the staggered entrance finish, or the cards photograph mid-rise
Start-Sleep -Milliseconds $SettleMs

$rect = Get-Rect $hwnd
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top

function Get-WindowBitmap([IntPtr] $target, [int] $bw, [int] $bh) {
    $bmp = New-Object System.Drawing.Bitmap $bw, $bh
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    $ok = [Native.Win32]::PrintWindow($target, $hdc, 2)  # PW_RENDERFULLCONTENT
    $gfx.ReleaseHdc($hdc)
    $gfx.Dispose()

    # a TRUE return is not evidence of pixels - sample before trusting it
    $distinct = @{}
    for ($y = 0; $y -lt $bh; $y += [Math]::Max(1, [int]($bh / 24))) {
        for ($x = 0; $x -lt $bw; $x += [Math]::Max(1, [int]($bw / 24))) {
            $distinct[$bmp.GetPixel($x, $y).ToArgb()] = $true
        }
    }

    if ($ok -and $distinct.Count -ge 4) { return $bmp }
    $bmp.Dispose()
    return $null
}

$bmp = $null
foreach ($attempt in 1..5) {
    $bmp = Get-WindowBitmap $hwnd $w $h
    if ($bmp) { break }
    Write-Host "PrintWindow came back blank (attempt $attempt); retrying."
    Start-Sleep -Milliseconds 600
}

if (-not $bmp) {
    # last resort: force the window in front, because CopyFromScreen is not
    # occlusion-proof and will happily photograph whatever is covering it
    Write-Host 'Falling back to a foreground screen copy.'
    [void][Native.Win32]::ShowWindow($hwnd, 9)
    [void][Native.Win32]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003)
    [void][Native.Win32]::BringWindowToTop($hwnd)
    [void][Native.Win32]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 1200

    $rect = Get-Rect $hwnd
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $gfx.Dispose()
    [void][Native.Win32]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0003)
}

if ($CropW -gt 0 -and $CropH -gt 0) {
    $crop = New-Object System.Drawing.Bitmap $CropW, $CropH
    $cg = [System.Drawing.Graphics]::FromImage($crop)
    $cg.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $CropW, $CropH), $CropX, $CropY, $CropW, $CropH, [System.Drawing.GraphicsUnit]::Pixel)
    $cg.Dispose()
    $bmp.Dispose()
    $bmp = $crop
    $w = $CropW; $h = $CropH
}

if ($Zoom -gt 1) {
    $big = New-Object System.Drawing.Bitmap ($w * $Zoom), ($h * $Zoom)
    $zg = [System.Drawing.Graphics]::FromImage($big)
    $zg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $zg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $zg.DrawImage($bmp, 0, 0, ($w * $Zoom), ($h * $Zoom))
    $zg.Dispose()
    $bmp.Dispose()
    $bmp = $big
    $w = $w * $Zoom; $h = $h * $Zoom
}

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

"{0}  {1}x{2}" -f $Out, $w, $h
