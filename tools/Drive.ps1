<#
.SYNOPSIS
  Synthesises pointer input against a running Measures window.

.NOTES
  Coordinates are window-relative, the same space the capture script writes, so
  a point read off a screenshot can be clicked directly.
  Movement must go through mouse_event with MOUSEEVENTF_MOVE|ABSOLUTE: a drag
  scripted with SetCursorPos arrives as a press with no movement at all, so
  selection works and dragging silently does not.
#>
param(
    [Parameter(Mandatory = $true)][int] $ProcessId,
    [Parameter(Mandatory = $true)][ValidateSet('move', 'click', 'drag', 'release', 'key')][string] $Action,
    [int] $X = 0,
    [int] $Y = 0,
    [int] $ToX = 0,
    [int] $ToY = 0,
    [int] $Steps = 24,
    [switch] $Hold,
    [int] $WakeX = 60,
    [int] $WakeY = 1050,
    [byte] $Key = 0,
    [int] $Repeat = 1
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WindowLib.ps1')

$MOVE = 0x0001; $LEFTDOWN = 0x0002; $LEFTUP = 0x0004; $ABSOLUTE = 0x8000

$hwnd = Get-AppWindow -ProcessId $ProcessId
$rect = Get-Rect $hwnd
$sw = [Native.Win32]::GetSystemMetrics(0)
$sh = [Native.Win32]::GetSystemMetrics(1)

function Move-To([int] $wx, [int] $wy) {
    $sx = [int](($rect.Left + $wx) * 65535 / $sw)
    $sy = [int](($rect.Top + $wy) * 65535 / $sh)
    [Native.Win32]::mouse_event($MOVE -bor $ABSOLUTE, $sx, $sy, 0, [IntPtr]::Zero)
}

# SetForegroundWindow from a background process is usually refused, and the
# first click on an inactive window is then swallowed as an activation click -
# which reads exactly like a dead control. Spend that click on empty page
# background before doing anything that is supposed to have an effect.
if ($Action -ne 'release') {
    if (-not (Set-AppForeground $hwnd)) {
        # the lock did not lift; fall back to spending a click on empty page
        Move-To $WakeX $WakeY
        Start-Sleep -Milliseconds 150
        [Native.Win32]::mouse_event($LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Native.Win32]::mouse_event($LEFTUP, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 400
    }
}

switch ($Action) {
    'move' {
        Move-To $X $Y
        "moved to $X,$Y (window at $($rect.Left),$($rect.Top))"
    }
    'click' {
        Move-To $X $Y
        Start-Sleep -Milliseconds 220
        [Native.Win32]::mouse_event($LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 70
        [Native.Win32]::mouse_event($LEFTUP, 0, 0, 0, [IntPtr]::Zero)
        "clicked $X,$Y (window at $($rect.Left),$($rect.Top))"
    }
    'drag' {
        Move-To $X $Y
        Start-Sleep -Milliseconds 220
        [Native.Win32]::mouse_event($LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
        for ($i = 1; $i -le $Steps; $i++) {
            $t = $i / $Steps
            Move-To ([int]($X + ($ToX - $X) * $t)) ([int]($Y + ($ToY - $Y) * $t))
            Start-Sleep -Milliseconds 25
        }
        if (-not $Hold) {
            [Native.Win32]::mouse_event($LEFTUP, 0, 0, 0, [IntPtr]::Zero)
        }
        "dragged $X,$Y -> $ToX,$ToY$(if ($Hold) { ' (button held)' })"
    }
    'release' {
        [Native.Win32]::mouse_event($LEFTUP, 0, 0, 0, [IntPtr]::Zero)
        'released'
    }
    'key' {
        for ($i = 0; $i -lt $Repeat; $i++) {
            [Native.Win32]::keybd_event($Key, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 40
            [Native.Win32]::keybd_event($Key, 0, 2, [IntPtr]::Zero)   # KEYEVENTF_KEYUP
            Start-Sleep -Milliseconds 90
        }
        "pressed key 0x{0:X2} x$Repeat" -f $Key
    }
}
