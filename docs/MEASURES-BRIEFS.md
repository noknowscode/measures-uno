# MEASURES — Uno Platform App Spec

Session: Measures Uno port
Prototype: `prototype/ethereal-measures.html` (open in any browser; light + dark via OS scheme)

Skill/Rule: `xaml-art-direction`
Applies because: a showcase XAML surface where visual identity is the point.
Guidance being followed: tokens-in-plain-dictionary, static per-weight TTFs (variable fonts render default instance only on Skia), signature-element discipline, Path geometry over font glyphs, boldness spent in one place.

No matching motion skill loaded in this session (`xaml-design-polish` referenced by the art-direction skill is not present); motion values below carry over from the validated prototype and gate on `UISettings.AnimationsEnabled`.

---

## Architecture Brief

**App/module structure**
- Single-project Uno.Sdk app (`net10.0-desktop`, `net10.0-browserwasm`, `net10.0-android`, `net10.0-ios`), Skia renderer on all targets.
- One page (`MeasuresPage`) hosting four templated controls, one per chart:
  `RidgeChart`, `RainChart`, `NightChart`, `RoadChart` — each a `Control` with a default template in `Themes/Generic.xaml`. This matches the STRATA/retemplating direction: each chart is a reusable control, not page markup.
- Shared primitives: `MeasureCard` (chrome: head labels, numeral slot, caption slot, foot label), `CaptionPresenter` (fade-swap + invitation logic), `CubicSampler` (static Bézier evaluation).

**State model**
- Data in: plain records (`RidgeData(IReadOnlyList<DayValue>)`, `RunLog(IReadOnlyList<Run>, double GoalKm, double PaceMinPerKm)`, `SleepNight(TimeOnly Down, TimeOnly Up, IReadOnlyList<DepthSample>)`, `SessionLog(...)`) exposed as dependency properties on each control.
- Interaction state (selection index, scrub position, invitation-consumed flag, runner progress) is private to each control — presentation state, not app state.
- No MVUX, no MVVM framework for v1: a `SampleData` static class feeds the page via `x:Bind`. Decision: smallest implementation that satisfies the requirement. Tradeoff: wiring a live data source later means adding a VM layer then, not now.

**Navigation model**
- None. One page. `Frame` exists only because the template ships it.

**Services/dependencies**
- Zero external packages beyond Uno.Sdk defaults. Charts are XAML `Path`/`Ellipse`/`Line` + code-computed geometry.
- Escalation hatch (documented, not taken): if chart count or point density grows, `SKCanvasElement` (Uno.WinUI.Graphics2DSK, auto-referenced with the Skia renderer, hardware-accelerated, `IsSupportedOnCurrentPlatform()` guard) replaces per-chart Path composition. Ref: platform.uno/docs/articles/controls/SKCanvasElement.html
- Fonts as assets: static TTF instances only — Newsreader Light 300, Newsreader Light Italic 300, Martian Mono Regular 400. Newsreader ships variable on Google Fonts; export static instances or Skia renders the default axis only.

**Data flow**
- Page → control: records via DP (`ChartData="{x:Bind Sample.Ridge}"`).
- Control internal: on DP change → recompute geometry (`PathFigure` from `CubicSampler`) → invalidate template parts.
- Caption text flows one way: interaction → `CaptionPresenter.SetReading(text, live)` / `SetDefault()`; the presenter owns the 140 ms fade-swap and the invitation-once behavior.

