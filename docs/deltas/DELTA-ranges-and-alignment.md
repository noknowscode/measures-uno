# DELTA BRIEF — Live Ranges, Plot Standardization, Semantic Atmospheres

Supersedes: head-label and plot-geometry items across `DELTA-design.md` / `DELTA-behavior.md`.

## Range switchers Δ
- Ridge and Sessions period labels are functional, chromeless switchers: "7 DAYS ⌄" — mono label + caret, faint ink tint on hover only. Tap toggles 7↔14 with real data: totals, averages, peak, axis dates recompute; entrance replays.
- Night and Road keep plain labels — no caret where nothing can change. An affordance that does nothing is a lie.
- Uno: a ToggleButton retemplated to the label style; caret is a glyph, not a ComboBox.

## Plot standardization Δ
- Every chart's data occupies exactly x 10–290 in the 300:120 box (night's time axis remapped; road endpoints moved; sessions inset to match). All axis rows share the 3.3 % inset.
- Card MaxWidth 420, grid centered (composition constraint; stroke-scaling rationale is prototype-only).

## Semantic atmospheres Δ
- Temperature tokens get semantic names: AtmoEffort (butter), AtmoFrequency (sage), AtmoRest (lavender) + AtmoDepth (mist), AtmoDistance (blush).
- Written law beside them: hue distinguishes the instrument, never the state; no reading may require the hue. Release gate: every state distinguishable in two non-hue channels.

## Guard (from a real incident)
- Gradient stops with no stop-color render black. Wash brushes must be defined centrally and asserted present at startup; never inline per chart.
