# Glyph Field Background — Uno Platform Implementation Spec

Recreate the "magnetic glyph grid" background as a reusable Uno Platform control:
a full-bleed grid of small code symbols (`<` `/` `>` `{` `}` `;` `=`) that sits at
hairline contrast behind app content. Glyphs near the pointer pull off the grid
into a slow orbit, realign their characters to the field's direction, and spring
back home when the pointer leaves. Pointer press fires a radial shockwave.

Reference implementation: `glyph-grid-background.html` (canvas/JS). This spec is
self-contained — everything needed is below.

---

## Architecture Brief

- **Control**: `GlyphFieldBackground : Control`, hosting a single `SKXamlCanvas`.
  No template parts, no visual states. Consumers place it as the first child of a
  root `Grid`, behind content.
- **Mode**: `FieldMode` dependency property, `Magnet` (default) or `Part`.
  Both modes run the identical physics pipeline; a mode is only a constant
  profile (see tuning table). Switching at runtime swaps the profile and lets
  the springs re-settle — no cell rebuild, no second code path.
- **Rendering**: SkiaSharp immediate-mode. XAML elements per glyph is a non-starter
  (~3,000 cells); one Skia canvas draws everything in a single pass.
  - Package: `SkiaSharp.Views.Uno.WinUI` (WinAppSDK head uses
    `SkiaSharp.Views.Windows`; same `SKXamlCanvas` API).
- **Render loop**: subscribe `CompositionTarget.Rendering` → `canvas.Invalidate()`.
  - **Idle sleep**: when every cell is at rest AND the pointer is outside the
    field, unsubscribe from `Rendering` and repaint once. Re-subscribe on
    `PointerMoved`/`PointerPressed`. A background must cost ~0 when nothing moves.
  - **Delta-time normalization**: forces below are tuned for 60 fps. Scale each
    tick by `dt / 16.667` (clamped to [0.5, 2.0]) so 120 Hz and 30 fps feel the same.
- **State model**: one flat `Cell[]` (struct array, preallocated at resize):
  `HomeX, HomeY, X, Y, VX, VY, Rot, Wake, BaseCharIndex, CharIndex, Phase, SwapAtMs`.
  No per-cell objects, no LINQ in the loop, zero allocations per frame.
- **Input**: `PointerMoved` / `PointerExited` / `PointerPressed` handled on the
  control. The canvas itself is `IsHitTestVisible="False"`; the control listens at
  its own level so overlaid content keeps normal hit-testing. If the control is
  fully covered by content panels, listen on the page root and forward positions.
- **Platform constraints**:
  - Font: ship `MartianMono-Regular.ttf` as `Content` asset; load
    `SKTypeface.FromStream` once. Fallback: `SKTypeface.FromFamilyName("Consolas")`.
  - DPI: multiply all layout by `XamlRoot.RasterizationScale`; Skia draws in
    physical pixels.
  - WASM: `SKXamlCanvas` works; verify frame pacing (see Unresolved).
- **Testing/validation**: run on Windows + WASM heads. Validate: 60 fps with a
  moving pointer over a 1920×1080 grid; 0% CPU render cost after 2 s idle;
  pointer over foreground content still bends the field.

Decision: SkiaSharp canvas over XAML composition.
Reason: 3k animated text elements with per-frame transforms is exactly the
workload retained-mode UI is worst at; Skia does it in one draw pass.
Tradeoff: no per-glyph accessibility tree — acceptable, it is decorative
(`AutomationProperties.AccessibilityView="Raw"`).

---

## Design Brief

- **Visual direction**: quiet, IDE-native texture — an engineered alternative to a
  dot grid. No glow, no blur, no gradients. The effect is carried entirely by
  displacement, rotation, color, and character choice.
- **Layout**: uniform grid, pitch **26 px** (logical), glyph font size **12 px**,
  centered in each cell. Symbol pool weighted toward the tag glyphs:
  `< / > < / > { } ; =` (pick uniformly from that 10-entry list).
- **Typography**: Martian Mono Regular (400). All glyphs same size/weight; the
  field never changes weight, only color/scale/rotation.
- **Color tokens** (theme-aware):

  | Token        | Dark        | Light       |
  |--------------|-------------|-------------|
  | Ground       | `#101013`   | `#F4F2EC`   |
  | Glyph ink    | `#40404A`   | `#BAB5A8`   |
  | Accent (wake)| `#FF6B57`   | `#E8543F`   |

  Base glyph alpha **0.9**, plus a per-cell shimmer of ±0.04
  (`sin(t/1600 + phase)`). Waked glyphs lerp ink → accent by wake, alpha up to
  1.0, scale up to **1.2×**.
- **Component hierarchy**: `GlyphFieldBackground` fills the root; app content sits
  above in the same `Grid`. Content panels need no special treatment.
