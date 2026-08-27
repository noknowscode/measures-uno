# DELTA BRIEF — Time Scrubber (EXPERIMENT — separate prototype)

Status: experimental. Lives in `prototype/ethereal-scrubber.html`; NOT part of the main dashboard spec. Proposed as a mode, not a resting-state feature.

## Concept
- One timeline under the header scrubs every chart's window through history simultaneously. Drag is fully continuous (fluid, never stepped); release settles on the nearest WEEK detent with a 380 ms hands-ease tween. Detents, not steps: destinations are semantic ("wk of aug 4"), transit is fluid.

## Interpolation strategies (the technical payload)
- Sliding windows (ridge, sessions): fractional day offset, points enter/leave through a plot clip; per-frame path regen.
- Record crossfade (night): each night sampled to a matched point count, lerped; depth lobes SUMMED, never max()'d (max creases → visible shake under interpolation).
- Discrete snap (road): progress line and warmth lerp continuously between weekly totals; run markers snap to the nearest week at f=0.5 — discrete sets never morph (a 4-run week cannot become a 3-run week mid-flight).
- Timeline: day ticks minor, week detents major; handle label reads the day in transit, the week when settled.

## Why a mode
- Live scrubbing costs the dashboard its stillness and conflicts with per-chart gestures (pin/scrub/replay assume a stable window) and entrance choreography. As a toggled mode: scrubber rises, chart gestures sleep, release settles, dashboard returns to stillness at the chosen week.

## Uno notes
- Per-frame path regeneration is a natural Skia showcase (SKCanvasElement or per-frame PathGeometry swap — benchmark both). Settle tween via CompositionTarget.Rendering with cancel-on-grab.

## Unresolved
- Nearest-detent vs velocity-carry on release. / How the mode is entered (hold, toggle, chip). / Whether night should settle to the week's best night or its last night.
