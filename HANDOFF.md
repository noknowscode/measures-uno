# HANDOFF — Measures Uno port
Updated: 2026-08-27 11:50

## Where we are

All ten delta briefs (`docs/deltas/`) are implemented on `master` on top of the Soft
Materialism retheme; the app runs on desktop and on the Pixel 8. This session added the
Android head (transparent status bar, self-padded inset), a phone carousel (one card at a
time below 620 epx, `FlipView` + pips, night plot scrubs on press), a Dark/Light toggle, a
fixed Replay (`Anim.Run` stops the previous HoldEnd board on the same target/property),
cards that survive being re-seated (Loaded/Unloaded symmetric), per-tile grain opacity
(no full-screen layer per frame), stadium pills (`CornerRadius` 14, since 999 goes oval on
Skia), and an audit pass (dead members, unused brushes, scaffold usings, 0 warnings).

Animation smoothness was investigated with objective SurfaceFlinger stats
(`tools/sf-stats.py`): on the Debug build the entrance runs at 60 fps with 1.6 % doubled
frames, swipe 3–4 %, idle 5–10 %. The stagger the user saw coincided with screen recording.
A Release measurement is blocked by the environment (see Next actions).

## Last verified state

- Build: `net10.0-desktop` 0 warnings; tests 24/24. `net10.0-android` last built clean at
  `b6c5381`; later Android builds on this PC fail with `paging file is too small` in the
  manifest-merger JVM (commit-limit exhaustion, not a code error).
- Runtime desktop (App MCP, `tools/shots/d-*.png`): bench, pins, range switch, dark, replay.
- Runtime Pixel 8 (`tools/shots/px-*.png`): inset, carousel + pips, centred card with full
  foot padding, Dark/Light, replay mid-entrance, stadium pills. The APK on the device is the
  A/B experiment build with the grain layer collapsed; the next successful install restores it.
- Git: `master` at `6b3b9d2` plus this handoff; untracked `tools/shots/*.png` from the session.

## Next actions (in order)

1. Environment: `dotnet workload update` (the mono toolchain manifest 10.0.101 pins runtime
   packs 10.0.1, which no longer exist; SDK 10.0.303 ships 10.0.11, so Release Android cannot
   restore) and free commit memory / enlarge the pagefile so Android builds run again.
2. Build a Release APK with `-c Release -p:FrameWatch=true`, install, and run
   `python tools/sf-stats.py "release replay" input tap 948 206 --wait 3` (and idle, swipe).
   That number decides whether anything in the app needs tuning. If it does: raise
   `BreathClock` 30→60 Hz on phones, audit per-frame allocations in the runner/march.
3. Rebuild Debug Android to put the grain back on the device.
4. Uno.Sdk 6.6.42 → 6.7.18 (latest stable, checked 2026-08-27); re-verify the gotchas pinned
   on 6.6.42 (StrokeDashArray, CharacterSpacing, ManipulationMode capture, CornerRadius oval).
5. Device pass: real-finger carousel swipe (a 200 ms synthetic flick jumped two cards once),
   night scrub now that it starts on press, pips placement (under header vs under card).
6. Still open from the delta pass: ridge AVG label can touch the line at the left end;
   SMAX 6 clip; in-wash grain not built; time scrubber is experimental and not built.

## Open questions

- Should the pips ride under the card rather than under the header on the phone?
- Replay on the phone replays all four cards; only the visible one is seen. Keep, or replay
  the current card only?
- The Uno usage rules discourage an in-app theme toggle; it was explicitly requested and stays.

## Relaunch

```powershell
cd C:\Users\Platform006\UnoProjects\measures-uno
dotnet build .\Measures\Measures.csproj -f net10.0-desktop
.\tools\Verify.ps1 -Name check                      # stop, build, launch, PrintWindow capture
$env:MEASURES_THEME='dark'; .\tools\Verify.ps1 -Name dark -NoBuild
# Android (Pixel 8 serial 3C090DLJH000V4):
dotnet build .\Measures\Measures.csproj -f net10.0-android -t:Install -p:AdbTarget="-s 3C090DLJH000V4"
python tools\sf-stats.py "label" input swipe 900 1000 150 1000 400 --wait 1.5
```

App MCP: `uno_app_start` on `Measures\Measures.csproj` / `net10.0-desktop`; drive chart
buttons with `uno_app_pointer_click` in client coordinates (peer invokes return template
Grids). Stop the running app before a desktop build or the exe copy fails on the lock.
`adb` is at `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe` (not on PATH).