- **Theme usage**: expose the three colors as dependency properties defaulting to
  theme-resource lookups so the control follows Light/Dark automatically.
- **Responsive behavior**: rebuild the cell array on `SizeChanged`. Grid pitch is
  constant; cell count derives from size. On touch-only devices with no pointer
  hover, the field stays idle (still a good static texture) and tap = shockwave.

---

## Interaction Brief

- **User flows**: purely ambient. No focus, no keyboard interaction, never
  intercepts input meant for content.
- **Field physics** (per tick, per cell; constants in the tuning table):
  1. Eased pointer: `mx += (targetX - mx) * 0.2` (same for y). Pointer velocity
     `(mvx, mvy)` = eased delta this tick. The easing gives the field a trailing,
     fluid feel — it must not track the raw pointer 1:1.
  2. Sample field at the cell's **home** position (stability — displaced glyphs
     must not chase their own displacement):
     `d = dist(home, pointer)`, `inField = d < FIELD_R`, `t = 1 - d / FIELD_R`.
  3. If in field, with radial unit `n = (home - pointer) / d`:
     - Ring pull: `v -= n * (d - ORBIT_R) * K_FIELD * t`
       (outside the ring pulls in, inside pushes out → glyphs gather into a ring).
       This one formula covers both modes: **Magnet** uses a small `ORBIT_R`, so
       glyphs collapse inward into a tight orbit; **Part** uses a large `ORBIT_R`
       near the field edge, so everything inside is pushed outward and the grid
       opens around the pointer, with a slight bow-wave gather at the rim.
     - Swirl: `v += perp(n) * K_SWIRL * t²` (slow orbit).
     - Fling: `v += pointerVelocity * KICK * t²`.
     - Realign: rotate toward the tangent, capped ±0.7 rad, eased at 0.15/tick.
       When `t > 0.35`, the character snaps to the field:
       right sector → `>`, left → `<`, above → `{`, below → `}`
       (sector = angle from pointer to cell, 90° quadrants centered on the axes).
       Reverts to `BaseChar` when wake decays below 0.1.
  4. Always: spring home `v += (home - pos) * K_HOME`, damping `v *= DAMP`,
     integrate `pos += v`.
  5. Wake easing is asymmetric: rise `0.25`, decay `0.05` — the coral trail
     lingers behind the pointer.
- **Shockwave** (pointer press / tap): every cell within `FIELD_R * 1.6` gets a
  radial impulse `(1 - d / (FIELD_R * 1.6)) * SHOCK` and its wake floored at the
  same falloff. Springs bring everything home.
- **Idle behavior**: shimmer only, plus rare character swaps — each cell reroll
  every 6–30 s (random). Swap silently (no animation); if the cell is waked,
  defer the visible swap until wake < 0.1.
- **Empty/loading/error states**: none — the control renders from first frame
  with fallback font if the asset isn't ready, and swaps typeface when loaded.
- **Accessibility / reduced motion**: read
  `UISettings.AnimationsEnabled` (and browser `prefers-reduced-motion` on WASM).
  When reduced: no physics, no shimmer, no shockwave — a static grid at base ink.
- **Runtime verification**: pointer circles at moderate speed → glyphs form a
  visible orbiting ring that trails the pointer; fast flick → glyphs scatter and
  spring back with overshoot; press → radial burst; leave window → field decays
  fully and CPU drops to idle within ~2 s.

### Tuning constants

Shared (mode-independent):

| Constant    | Value | Meaning                     |
|-------------|-------|-----------------------------|
| `SPACING`   | 26    | grid pitch (logical px)     |
| `FONT_SIZE` | 12    | glyph size                  |
| `K_HOME`    | 0.045 | spring back to grid         |
| `DAMP`      | 0.86  | velocity damping            |
| `SHOCK`     | 9.5   | press impulse               |

Mode profiles:

| Constant   | Magnet | Part  | Meaning                                  |
|------------|--------|-------|-------------------------------------------|
| `FIELD_R`  | 170    | 150   | outer field radius                        |
| `ORBIT_R`  | 42     | 118   | ring the field drives glyphs toward       |
| `K_FIELD`  | 0.055  | 0.05  | strength of the ring force                |
| `K_SWIRL`  | 0.55   | 0.0   | tangential drift (skip the term when 0)   |
| `KICK`     | 0.35   | 0.2   | pointer-velocity fling                    |
| Realign `t`| 0.35   | 0.55  | field strength above which chars realign  |

Part-mode reading: no swirl, so the motion is pure displacement — the grid
parts like water around the pointer and closes behind it (the trailing-pointer
easing does the closing). The higher realign threshold keeps character swaps to
the immediate rim; in Part mode the sector glyphs read as pointing *away* from
the pointer, which is correct for repulsion. Wake color/scale behave
identically in both modes.

---

## Reference algorithm (core of `OnPaintSurface`)

