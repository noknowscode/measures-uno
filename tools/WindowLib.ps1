<#
.SYNOPSIS
  Win32 interop shared by the capture and input scripts.

.NOTES
  A Debug Uno build owns more than one top-level window: the Uno Studio dev
  overlay is its own HWND, and Get-Process().MainWindowHandle will happily hand
  it back instead of the app. Capturing or clicking against that window silently
  targets the wrong coordinate space, so the app window is resolved by
  enumerating the process's visible windows and taking the largest.
#>

if (-not ('Native.Win32' -as [type])) {
    Add-Type -Name Win32 -Namespace Native -MemberDefinition @'
public delegate bool EnumProc(System.IntPtr hwnd, System.IntPtr param);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool EnumWindows(EnumProc callback, System.IntPtr param);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern uint GetWindowThreadProcessId(System.IntPtr hwnd, out uint pid);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool IsWindowVisible(System.IntPtr hwnd);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool GetWindowRect(System.IntPtr hwnd, out RECT rect);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool PrintWindow(System.IntPtr hwnd, System.IntPtr hdc, uint flags);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool SetWindowPos(System.IntPtr hwnd, System.IntPtr after, int x, int y, int cx, int cy, uint flags);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern System.IntPtr GetForegroundWindow();

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool SetForegroundWindow(System.IntPtr hwnd);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool BringWindowToTop(System.IntPtr hwnd);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool ShowWindow(System.IntPtr hwnd, int cmd);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern int GetSystemMetrics(int index);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern void mouse_event(uint flags, int dx, int dy, uint data, System.IntPtr extra);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern void keybd_event(byte vk, byte scan, uint flags, System.IntPtr extra);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool AttachThreadInput(uint from, uint to, bool attach);

[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern uint GetCurrentThreadId();

public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
'@
}

function Get-AppWindow {
    param(
        [Parameter(Mandatory = $true)][int] $ProcessId,
        [int] $TimeoutSeconds = 40,
        [int] $MinArea = 40000
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            throw "Process $ProcessId exited before a window appeared."
        }

        $best = [IntPtr]::Zero
        $bestArea = 0

        $callback = [Native.Win32+EnumProc] {
            param([IntPtr] $hwnd, [IntPtr] $param)

            $pid2 = 0
            [void][Native.Win32]::GetWindowThreadProcessId($hwnd, [ref]$pid2)

            if ($pid2 -ne $ProcessId -or -not [Native.Win32]::IsWindowVisible($hwnd)) { return $true }

            $r = New-Object Native.Win32+RECT
            [void][Native.Win32]::GetWindowRect($hwnd, [ref]$r)
            $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)

            if ($area -gt $bestArea) {
                $script:__bestArea = $area
                $script:__best = $hwnd
                $bestArea = $area
            }

            return $true
        }

        $script:__best = [IntPtr]::Zero
        $script:__bestArea = 0
        [void][Native.Win32]::EnumWindows($callback, [IntPtr]::Zero)

        if ($script:__bestArea -ge $MinArea) { return $script:__best }

        Start-Sleep -Milliseconds 400
    }

    throw "No app window of at least $MinArea px found on process $ProcessId."
}

function Get-Rect([IntPtr] $hwnd) {
    $r = New-Object Native.Win32+RECT
    [void][Native.Win32]::GetWindowRect($hwnd, [ref]$r)
    return $r
}

<#
  SetForegroundWindow from a background process is refused by the foreground
  lock, and the first click on an inactive window is then eaten as an activation
  click - which is indistinguishable from a dead control. Attaching to the
  current foreground thread's input queue lifts the lock, so no click is spent.
#>
function Set-AppForeground([IntPtr] $hwnd) {
    $fg = [Native.Win32]::GetForegroundWindow()
    if ($fg -eq $hwnd) { return $true }

    $other = 0
    [void][Native.Win32]::GetWindowThreadProcessId($fg, [ref]$other)
    $otherThread = [Native.Win32]::GetWindowThreadProcessId($fg, [ref]$other)
    $mine = [Native.Win32]::GetCurrentThreadId()

    [void][Native.Win32]::AttachThreadInput($mine, $otherThread, $true)
    [void][Native.Win32]::BringWindowToTop($hwnd)
    [void][Native.Win32]::SetForegroundWindow($hwnd)
    [void][Native.Win32]::AttachThreadInput($mine, $otherThread, $false)

    Start-Sleep -Milliseconds 250
    return ([Native.Win32]::GetForegroundWindow() -eq $hwnd)
}
