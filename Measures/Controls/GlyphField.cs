using SkiaSharp;

namespace Measures.Controls;

/// <summary>
/// Part opens the grid around the card and closes it behind; Magnet gathers the
/// glyphs into a band hugging its edges. One pipeline, two constant profiles -
/// switching at runtime only swaps the profile and lets the springs re-settle.
/// </summary>
public enum FieldMode
{
    Magnet,
    Part,
}

/// <summary>
/// The magnetic glyph grid: a flat array of cells, one fused physics-and-draw
/// pass, no XAML types. Kept out of the element so the forces can be reasoned
/// about without a window.
///
/// A field of ~3,000 cells rules out an element per glyph; every frame is one
/// Skia pass over a struct array, drawing cached SKTextBlobs so nothing
/// allocates per frame.
/// </summary>
public sealed class GlyphField : IDisposable
{
    /// <summary>The seven shapes drawn, one cached blob each.</summary>
    static readonly char[] Glyphs = ['<', '/', '>', '{', '}', ';', '='];

    /// <summary>The pool a cell rolls from - weighted toward the tag glyphs.</summary>
    static readonly byte[] Pool = [0, 1, 2, 0, 1, 2, 3, 4, 5, 6];

    const byte Left = 0;
    const byte Right = 2;
    const byte OpenBrace = 3;
    const byte CloseBrace = 4;

    public const float Spacing = 26f;
    public const float FontSize = 12f;

    const float KHome = 0.045f;
    const float Damp = 0.86f;
    const float Shock = 9.5f;

    /// <summary>
    /// The rectangle the field follows is an eased one, a fifth of the way to
    /// the real card each tick. That lag is the whole feel: the field opens
    /// ahead of the card and closes behind it instead of being nailed to it.
    /// </summary>
    const float SourceEase = 0.2f;

    const float WakeRise = 0.25f;
    const float WakeDecay = 0.05f;

    /// <summary>Below this a cell is home, still and cold, and can be snapped.</summary>
    const float RestWake = 0.004f;

    /// <summary>
    /// Eased travel per tick at which the field is fully lit. The wake answers
    /// to the card MOVING, not to the card being near: a card standing still
    /// leaves a quiet dent in the grid, and only a hand dragging it blooms the
    /// accent. Proximity alone would paint a permanent indigo border.
    /// </summary>
    const float FullSpeed = 3.5f;

    /// <summary>Character rerolls, in ms, drawn per cell.</summary>
    const double SwapMinMs = 6000;
    const double SwapMaxMs = 30000;

    // Radii are measured from the card EDGE, not from a point, so the spec's
    // point-source numbers are re-tuned. The one that matters is KField: it
    // sets where the home spring balances the push, and that has to stay under
    // one grid pitch. At 0.018 a cell against the card settles ~24px out - a
    // visible recoil that still leaves every glyph in its own cell. Push it to
    // the spec's 0.05 and two rows collapse into one dark clump.
    static readonly Profile MagnetProfile = new(170f, 42f, 0.055f, 0.55f, 0.35f, 0.35f);
    static readonly Profile PartProfile = new(130f, 60f, 0.018f, 0f, 0.35f, 0.62f);

    readonly SKPaint _paint = new() { IsAntialias = true };
    readonly SKTextBlob?[] _blobs = new SKTextBlob?[Glyphs.Length];
    readonly SKTextBlobBuilder _batch = new();
    readonly ushort[] _glyphIds = new ushort[Glyphs.Length];
    readonly float[] _glyphDx = new float[Glyphs.Length];
    readonly Random _random = new();

    SKFont? _font;
    Cell[] _cells = [];
    ushort[] _restGlyphs = [];
    SKPoint[] _restPositions = [];
    float _glyphDy;

    // the source rectangle, eased, and the per-tick velocity of its centre
    float _targetL;
    float _targetT;
    float _targetR;
    float _targetB;
    float _l;
    float _t;
    float _r;
    float _b;
    float _vcx;
    float _vcy;
    float _activity;
    bool _hasSource;

    double _timeMs;

