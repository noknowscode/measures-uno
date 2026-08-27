# DELTA BRIEF — Architecture

Supersedes: token/theming and constraint items in `MEASURES-BRIEFS.md` → Architecture Brief.
Unchanged: project structure, four templated controls + `MeasureCard`, no-framework state model, `CubicSampler`, pointer/traversal mechanics.

## Token architecture Δ
- The single-accent model (`Ai` + neutrals) is replaced by a **two-axis system**:
  - Neutral axis: Canvas `#F5F0EC`, Canvas2 `#E8E5E4`, Card `#FBF8F5`, Ink `#1F1F1E`, Quiet `#A19A96`, Hairline (10 % ink).
  - Light axis: five wash colors — Butter `#F1E8CF`, Sage `#DEE4DA`, MistBlue `#DCE4EA`, Lavender `#E4E0E8`, Blush `#F1DEDA`.
- Wash intensity is tokenised separately from hue: `WashHi / WashMid / WashLo / EdgeLight` opacity values. **Dark theme dims the opacities, never recolors the hues** (0.85→0.15, edge 0.55→0.07). One switch, five lights.
- `Palette` static class drops `Ai`; adds the wash set and opacity tokens. Easing pair is a named resource too: `EaseHands` (0.22,1,0.36,1) / `EaseGravity` (0.4,0,1,1) as `KeySpline`/`CubicBezierEasingFunction` — the only two easings any storyboard may reference.

## Control surface Δ
- `MeasureCard` gains a `Temperature` enum property (Butter/Sage/Lavender/Blush) driving the top edge-light layer; charts request their temperature, the card renders it.
- `NightChart` implements a real slider automation peer: `RangeValuePattern` (min/max/now) + live value text ("2:10, deep sleep") — matches the prototype's `role=slider` + `aria-valuetext`.
- Grain: in the prototype, a shared `feTurbulence` filter + a page noise tile. In Uno, grain is a **precomputed noise bitmap asset** (tileable PNG, ~200 px) composited via an `ImageBrush` overlay at soft-light-equivalent opacity — do not port per-element SVG filters; assert once at startup that the asset loaded.

## Constraint Δ
- Card `MaxWidth = 420` epx, grid centered. Note: the prototype needs this to stop SVG stroke scaling; XAML strokes are absolute so the risk doesn't transfer — the cap is kept anyway as a **composition** constraint, and the reason is documented so nobody "fixes" it.

## Unresolved
- Noise asset: one tile for both themes (opacity-tuned) or per-theme tiles? Test banding on OLED dark first.
