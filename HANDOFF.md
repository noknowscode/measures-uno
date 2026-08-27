# HANDOFF — Measures Uno port
Updated: 2026-08-27 18:30

## Where we are

All ten delta briefs are in, plus the Soft Materialism retheme, the Android head, Dark/Light,
Replay, the audit pass and the frame-measurement tooling (see git history). This session
(afternoon, 2026-08-27, `70806d9`) changed the page's shape:

- **One card at a time on every width.** The bench is gone; the `FlipView` carousel hosts
  the four cards on desktop too. Arrow keys (Left/Right/Home/End on `Root`) turn it, the
  pips are 24 epx buttons around a 5 epx dot, and the FlipView's stock Fluent arrows are
  zeroed out on layout (`HideStockArrows`, by part name, MaxWidth/MaxHeight 0 - plain
  Visibility gets re-shown on pointer-over).
- **Three breakpoints.** Narrow < 620: compact head (no tagline), pips. Medium >= 620:
  tagline, 56 padding. Wide >= 1100: an index rail (`Index`, 220 col) at the left with each
  instrument's name and live reading (`BuildIndex`/`PaintIndex`, `IndexButtonStyle`), the
  current one in ink; pips hidden.
- **Fifth stop: the overview.** Last carousel item, a 2x2 of second `MeasureCard`s
  (`*Tile`, charts `*Mirror`) fed the same records (`Mirror(...)` in `Bind`/`ShowRidge`/
  `ShowSessions`); one scrolling column below 620. A tap on a tile head opens that card
  (the plot area belongs to the chart, so tap the head/numeral/foot). Replay replays tiles too.
- **Cards scale their chrome.** `MeasureCard.Fit` sets numeral size, paddings, chart gap and
  MinHeight from width (density 0.78..1.5 of the 420 design; `MaxWidth` 720). Labels and
  whispers stay at their sizes on purpose.
- Dropped by the user: an "echoes" backdrop (card silhouette repeated faintly behind the
  card). Reverted, not to be revisited unasked.

## Last verified state

- Build: `net10.0-desktop` 0 warnings; tests 24/24.
- Runtime desktop (`tools/shots/b2-*.png`, `b3-*.png`, `o-1400-*.png`): 440 / 800 / 1400
  layouts, rail click -> Sleep, Right arrow -> Running, arrows hidden under pointer,
  overview at 1400, tile tap -> Sleep card. All through the App MCP against a Debug exe
  launched by `tools/Verify.ps1 -Keep`.
- Android: **not rebuilt since the carousel/overview change** (commit-limit build failure
  persists, see below). The APK on the device is still the older A/B grain build.

## Next actions (in order)

1. Phone pass once Android builds again: five pips, overview as one scrolling column at
   density 0.78 (check `48 sessions` fits at ~370 epx), night scrub on the mirror tile,
   swipe still reaching the overview.
2. Environment: `dotnet workload update` (mono manifest 10.0.101 pins runtime 10.0.1; SDK
   10.0.303 ships 10.0.11) and free commit memory / enlarge the pagefile.
3. Release APK `-c Release -p:FrameWatch=true`, then `python tools/sf-stats.py ...` for
   replay / idle / swipe. Tune only if the numbers say so.
4. Uno.Sdk 6.6.42 -> 6.7.18; re-verify the gotchas pinned on 6.6.42 (StrokeDashArray,
   CharacterSpacing, ManipulationMode capture, CornerRadius oval, FlipView arrow part names).
5. Still open from the delta pass: ridge AVG label can touch the line at the left end;
   SMAX 6 clip; in-wash grain; time scrubber.

## Open questions

- Wide: should the rail's readings follow a pin/hover live, or stay at the card's default numeral?
- Overview tiles are read-only rooms (no range switch); should the range toggled on the
  main card be reflected in the tile head (it is, via Mirror) and also switchable there?
- Medium: the card can reach 720 wide; is 96 px the right numeral ceiling, or cap density at 1.3?
- Replay on the phone replays all five stops; only the visible one is seen.

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
