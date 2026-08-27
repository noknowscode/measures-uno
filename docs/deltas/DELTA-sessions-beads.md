> SUPERSEDED by deltas/DELTA-sessions-stepline.md — kept for history only.

# DELTA BRIEF — Sessions (RainChart): stems → session beads

Supersedes: the Rain paragraphs in `MEASURES-BRIEFS.md` (Design "Rain — droplet" and the rain entries under Input behavior / Loading states). Everything not named here is unchanged.
Prototype reference: `prototype/ethereal-measures.html` (current state = beads).

Rationale: a session is a discrete, countable unit; a continuous stem height encoded a ratio where the data is a count. One bead per session makes the value readable without the axis — the count *is* the chart.

---

## Architecture Δ

- `RainChart` DP unchanged (`SessionLog` of per-day counts). Template parts change:
  - REMOVED: stem rectangle, gradient-pour overlay, droplet/splash elements, tip-bead (`tipdot`) element and its reform logic, √height fall-duration math.
  - ADDED: per-day column of bead `Ellipse`s (4.5 epx) built in code on data change (same code-built pattern as the other charts); zero days get one hairline `Ellipse` rest ring (5 epx) at the baseline.
- Entrance orchestration: one reusable fall storyboard per bead (`TranslateY -64 → 0`, 0.45 s, gravity ease-in), begin-time = dayIndex × 50 ms + stackIndex × 70 ms, bottom bead first.
- Tap feedback: `hop` storyboard on the top bead (Y 0 → −7 → 0, 0.30 s) plus a `plip` ring `Ellipse` (scale 0.6 → 2.4, fade, 0.4 s, 0.26 s delay), both cancel-safe on rapid taps.
- Legibility guard: assert-in-debug when any day exceeds the bead ceiling (see Unresolved).

## Design Δ

- Beads are **uniform**: same size, same color, no opacity or size grading. Sessions are identical units; a second encoding would undermine the count-the-drops premise.
- Ink at 75 % for history; `Ai` for the selected day's beads and for today when nothing is selected. Today's **top bead** breathes (5 s) — the living-edge mark now lives in the data instead of sitting on it.
- Geometry: bead 4.5 epx, stack gap 3 epx, column height ≤ 6 × 7.5 epx inside the unchanged 92 epx plot. Rest ring: 5 epx, hairline stroke, transparent fill.
- Card anatomy, tokens, numeral, axis whispers: unchanged.

## Interaction Δ

- Verb renamed: *droplet* → **plip**. Tap a day → its top bead hops 7 epx and lands with an expanding ring — a drop disturbing the pool — while the caption takes the reading. Fires on Enter/Space too (Decision: keyboard gets the same physical confirmation; Tradeoff: none).
- Hover/focus: the gradient pour is replaced by a **bead cascade** — beads turn `Ai` top-to-bottom at 45 ms per bead. Same downward-highlight read, now in discrete form.
- Loading state: the entrance now *shows the metric assembling* — 48 beads rain in and pool. This is the chart's strongest representational moment; do not shorten it below the stagger values above.
- Invitation copy: "tap a day to read it" → summary "3.4 a day — a steady fortnight" after first use.
- A11y: the day button remains the single focus stop ("wed 13, 5 sessions"); beads are not individually focusable. Reduced motion: beads render placed, no fall, no hop, no ring, no breathe.

## Runtime verification Δ

1. Counts subitizable at 280 epx card width in both schemes (light ink-75 % vs dark).
2. Entrance order: bottom-first within a day, left-to-right across days.
3. Today's breathe moves correctly between top beads on select/deselect.
4. Rapid taps: no orphaned plip rings, hop restarts cleanly.

## Unresolved Questions

- Bead ceiling: real data above ~8 sessions/day breaks single-column legibility. Overflow strategy to decide before wiring live data — double-column stack per day, 5-bead "tally" grouping, or numeral-only fallback above the ceiling.
- Should selecting a day also cascade its beads to `Ai` sequentially (echoing hover) rather than switching at once?