    public FieldMode Mode { get; set; } = FieldMode.Part;

    /// <summary>Painted under the glyphs. Fully transparent leaves whatever is behind alone.</summary>
    public SKColor Ground { get; set; } = SKColors.Transparent;

    public SKColor Ink { get; set; } = new(0x2C, 0x2E, 0x2C, 0x42);

    public SKColor Accent { get; set; } = new(0x5A, 0x6C, 0x8F, 0xFF);

    /// <summary>With motion off the grid is a still texture: no forces, no shimmer, no shockwave.</summary>
    public bool ReducedMotion { get; set; }

    /// <summary>
    /// True when nothing changed measurably this frame. The parted field around
    /// a still card is a steady state, not a return home, so rest is measured
    /// as "stopped moving", not as "back on the grid".
    /// </summary>
    public bool IsAtRest { get; private set; } = true;

    public int CellCount => _cells.Length;

    public bool IsDisposed { get; private set; }

    Profile Active => Mode == FieldMode.Part ? PartProfile : MagnetProfile;

    public void SetTypeface(SKTypeface? typeface)
    {
        _font?.Dispose();
        _font = new SKFont(typeface ?? SKTypeface.Default, FontSize) { Subpixel = true };

        BuildBlobs();
    }

    /// <summary>
    /// Rebuilds the grid for a new size. Pitch is constant, so the cell count
    /// derives from the size and the leftover is split evenly at the edges -
    /// the field reads as centred rather than anchored to a corner.
    /// </summary>
    public void Resize(float width, float height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var cols = Math.Max(1, (int)(width / Spacing));
        var rows = Math.Max(1, (int)(height / Spacing));

        var originX = (width - cols * Spacing) / 2f + Spacing / 2f;
        var originY = (height - rows * Spacing) / 2f + Spacing / 2f;

        if (_cells.Length != cols * rows)
        {
            _cells = new Cell[cols * rows];
            _restGlyphs = new ushort[_cells.Length];
            _restPositions = new SKPoint[_cells.Length];
        }

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                ref var cell = ref _cells[row * cols + col];

                cell.HomeX = originX + col * Spacing;
                cell.HomeY = originY + row * Spacing;
                cell.BaseCharIndex = Pool[_random.Next(Pool.Length)];
                cell.CharIndex = cell.BaseCharIndex;
                cell.SwapAtMs = _timeMs + NextSwapDelay();
                cell.Snap();
            }
        }

        IsAtRest = false;
    }

    /// <summary>
    /// The rectangle the field bends around, in element coordinates. This is
    /// the card, not the pointer: the field answers to the object on the page.
    /// </summary>
    public void SetSource(float left, float top, float right, float bottom)
    {
        _targetL = left;
        _targetT = top;
        _targetR = right;
        _targetB = bottom;

        if (!_hasSource)
        {
            // arriving cold: snap, or the field gets dragged across the whole
            // page from a rectangle that was never really there
            _l = left;
            _t = top;
            _r = right;
            _b = bottom;
            _hasSource = true;
        }

        IsAtRest = false;
    }

    public void ClearSource()
    {
        _hasSource = false;
        _vcx = 0;
        _vcy = 0;
        IsAtRest = false;
    }

    /// <summary>A press throws a radial impulse out to 1.6x the field radius.</summary>
    public void Shockwave(float x, float y)
    {
        if (ReducedMotion)
        {
            return;
        }

        var radius = Active.FieldR * 1.6f;

        for (var i = 0; i < _cells.Length; i++)
        {
            ref var cell = ref _cells[i];

            var dx = cell.HomeX - x;
            var dy = cell.HomeY - y;
            var d = MathF.Sqrt(dx * dx + dy * dy);

            if (d >= radius)
            {
                continue;
            }

            var falloff = 1 - d / radius;

            // dead centre has no direction to fly; leave it, the neighbours move
            if (d > 0.001f)
            {
                cell.VX += dx / d * falloff * Shock;
                cell.VY += dy / d * falloff * Shock;
            }

            cell.Wake = MathF.Max(cell.Wake, falloff);
        }

        IsAtRest = false;
    }

    /// <summary>
    /// Advances the field one frame. Runs on the UI thread, where the pointer
    /// state lives and where the caller can act on <see cref="IsAtRest"/>;
    /// drawing is a separate read-only pass on the render thread.
    /// </summary>
    public void Step(double dtMs)
    {
        if (_cells.Length == 0 || ReducedMotion)
        {
            IsAtRest = true;
            return;
        }

        _timeMs += dtMs;

        // forces are tuned at 60 fps; everything scales by the frame's share of
        // one, clamped so a stalled frame cannot fling the field apart
        var k = (float)Math.Clamp(dtMs / 16.667, 0.5, 2.0);

        EaseSource(k);

        var profile = Active;
        var damp = MathF.Pow(Damp, k);
        var rotDecay = MathF.Pow(0.85f, k);
        var busiest = 0f;

        for (var i = 0; i < _cells.Length; i++)
        {
            ref var cell = ref _cells[i];

            if (_timeMs >= cell.SwapAtMs && cell.Wake < 0.1f)
            {
                cell.BaseCharIndex = Pool[_random.Next(Pool.Length)];
                cell.CharIndex = cell.BaseCharIndex;
                cell.SwapAtMs = _timeMs + NextSwapDelay();
            }

            // the field is sampled at the cell's HOME, never its current
            // position - a displaced glyph that sampled where it now is would
            // chase its own displacement and never settle
            var nearX = Math.Clamp(cell.HomeX, _l, _r);
            var nearY = Math.Clamp(cell.HomeY, _t, _b);
            var dhx = cell.HomeX - nearX;
            var dhy = cell.HomeY - nearY;
            var d = MathF.Sqrt(dhx * dhx + dhy * dhy);

            // cells under the card are left alone: nobody can see them, and
            // shoving several hundred of them out at once is a lot of energy
            // spent on a hole
            var inField = _hasSource && d > 0.001f && d < profile.FieldR;

            if (!inField && cell.IsHome())
            {
                // the cheap path, and the one almost every cell takes
                continue;
            }

            var beforeX = cell.X;
            var beforeY = cell.Y;
            var beforeWake = cell.Wake;
            var targetWake = 0f;

            if (inField)
            {
                var t = 1 - d / profile.FieldR;
                targetWake = t * _activity;

                var nx = dhx / d;
                var ny = dhy / d;

                // one ring force covers both modes: outside the ring it pulls
                // in, inside it pushes out, so glyphs gather at OrbitR
                cell.VX -= nx * (d - profile.OrbitR) * profile.KField * t * k;
                cell.VY -= ny * (d - profile.OrbitR) * profile.KField * t * k;

                if (profile.KSwirl != 0)
                {
                    cell.VX += -ny * profile.KSwirl * t * t * k;
                    cell.VY += nx * profile.KSwirl * t * t * k;
                }

                cell.VX += _vcx * profile.Kick * t * t;
                cell.VY += _vcy * profile.Kick * t * t;

                // half the spec's swing: 0.7 rad against a straight card edge
                // reads as scribble, where 0.35 reads as glyphs leaning away
                var tangent = MathF.Atan2(nx, -ny);
                cell.Rot += (Math.Clamp(tangent, -0.35f, 0.35f) * t - cell.Rot) * 0.15f * k;

                // kept to a thin rim: a card edge is straight and long, so a
                // generous threshold turns hundreds of cells into one uniform
                // stripe of the same glyph rather than a tell
                if (t > profile.RealignT)
                {
                    cell.CharIndex = FieldChar(dhy, dhx);
                }
            }
            else
            {
                cell.Rot *= rotDecay;

                if (cell.Wake < 0.1f)
                {
                    cell.CharIndex = cell.BaseCharIndex;
                }
            }

            cell.VX += (cell.HomeX - cell.X) * KHome * k;
            cell.VY += (cell.HomeY - cell.Y) * KHome * k;
            cell.VX *= damp;
            cell.VY *= damp;
            cell.X += cell.VX * k;
            cell.Y += cell.VY * k;

            // asymmetric on purpose: the wake floods in and drains slowly, so
            // the accent lingers behind the pointer as a trail
            var rate = targetWake > cell.Wake ? WakeRise : WakeDecay;
            cell.Wake += (targetWake - cell.Wake) * rate * k;

            if (!inField && cell.IsHome())
            {
                cell.Snap();
                continue;
            }

            // rest is measured as change, not as position: a field parted
            // around a card that is standing still has settled even though
            // every cell in the halo is far from home
            var change = MathF.Max(
                MathF.Max(MathF.Abs(cell.X - beforeX), MathF.Abs(cell.Y - beforeY)),
                MathF.Abs(cell.Wake - beforeWake) * 20f);

            busiest = MathF.Max(busiest, change);
        }

        IsAtRest = busiest < 0.02f;
    }

    /// <summary>
    /// Paints the current state. Read-only, so it can run on the render thread
    /// against whatever <see cref="Step"/> last wrote.
    /// </summary>
    public void Draw(SKCanvas canvas)
    {
        if (Ground.Alpha != 0)
        {
            canvas.Clear(Ground);
        }

        // a frame can be in flight on the render thread while the control
        // unloads; drawing through disposed native handles is an access violation
        if (IsDisposed || _cells.Length == 0 || _font is null)
        {
            return;
        }

        // Everything still on the grid goes out as ONE positioned run. Drawn
        // per cell, three thousand glyphs cost three thousand colour setters
        // and three thousand draw calls a frame - about 6,000 native calls,
        // which is what made this judder. Batched, the resting field is a
        // single call and only the disturbed halo is drawn glyph by glyph.
        var resting = 0;
        var shimmer = Shimmer;

        for (var i = 0; i < _cells.Length; i++)
        {
            ref var cell = ref _cells[i];

            if (!cell.IsHome())
            {
                continue;
            }

            _restGlyphs[resting] = _glyphIds[cell.CharIndex];
            _restPositions[resting] = new SKPoint(
                cell.HomeX + _glyphDx[cell.CharIndex],
                cell.HomeY + _glyphDy);
            resting++;
        }

        if (resting > 0)
        {
            _paint.Color = Tint(Ink, shimmer);
            _batch.AddPositionedRun(
                _restGlyphs.AsSpan(0, resting),
                _font,
                _restPositions.AsSpan(0, resting));

            using var run = _batch.Build();
            canvas.DrawText(run, 0, 0, _paint);
        }

        for (var i = 0; i < _cells.Length; i++)
        {
            ref var cell = ref _cells[i];

            if (cell.IsHome())
            {
                continue;
            }

            // waked glyphs lerp ink to accent, come up to full alpha, and grow
            _paint.Color = Blend(Ink, Accent, cell.Wake, shimmer + (1 - shimmer) * cell.Wake);

            canvas.Save();
            canvas.Translate(cell.X, cell.Y);
            canvas.RotateRadians(cell.Rot);

            var scale = 1 + cell.Wake * 0.2f;
            canvas.Scale(scale, scale);
            canvas.DrawText(_blobs[cell.CharIndex], 0, 0, _paint);
            canvas.Restore();
        }
    }

    /// <summary>
    /// Which way a waked glyph points: the sector of the pointer the cell sits
    /// in, quadrants centred on the axes. To the right reads as a right angle
    /// bracket, below as a closing brace. In Part mode the same rule points
    /// them away from the pointer, which is what repulsion should look like.
    /// </summary>
    public static byte FieldChar(float dy, float dx)
    {
        var a = MathF.Atan2(dy, dx);

        if (a < 0)
        {
            a += MathF.Tau;
        }

        const float Quarter = MathF.PI / 4;

        if (a < Quarter || a >= 7 * Quarter)
        {
            return Right;
        }

        if (a < 3 * Quarter)
        {
            return CloseBrace;
        }

        if (a < 5 * Quarter)
        {
            return Left;
        }

        return OpenBrace;
    }

    public void Dispose()
    {
        IsDisposed = true;
        _batch.Dispose();
        _paint.Dispose();
        _font?.Dispose();
        _font = null;

        for (var i = 0; i < _blobs.Length; i++)
        {
            _blobs[i]?.Dispose();
            _blobs[i] = null;
        }
    }

    void EaseSource(float k)
    {
        if (!_hasSource)
        {
            _vcx = 0;
            _vcy = 0;
            return;
        }

        var ease = Math.Min(SourceEase * k, 1f);
        var beforeCx = (_l + _r) / 2f;
        var beforeCy = (_t + _b) / 2f;

        _l += (_targetL - _l) * ease;
        _t += (_targetT - _t) * ease;
        _r += (_targetR - _r) * ease;
        _b += (_targetB - _b) * ease;

        _vcx = (_l + _r) / 2f - beforeCx;
        _vcy = (_t + _b) / 2f - beforeCy;

        var speed = MathF.Sqrt(_vcx * _vcx + _vcy * _vcy) / k;
        _activity = Math.Clamp(speed / FullSpeed, 0, 1);
    }

    double NextSwapDelay() => SwapMinMs + _random.NextDouble() * (SwapMaxMs - SwapMinMs);

    /// <summary>
    /// Base 0.9 with a slow breath of 0.04. The spec gives every cell its own
    /// phase; one shared phase costs a single colour setter for the whole
    /// resting field instead of one per glyph, and at four hundredths of an
    /// alpha step nobody has ever seen the difference.
    /// </summary>
    float Shimmer =>
        ReducedMotion ? 0.9f : 0.9f + 0.04f * MathF.Sin((float)(_timeMs / 1600.0));

    void BuildBlobs()
    {
        for (var i = 0; i < _blobs.Length; i++)
        {
            _blobs[i]?.Dispose();
            _blobs[i] = null;
        }

        if (_font is null)
        {
            return;
        }

        var metrics = _font.Metrics;

        // blobs are built centred on the cell, so the draw call is one point
        // and the rotation pivots on the glyph rather than its baseline corner
        _glyphDy = -(metrics.Ascent + metrics.Descent) / 2f;

        for (var i = 0; i < Glyphs.Length; i++)
        {
            var text = Glyphs[i].ToString();

            _glyphDx[i] = -_font.MeasureText(text) / 2f;
            _glyphIds[i] = _font.GetGlyph(Glyphs[i]);
            _blobs[i] = SKTextBlob.Create(text, _font, new SKPoint(_glyphDx[i], _glyphDy));
        }
    }

    static SKColor Tint(SKColor color, float alpha) =>
        color.WithAlpha((byte)Math.Clamp(color.Alpha * alpha, 0, 255));

    static SKColor Blend(SKColor from, SKColor to, float u, float alpha)
    {
        u = Math.Clamp(u, 0, 1);

        var r = (byte)(from.Red + (to.Red - from.Red) * u);
        var g = (byte)(from.Green + (to.Green - from.Green) * u);
        var b = (byte)(from.Blue + (to.Blue - from.Blue) * u);
        var a = from.Alpha + (to.Alpha - from.Alpha) * u;

        return new SKColor(r, g, b, (byte)Math.Clamp(a * alpha, 0, 255));
    }

    readonly record struct Profile(
        float FieldR,
        float OrbitR,
        float KField,
        float KSwirl,
        float Kick,
        float RealignT);

    struct Cell
    {
        public float HomeX;
        public float HomeY;
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Rot;
        public float Wake;
        public byte BaseCharIndex;
        public byte CharIndex;
        public double SwapAtMs;

        /// <summary>On the grid, unrotated and cold: the cheap draw, and nothing to integrate.</summary>
        public readonly bool IsHome() =>
            Wake < RestWake
            && MathF.Abs(VX) < 0.01f
            && MathF.Abs(VY) < 0.01f
            && MathF.Abs(X - HomeX) < 0.05f
            && MathF.Abs(Y - HomeY) < 0.05f
            && MathF.Abs(Rot) < 0.002f;

        public void Snap()
        {
            X = HomeX;
            Y = HomeY;
            VX = 0;
            VY = 0;
            Rot = 0;
            Wake = 0;
        }
    }
}
