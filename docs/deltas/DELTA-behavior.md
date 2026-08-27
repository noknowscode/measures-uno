# DELTA BRIEF — Behavior

Supersedes: interaction-state and a11y items in `MEASURES-BRIEFS.md` → Interaction Brief; incorporates maturation + retheme behavior changes.

## State signaling Δ (consequence of Soft Materialism)
- "Live/selected" is no longer a hue change (indigo) — it is **contrast + size + position**: charcoal fill, mark growth, plumb line, wake. Verify every state remains distinguishable without color: selected vs today vs rest must differ in at least two non-hue channels. This is now a release gate, not a nicety.
- Live captions signal by weight of color (quiet taupe → full charcoal) instead of hue.

## Affordance Δ
- Night chart advertises its gesture: `ew-resize` cursor on pointer hover (SizeWestEast in XAML), alongside the caption invitation.
- Hover = preview everywhere, in body and caption: marks grow, beads cascade, caption reads; tap = commit. (Rain previously previewed only via caption.)

## Semantics Δ
- Night is a real slider to assistive tech: value min/max/now plus value text ("2:10, deep sleep") updated per scrub step; Escape releases.
- Browser/system chrome joins the field: theme-color `#F5F0EC` light / `#1A1918` dark (in Uno: title bar / status bar theming per platform).

## Guarantees Δ
- Captions can never reflow the card: single line, ellipsis worst-case, fixed line box. Any new reading must be authored to fit ~34 chars at 280 epx.
- Road-ahead reading persists until the next interaction (no timers racing the reader).

## Known debt (carried, explicit)
- Rain day tap targets ≈15 epx wide at minimum card width — below the 24 epx comfort bar. Held deliberately: widening breaks the column rhythm. Revisit only with device evidence.
- Bead ceiling (~8/day) from `DELTA-sessions-beads.md` still unresolved before live data.
