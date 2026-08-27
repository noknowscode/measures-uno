# HANDOFF — Measures Uno port
Updated: 2026-08-27

## Where we are

The ten delta briefs under `docs/deltas/` are implemented on `master`, on top of the
Soft Materialism retheme (commit `f4d5756`). The page is still one `MeasuresPage` hosting
four `MeasureCard`s; no MVUX/MVVM, no packages beyond the Uno.Sdk defaults.

What the delta pass changed:

- **Sessions are a step line** (`StepChart`), per `DELTA-sessions-stepline`. The bead system
  (`BeadColumn`, `RainChart`, hop/plip, `Anim.BeadHop`) is deleted. Hit zones are full-height
  `DaySlot`s; peak is a hollow mark, today rides the line end and breathes, pin drops a plumb.
- **Shared 300:120 plot box** (`ChartBase.DesignWidth/Height`), data at x 10–290 in every
  chart, height follows width (`PlotHeight` is computed in `Layout`). Charts author in design
  units and multiply by `Px`. Axis whispers default to the 3.3 % inset.
- **Annotation layer**: `PointLabel` (date over value, clamped, flips below near the top) and
  `HaloText` (3 epx card-coloured halo = 8 offset copies; XAML has no text stroke). Reference
  rules are real dash geometry (`Curves.DashedRule`), labelled `AVG 3.5 H` / `AVG 3.4` / `GOAL 25`.
- **Interpretation captions**: ridge/sessions selected → "+X % vs …average"; ridge default →
  today's delta; sessions default → "+N vs prior week/fortnight"; road → "N km remaining";
  night → "asleep … — awake …". Invitations still occupy the caption until first use.
- **Range switchers** (`RangeSwitch`, a chromeless ButtonBase with a caret) on ridge and
  sessions: 7↔14 days with real data (`SampleData.RidgeHistory` 14 days, `SessionsHistory`
  28 days), numerals/axis/AVG/peak recompute, entrance replays.
- **Metric hierarchy**: numeral 64 / unit 26 at 75 % Quiet (`UnitBrush`), metric column
  top-anchored with a fixed 40 epx gap, hairline rule above every foot, axis whispers at 45 %
  ink, references at 15 % (`ReferenceBrush`), road numeral is "18.2 km" (goal lives in chart).
- **Tokens**: wash brushes carry semantic names (`AtmoEffort/Frequency/Rest/Depth/Distance`),
  `Temperature` enum is semantic (Effort/Frequency/Rest/Distance), and `Palette.Verify` throws
  at the first `Apply` if any expected key or gradient stop count is missing from `Tokens.xaml`.

Since then (2026-08-27, on the Pixel 8 and desktop):

- **Android** builds and runs on the Pixel 8 (`com.measures.app`). Status bar is transparent
  with scheme-coloured icons; the page pads its own inset from `VisibleBounds`.
- **Carousel below 620**: the four cards are re-seated from the 2x2 bench into a `FlipView`
  with pips (`MeasuresPage.Seat`). In the carousel `NightChart.ScrubOnPress` claims the finger
  on press, so the swipe lives on the rest of the card.
- **Dark/Light button** in the header flips the window's root Frame (what `Palette` listens to).
- **Replay** now really replays: `Anim.Run` stops the previous HoldEnd board on the same
  target/property, or the held value masks the local `Opacity = 0` through the delay.
- **Audit pass**: dead members and two unused brushes removed, scaffold `GlobalUsings` trimmed,
  no hex outside `Tokens.xaml`, no narration comments, 0 warnings on both heads.

Platform gaps carried from earlier sessions still apply (StrokeDashArray does not render,
CharacterSpacing is a no-op, decoration must be `IsHitTestVisible="False"`).

## Last verified state

- **Build:** `net10.0-desktop` passes, 0 warnings. Tests 24/24 green. Android not rebuilt this session.
- **Runtime (desktop, App MCP, screenshots `tools/shots/d-*.png`):** light and dark full page;
  ridge pin (`SAT 22 / 5.2 h`, "+48 % vs the 7-day average"); sessions pin (`THU 20 / 1 session`,
  "−71 % vs daily average") and invited default ("+2 vs prior week"); ridge 7→14 (46.5 h,
  `mon 10`/`sun 23`, AVG 3.3); sessions 14→7 (25 sessions, AVG 3.6); night press → "asleep
  23:04 — awake 6:36"; road replay (`WED / 6.1 km`, "14.1 km remaining after this run").
- **Not verified:** hover previews and keyboard walking (the MCP cannot hover), reduced
  motion, Android, the night scrub mid-drag (only press/release).

## Next actions

1. Uno.Sdk 6.6.42 -> 6.7.18 is the latest stable (checked 2026-08-27). Bump in `global.json`,
   rebuild both heads, re-verify the Runtime Gotchas that were pinned on 6.6.42.
2. Device pass on the carousel: does a real swipe ever skip a card (a 200 ms synthetic flick
   jumped two)? Does the night scrub feel right now that it starts on press?
3. Hover pass with a real pointer on desktop: ridge hollow-mark materialisation, sessions
   `PART_Hover`, point-label fades.
4. Decide SMAX overflow for sessions (fixed 6; live data above clips) and the ridge Ceiling.
5. The time scrubber (`DELTA-time-scrubber`) is explicitly experimental and not built.
   `docs/glyph-field-background.md` is an unrelated spec.

## Open questions

- Ridge AVG label sits at the left end of the rule and can touch the line where the first day
  is near the average; a small dodge (above vs below the rule) would settle it.
- In-wash grain (grain clipped to each gradient's alpha) is not built; only the page grain
  is. Needs an OpacityMask that works on Skia, or a per-wash tile.
- Sample data for the ridge is anchored to the most recent Sunday so the week reads mon..sun
  and the peak stays Saturday; sessions anchor to today. Two anchors, by design, until live data.

## Relaunch

```powershell
cd C:\Users\Platform006\UnoProjects\measures-uno
dotnet build .\Measures\Measures.csproj -f net10.0-desktop
.\tools\Verify.ps1 -Name check                      # stop, build, launch, PrintWindow capture
$env:MEASURES_THEME='dark'; .\tools\Verify.ps1 -Name dark -NoBuild
```

The `uno-app` MCP loads when the session is rooted in this folder: `uno_app_start` on
`Measures\Measures.csproj` / `net10.0-desktop`, then `uno_app_pointer_click` with client
coordinates (peer invokes fail on the chart buttons because `justMyCode` snapshots return
their template Grids).
