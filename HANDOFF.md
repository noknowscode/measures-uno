# HANDOFF — Measures Uno port
Updated: 2026-08-25

## Where we are

Scaffolded a .NET 10 Uno single-project app (`net10.0-android;net10.0-desktop;net10.0`) from
`MEASURES-BRIEFS.md` and the HTML prototype, and implemented all four instruments plus the
`DELTA-sessions-beads.md` revision. The page is one `MeasuresPage` hosting four `MeasureCard`s,
each holding a templated chart control (`RidgeChart`, `RainChart`, `NightChart`, `RoadChart`).
Tokens live in a plain `Tokens.xaml` re-coloured in code by `Palette` on `ActualThemeChanged`.
No MVUX/MVVM, no packages beyond the Uno.Sdk defaults, per the brief.

Three platform gaps were found and worked around, each with the fix commented at the site:

- **`StrokeDashArray` does not render for `Path`** on Uno Skia desktop (proved with a static
  `4,4` probe that drew solid). So draw-ins are a clip wipe driven by `Ticker`, the road ahead
  is built as real dash geometry from arc length, and the 72.8 % progress is a truncated road.
- **`TextBlock.CharacterSpacing` is a no-op** — `SpacedText` lays labels out one glyph per
  `TextBlock` in a `StackPanel` with `Spacing`.
- **A focus ring at a negative margin swallowed every button press** — decoration is now
  `IsHitTestVisible="False"` throughout. This was why no chart button fired.

## Last verified state

- **Build:** `net10.0-desktop` and `net10.0-android` both pass, 0 warnings. Tests 19/19 green.
- **Runtime (desktop, screenshots in `tools/shots/`):** all four cards render and match the
  prototype. Verified by driving real input: ridge pin (`sat — 5.2 h · +1.7 vs average`),
  sessions plip (`fri 14 — 2 sessions`), road replay + arrival swell (`wed — 6.1 km`), road
  ahead (`6.8 km — about 39 min at your pace`). Night scrub verified from app logs — 162
  scrub updates with correct readings (`4:44 — deep sleep`) and the dot tracking the curve;
  a still of it mid-scrub was never captured. The Replay button re-draws every instrument
  (not the card rise); beads were photographed raining in from above the card and stacking.
- **Not verified:** dark scheme (paused by request), reduced motion, anything on an Android
  device or emulator.
- **Git:** `main`, last commit `3f8a732 fix: beads fall from the sky, and animations stop being collected mid-flight`.
  Clean.

## Next actions (in order)

1. Verify on an Android device: **the night scrub vs vertical scroll** is the highest-risk item.
   `NightChart` deliberately does not set `ManipulationMode` (that handed the pointer to the
   gesture processor and killed the capture on desktop); it captures only after horizontal
   intent, |Δx| > |Δy| over the first 8 px. Confirm a vertical swipe still scrolls the page.
2. Dark scheme pass on all four cards (the palette exists and is wired; it has never been seen).
3. Reduced-motion pass — every animation routes through `Anim`/`Ticker`/`BreathClock`, which
   gate on `UISettings.AnimationsEnabled`, but no frame of it has been observed.
4. Decide the bead ceiling overflow strategy (see Open questions) before wiring live data.

## Open questions

- Bead ceiling: days above ~8 sessions break single-column legibility. `BeadColumn` logs a
  Debug warning past 6 (written, not asserted — a modal assert would freeze the render thread).
  Double-column, 5-bead tally grouping, or numeral fallback?
- Should selecting a day cascade its beads to `Ai` sequentially, echoing hover, rather than
  switching at once?
- Ship as an Uno sample/article pair or as a personal app? Decides how much API surface the
  chart controls should expose.
- `MEASURES-BRIEFS.md` still describes the old stem-based Sessions card; the delta supersedes
  it but the brief was left unedited.

## Relaunch

```powershell
cd C:\Users\Platform006\UnoProjects\measures-uno
dotnet build .\Measures\Measures.csproj -f net10.0-desktop
.\tools\Verify.ps1 -Name check        # stop, build, launch, screenshot
.\tools\Drive.ps1  -ProcessId <pid> -Action click -X <x> -Y <y>
```

**MCP note:** the `uno-app` MCP server was never available this session — `.mcp.json` is read
at session start and was created after this one began. Start the next session rooted in this
folder and `uno_app_start` / visual-tree snapshots will be available, which is far better than
the screenshot harness in `tools/`. Diagnostic: if the only Uno tools are
`uno_platform_docs_search` / `_fetch`, the app MCP did not load and no `/mcp` reconnect fixes it.
