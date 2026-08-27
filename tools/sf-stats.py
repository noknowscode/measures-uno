"""Frame statistics for the app's SurfaceView from SurfaceFlinger TimeStats.

Usage: python tools/sf-stats.py <label> [adb shell action...] [--wait seconds]
Clears the stats, runs the optional action (an `adb shell` command such as
`input swipe 900 1000 150 1000 400`), waits, then prints the present-to-present
distribution for the Measures layer: how many frames took one vsync (16 ms),
two (33 ms), three (50 ms) and more. Objective - nothing inside the app is
subscribed to measure it, so nothing is pumping frames.
"""
import re, subprocess, sys, time

ADB = r"C:\Users\Platform006\AppData\Local\Android\Sdk\platform-tools\adb.exe"
DEVICE = "3C090DLJH000V4"

def sh(*args):
    return subprocess.run([ADB, "-s", DEVICE, "shell", *args], capture_output=True, text=True).stdout

args = sys.argv[1:]
wait = 3.0
if "--wait" in args:
    i = args.index("--wait"); wait = float(args[i + 1]); del args[i:i + 2]
label = args[0] if args else ""
action = args[1:]

sh("dumpsys", "SurfaceFlinger", "--timestats", "-enable")
sh("dumpsys", "SurfaceFlinger", "--timestats", "-clear")
if action:
    sh(*action)
time.sleep(wait)
dump = sh("dumpsys", "SurfaceFlinger", "--timestats", "-dump")

block = None
for part in dump.split("layerName = "):
    head = part.splitlines()[0]
    if "SurfaceView[com.measures.app" in head and "(BLAST)" in head and "present2present" in part:
        block = part
if block is None:
    print(f"{label}: no layer stats"); sys.exit(0)

total = int(re.search(r"totalFrames = (\d+)", block).group(1))
p2p = block.split("present2present histogram is as below:")[1].splitlines()[1]
hist = {int(k): int(v) for k, v in re.findall(r"(\d+)ms=(\d+)", p2p)}
one = hist.get(16, 0) + hist.get(17, 0)
two = sum(v for k, v in hist.items() if 30 <= k <= 36)
three = sum(v for k, v in hist.items() if 46 <= k <= 54)
more = sum(v for k, v in hist.items() if k > 54)
print(f"{label}: frames={total} 1vsync={one} 2vsync={two} 3vsync={three} worse={more}  ({(two+three+more)/max(total,1)*100:.1f}% hitched)")
