# DELTA BRIEF — Metric Hierarchy, Card Anatomy, Contrast, Dot Doctrine

Supersedes: numeral/typography and per-chart layout items in `DELTA-design.md`; the mark treatments in the original Design Brief.

## Metric dominance Δ
- Value 64 epx; unit suffix 26 epx (≈41 %) at 75 % of Quiet, hair of tracking after the value. Value + unit only — "24.6 h", "18.2 km"; the period lives in the head, the goal lives in the chart (GOAL label).

## Rigid anatomy Δ
- Fixed structure per card: LABEL·PERIOD / VALUE+UNIT / CHART / rule / CONTEXT·DETAIL.
- One shared plot box 300:120 for all four charts (rain/sessions included — no fixed-px plots). Metric column top-anchored with a fixed 40 epx metric→chart gap; slack never pools between metric and chart.
- Shared horizontals: numeral tops, chart boxes, axis rows (all inset 3.3 % to the 10–290 data span), and a hairline divider above every foot (the footer baseline).

## Contrast ramp Δ (no color, hierarchy by opacity)
- Primary series: full Ink. Historical/secondary: ~40 %. Reference (dashed road-ahead, AVG/PACE rules): 13–16 %. Axis whispers: 45 % Ink (was Quiet — they were decorative). Selected/current: solid Ink.

## Dot doctrine Δ
- No marker for ordinary points (hover materializes a hollow one — targets stay discoverable).
- Small hollow marker for meaningful samples (ridge peak; past runs on the road).
- Solid marker for selected and for current (breathing).
- Dashed continuation for goal/forecast (the road ghost).

## Unresolved
- "Meaningful sample" is hard-coded (peak index). Uno build computes it (max, optionally min) per window.
