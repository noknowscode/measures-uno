# DELTA BRIEF — Motion

Supersedes: nothing wholesale — the physics set in `MEASURES-BRIEFS.md` and `DELTA-sessions-beads.md` stands. This delta names the system and adds the post-brief values.

## Vocabulary Δ
- Exactly **two easings**, named resources, referenced everywhere: `EaseHands` 0.22,1,0.36,1 (drawn things, releases) and `EaseGravity` 0.4,0,1,1 (falling things, presses). Literal easing values in a storyboard are a review failure. Sole grandfathered exception: the bead `hop` (0.35,0,0.65,1 — up-and-back needs symmetry).

## Added values Δ (post-brief)
- Hover growth: ridge marks r 2.5→3.75, road marks 3→4, 0.3 s, EaseHands.
- Bead cascade (hover/focus): 45 ms per bead, top-down.
- Bead fall: 0.45 s EaseGravity, −64 epx entry; stagger day×50 ms + stack×70 ms, bottom bead first.
- Hop 0.30 s + plip ring 0.4 s at 0.26 s delay.
- Runner wake: reveals with the runner, holds, fades 1.2 s after arrival (0.4 s on cancel).
- Goal echo: one ring, 1.4 s, begins ~2.2 s (after the road finishes drawing).
- Caption swap: 140 ms dip with stale-write token; live scrub writes directly (no fade while dragging).

## Stillness Δ
- Grain never animates. Edge light never animates. Temperatures are rooms, not effects — if a wash moves, it has become a "blob" and violates the design law.
- Idle motion budget stays: breathing marks (5 s) are the only permanent motion on the page.

## Reduced motion
- Everything above gates on `UISettings.AnimationsEnabled`; draw-ins snap to final frame; hop/plip/echo/cascade suppressed; grain unaffected (it doesn't move).