**Platform constraints**
- Scrub vs scroll (the `pan-y` problem): `NightChart` sets `ManipulationMode="System,TranslateX"` and only captures the pointer after horizontal intent (|Δx| > |Δy| over the first 8 px); vertical gestures fall through to the ScrollViewer. This is the highest-risk item — verify on Android first.
- Dash-march and partial-reveal use `StrokeDashOffset`/`StrokeDashArray` animations; actual path length computed once from the sampled polyline (no `pathLength` normalization exists in XAML).
- Runner traversal: `CompositionTarget.Rendering`-driven interpolation over precomputed samples (the WinUI analog of the prototype's rAF loop), cancellable by token exactly as in the prototype.

**Testing/validation approach**
- Unit: `CubicSampler` (point-at-x, point-at-length), caption invitation state machine, run-bounds math (18.2/25 = 72.8 %).
- Runtime: each chart verified against the prototype side-by-side per platform — draw-in, gravity easings, dark scheme — before the next chart starts. Build/commit after each chart lands.

---

## Design Brief

**Visual direction** — "Ethereal Super Normal": four instruments on mist. Law of the page: history in ink, the living edge in indigo (`ai`); the night curve is the sanctioned exception. Gravity eases in; hands ease out.

**Tokens** (plain root-level dictionary `Tokens.xaml`, merged in App.xaml; mirrored in a static `Palette` class for brushes inside DataTemplates):
- Light: Mist `#F6F7F5`, Mist2 `#F1F3F1`, Card `#FBFCFB`, Ink `#2C2E2C`, Quiet `#9AA09B`, Ai `#5A6C8F`, Hairline `#1A2C2E2C` (10 % ink).
- Dark: Mist `#151714`, Mist2 `#101210`, Card `#1B1E1B`, Ink `#E6E8E4`, Quiet `#878E87`, Ai `#8FA3CB`, Hairline 10 % of dark ink.
- Theme switching: standard-role brushes via ThemeDictionaries; custom token keys resolve from the plain dictionary and are re-pointed on `ActualThemeChanged` (custom keys inside ThemeDictionaries resolve unreliably — per skill).
- `Ai` appears only where something is alive: today-marks, running time, scrub readouts, the wake dot, gradients of sky/rest.

**Typography**
- Newsreader Light 300 — numerals (60 epx) and captions (15 epx italic); tabular figures via `Typography` where supported, else fixed-width numeral layout.
- Martian Mono 400 — labels 9.5 epx, letter-spacing 0.18 em (approximate with `CharacterSpacing="180"`), uppercase; axis whispers 8.5 epx.

**Layout structure** — archetype: *bench of instruments* (four identical device faces on a table, from the hardware-panel vernacular). 2-up grid ≥ 620 epx, single column below, 16 epx gap, 24 epx corner radius, hairline border, soft double shadow. Card anatomy is fixed: head labels (28 padding), numeral slot (60 epx line), chart slot (bottom-pinned), foot = caption + right label. Numeral tops and chart bottoms are the two shared horizontals — enforced by `MeasureCard`, not per-chart.

**Spacing** — 28 card padding, 16 head-to-body, 22 numeral-to-chart, 8 chart-to-axis; Fibonacci-adjacent, keep as-is from prototype.

**Component hierarchy** — `MeasuresPage` → `ItemsRepeater`/Grid of `MeasureCard` → chart control in the card's content slot. Charts own only their plot; all chrome is `MeasureCard`.

**Theme usage** — follows OS via `RequestedTheme` default; no in-app toggle in v1.

**Responsive/adaptive** — two states via `AdaptiveTrigger` at 620 epx (grid columns 2→1). Charts scale by width; `Viewbox` is banned (it would scale strokes) — geometry recomputes on `SizeChanged` instead.

**Signature** — the four metaphors as a system (valley, road, ridge, rain), with the road's replaying runner as the single boldest element. Everything else stays hairline.

---

## Interaction Brief

**User flows** — glance (numerals answer in one second) → touch (each chart's verb) → read (caption line takes the reading; no tooltips, popovers, or layout shift, ever).

**Input behavior (per chart)**
- Ridge — *pin*: tap/Enter a day → plumb hairline drops (gravity ease-in, 300 ms), caption "thu — 4.6 h · +1.1 vs average". Hover previews the same reading; leave restores. Arrow keys walk days.
- Rain — *droplet*: tap a stem → drop detaches, falls its true height (duration ∝ √h, 0.22–0.52 s, ease-in), lands with a flat splash. Today's stem carries a breathing bead; tapping today detaches it and a new bead forms 0.7 s after the splash. Hover/focus pours the top-down indigo gradient (`ScaleY` reveal, origin top).
- Night — *scrub*: press-drag reads clock + depth live ("2:10 — deep sleep"); dot rides the curve, or the horizon when awake. Arrow keys scrub 6 units; Escape releases. Horizontal-intent capture only (see Architecture).
- Road — *replay*: tap a marker → runner dot traverses that segment (easeInOutQuad, 0.4–1.0 s by distance), leaves a bright wake that fades 1.2 s after arrival; the marker swells only when the runner merges into it. Tap the dashed road/goal ring → dashes march once, caption converts distance to minutes at pace; reverts on next interaction, not a timer.

**Empty states** — v1 ships sample data; controls render an em-dash numeral and a still chart if a DP is null (no crash, no placeholder art).

**Loading states** — the entrance *is* the loading state: cards rise staggered (120 ms), ridge/night draw (1.8 s ease-out), rain falls (per-stem 50 ms stagger, ease-in), road draws then goal ring echoes once at ~2.2 s.

**Error states** — none reachable in v1 (static data). Invalid data (goal ≤ 0, empty series) renders the empty state.

**Animations/transitions** — carry the prototype's constants verbatim; the page law (gravity in / hands out) is the review test for every new motion. Caption swaps: 140 ms opacity dip with stale-write token.

**Feedback states** — hover = preview (caption only), press = commit, selection = indigo + size, arrival/landing = the confirmation moment (swell, splash, wake). Focus-visible: 2 epx `Ai` ring, 3 epx offset.

**Accessibility** — every interactive part is a real focus stop with `AutomationProperties.Name` ("thursday, 4.6 hours"); readings announced via `LiveSetting="Polite"` on the caption; all animation gates on `UISettings.AnimationsEnabled` (draw-ins snap to final frame, echo/breathing suppressed); statuses never color-alone (size + position carry selection too).

**Runtime verification steps** — per platform: (1) dark/light both schemes, (2) night scrub does not eat vertical scroll on touch, (3) reduced-motion snapshot shows final frames, (4) caption never wraps at 280 epx card width, (5) runner cancellation via rapid taps leaves no orphaned wake.

---

## Implementation Plan

1. Solution scaffold, fonts as static TTFs, `Tokens.xaml` + `Palette`, `MeasureCard` — commit `feat: card chrome and tokens`.
2. `CubicSampler` + unit tests — commit `feat: geometry sampling`.
3. `RidgeChart` (draw-in, pin, hover, keyboard) — verify vs prototype — commit.
4. `RainChart` (rainfall entrance, droplet physics, bead, pour) — commit.
5. `NightChart` (valley, sky gradient, scrub with intent capture) — commit.
6. `RoadChart` (ghost/progress/trace, runner, road-ahead, echo) — commit.
7. Page assembly, adaptive states, dark pass on all targets — commit.
8. Reduced-motion + accessibility pass — commit `feat: a11y and motion gates`.

## Unresolved Questions

- Ship as an Uno sample/article pair (STRATA-style) or personal app first? Affects how much API surface the chart controls expose.
- Should chart data DPs be granular (`Values`, `Labels`) or single record objects? Records read cleaner; granular binds easier from XAML previews.
- Uno Hot Design / DevServer workflow assumed for iteration — confirm current tooling on your machine before step 1.
- Android is proposed as the first verification target because of the scrub-capture risk — agree?
