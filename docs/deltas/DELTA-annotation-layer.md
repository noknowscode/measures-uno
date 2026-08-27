# DELTA BRIEF — Annotation Layer: Point Labels, References, Interpretation

Supersedes: caption behavior in `DELTA-behavior.md` (reading-in-caption model); adds a new in-chart text layer.

## Point labels Δ
- Selection/hover produces a two-line reading at the point: date (7.5 epx, 45 % Ink) over value (10 epx, solid Ink). No tooltip chrome. Clamped to plot edges; flips below the point near the top edge.
- All in-chart text carries a 3 epx Card-colored halo via paint-order:stroke (Uno: render text twice or use a stroked text layer) — labels stay legible across fills, lines, dashes, and grain. This halo is mandatory; sessions' step line proved it.

## Reference lines Δ (tiny labels, never full axes)
- Ridge: dashed AVG rule at true height, labeled "AVG 3.5 H".
- Sessions: dashed AVG rule, "AVG 3.4".
- Road: "GOAL 25" label at the goal ring (numeral simplified to value+unit as a result).
- One label style (svglab dim) across all references; values computed, never typed.

## Interpretation captions Δ
- The foot line never repeats a value the chart already shows; it interprets:
  ridge/sessions selected → "+X % vs N-day / daily average"; ridge default → today's delta; sessions default → "+N vs prior week"; road → "N km remaining"; night → "asleep 23:04 — awake 6:36".
- Hierarchy: metric → visualization → interpretation. Invitations still occupy the caption until first use.