```csharp
// Cached once: SKPaint per unique glyph is unnecessary — cache SKTextBlob
// per unique char instead (7 blobs total). DrawText(string, ...) allocates;
// DrawText(SKTextBlob, ...) does not.
// _blobs[i] = SKTextBlob.Create(chars[i].ToString(), _font);
//
// FIELD_R, ORBIT_R, K_FIELD, K_SWIRL, KICK, and the realign threshold are
// read from the active FieldMode profile (see tuning table). When the
// profile's K_SWIRL is 0 (Part), skip the swirl term entirely.

void Paint(SKCanvas c, int w, int h, float dt)
{
    float k = Math.Clamp(dt / 16.667f, 0.5f, 2.0f);   // 60fps-normalized
    c.Clear(_ground);

    for (int i = 0; i < _cells.Length; i++)
    {
        ref var cell = ref _cells[i];

        float dhx = cell.HomeX - _mx, dhy = cell.HomeY - _my;
        float d = MathF.Sqrt(dhx * dhx + dhy * dhy);
        bool inField = !_reducedMotion && d < FIELD_R;

        if (!inField && cell.IsAtRest())            // cheap path: most cells
        {
            _paint.Color = _ink.WithAlpha(ShimmerAlpha(ref cell));
            c.DrawText(_blobs[cell.CharIndex], cell.HomeX, cell.HomeY, _paint);
            continue;
        }

        float targetWake = 0;
        if (inField)
        {
            float t = 1 - d / FIELD_R;
            targetWake = t;
            float nx = dhx / d, ny = dhy / d;

            cell.VX -= nx * (d - ORBIT_R) * K_FIELD * t * k;
            cell.VY -= ny * (d - ORBIT_R) * K_FIELD * t * k;
            cell.VX += -ny * K_SWIRL * t * t * k;
            cell.VY +=  nx * K_SWIRL * t * t * k;
            cell.VX += _mvx * KICK * t * t;
            cell.VY += _mvy * KICK * t * t;

            float tangent = MathF.Atan2(nx, -ny);
            cell.Rot += (Math.Clamp(tangent, -0.7f, 0.7f) * t - cell.Rot) * 0.15f * k;
            if (t > 0.35f) cell.CharIndex = FieldChar(dhy, dhx);
        }
        else
        {
            cell.Rot *= 0.85f;
            if (cell.Wake < 0.1f) cell.CharIndex = cell.BaseCharIndex;
        }

        cell.VX += (cell.HomeX - cell.X) * K_HOME * k;
        cell.VY += (cell.HomeY - cell.Y) * K_HOME * k;
        cell.VX *= MathF.Pow(DAMP, k);
        cell.VY *= MathF.Pow(DAMP, k);
        cell.X += cell.VX * k;
        cell.Y += cell.VY * k;

        float rate = targetWake > cell.Wake ? 0.25f : 0.05f;
        cell.Wake += (targetWake - cell.Wake) * rate * k;

        // draw: ink→accent lerp by Wake, scale 1 + Wake*0.2, rotate Rot
        _paint.Color = Lerp(_ink, _accent, cell.Wake).WithAlpha(WakeAlpha(ref cell));
        c.Save();
        c.Translate(cell.X, cell.Y);
        c.RotateRadians(cell.Rot);
        float s = 1 + cell.Wake * 0.2f;
        c.Scale(s, s);
        c.DrawText(_blobs[cell.CharIndex], 0, 0, _paint);
        c.Restore();
    }
}
```

`FieldChar(dy, dx)`: `a = Atan2(dy, dx)` normalized to `[0, 2π)`;
`a < 45° or ≥ 315°` → `>`; `< 135°` → `}`; `< 225°` → `<`; else `{`.

---

## Implementation Plan

- Add `SkiaSharp.Views.Uno.WinUI`; ship `MartianMono-Regular.ttf` as an asset.
- `GlyphFieldBackground` control: canvas hosting, cell array build/rebuild on
  `SizeChanged` (DPI-aware), theme-aware color DPs.
- Static render path first (grid + shimmer + char swaps). Verify on Windows + WASM.
- Physics tick with dt normalization; pointer easing + velocity; field forces,
  realign, shockwave.
- `FieldMode` DP with the two constant profiles; verify runtime switching
  re-settles cleanly through the springs.
- Idle-sleep loop management; reduced-motion path.
- Perf pass: SKTextBlob cache, struct cells, zero-alloc frame confirmed with
  `dotnet-counters` (no Gen0 growth while animating).

## Unresolved Questions

- WASM frame pacing: does `CompositionTarget.Rendering` + `SKXamlCanvas.Invalidate`
  hold 60 fps in the browser, or should the WASM head throttle to 30 and rely on
  the dt normalization?
- Pointer source when content fully covers the background: listen on page root
  (simplest) or a lightweight input-forwarding attached property?
- Multi-pointer (touch): first contact only, or one field per contact?
