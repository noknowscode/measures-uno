using Measures.Motion;
using Measures.Theming;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Storage;

namespace Measures.Controls;

/// <summary>
/// A full-bleed field of code glyphs at hairline contrast, sitting behind the
/// page. It answers to <see cref="Source"/> - the card on top of it - not to
/// the pointer: glyphs part around the card edges, turn to face away from it,
/// stream past as it is dragged, and close in behind it.
///
/// Placed as the first child of a root Grid. It takes no input at all.
/// </summary>
public sealed partial class GlyphFieldBackground : SKCanvasElement
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(FieldMode), typeof(GlyphFieldBackground),
        new PropertyMetadata(FieldMode.Magnet, OnModeChanged));

    /// <summary>
    /// Painted under the glyphs. Null leaves whatever is behind the control
    /// alone, which is the useful default - the page keeps its own ground.
    /// Solid brushes only; a gradient here reads as no ground at all.
    /// </summary>
    public static readonly DependencyProperty GroundProperty = DependencyProperty.Register(
        nameof(Ground), typeof(Brush), typeof(GlyphFieldBackground),
        new PropertyMetadata(null, OnColourChanged));

    /// <summary>Resting glyph colour. Null follows the page palette.</summary>
    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
        nameof(Ink), typeof(Brush), typeof(GlyphFieldBackground),
        new PropertyMetadata(null, OnColourChanged));

    /// <summary>What a waked glyph turns into. Null follows the page palette.</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(GlyphFieldBackground),
        new PropertyMetadata(null, OnColourChanged));

    /// <summary>
    /// The element the field bends around. Its rectangle is re-read every frame
    /// in the coordinates of the field, so a drag, a resize and a relayout all
    /// arrive by the same route.
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(FrameworkElement), typeof(GlyphFieldBackground),
        new PropertyMetadata(null, OnSourceChanged));

    /// <summary>
    /// The ink is the page ink held well back - loud enough to read as an
    /// engineered texture, quiet enough that the instruments stay the only
    /// thing worth looking at.
    /// </summary>
    const double InkAlpha = 0.26;

    /// <summary>
    /// Asleep: one frame per second, which is only there so the rare character
    /// rerolls still land. A held per-frame subscription pins the compositor at
    /// display refresh for the life of the process whether or not anything is
    /// moving, so the awake loop is dropped the moment the field settles.
    /// </summary>
    const int IdleTickMs = 1000;

    static SKTypeface? _typeface;
    static Task<SKTypeface?>? _typefaceLoad;

    GlyphField _field = new();
    DispatcherTimer? _idle;
    EventHandler<object>? _rendering;
    IFieldSource? _hooked;
    long _lastTicks;

    public GlyphFieldBackground()
    {
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        // decorative: it has nothing to say to a screen reader, and 3,000
        // glyphs have nothing to say 3,000 times
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);

        _field.ReducedMotion = !Anim.Enabled;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    public FieldMode Mode
    {
        get => (FieldMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public Brush? Ground
    {
        get => (Brush?)GetValue(GroundProperty);
        set => SetValue(GroundProperty, value);
    }

    public Brush? Ink
    {
        get => (Brush?)GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public FrameworkElement? Source
    {
        get => (FrameworkElement?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area) => _field.Draw(canvas);

    static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (GlyphFieldBackground)d;

        // no rebuild: the springs carry the field from one profile to the other
        control._field.Mode = (FieldMode)e.NewValue;
        control.Wake();
    }

    static void OnColourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((GlyphFieldBackground)d).ApplyColours();

    static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (GlyphFieldBackground)d;

        control.Unhook(e.OldValue as FrameworkElement);
        control.Hook(e.NewValue as FrameworkElement);
        control.Wake();
    }

    static SKColor ToSkia(Brush? brush, SKColor fallback) =>
        brush is SolidColorBrush solid
            ? new SKColor(solid.Color.R, solid.Color.G, solid.Color.B,
                (byte)Math.Clamp(solid.Color.A * solid.Opacity, 0, 255))
            : fallback;

    static SKColor FromPalette(Palette.Token token, double alpha)
    {
        var colour = Palette.Get(token, alpha);

        return new SKColor(colour.R, colour.G, colour.B, colour.A);
    }

    /// <summary>
    /// Loaded once for the process. Until it lands the field draws in the
    /// platform default mono, then swaps typeface underneath itself - a
    /// background is never worth blocking a first frame for.
    /// </summary>
    static Task<SKTypeface?> LoadTypefaceAsync()
    {
        return _typefaceLoad ??= LoadAsync();

        static async Task<SKTypeface?> LoadAsync()
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Assets/Fonts/MartianMono-Regular.ttf"));

                using var stream = await file.OpenStreamForReadAsync();
                using var buffer = new MemoryStream();

                // SKTypeface.FromStream wants to seek; an app package stream
                // does not always oblige, so it reads from a copy
                await stream.CopyToAsync(buffer);
                buffer.Position = 0;

                _typeface = SKTypeface.FromStream(buffer);
            }
            catch (Exception)
            {
                // the field is decorative; any mono is a fine stand-in
                _typeface = SKTypeface.FromFamilyName("Consolas");
            }

            return _typeface;
        }
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        // a page can be navigated away from and back to on the same instance,
        // and the field was disposed on the way out
        if (_field.IsDisposed)
        {
            _field = new GlyphField
            {
                Mode = Mode,
                ReducedMotion = !Anim.Enabled,
            };

            _field.SetTypeface(_typeface);
            _field.Resize((float)ActualWidth, (float)ActualHeight);
        }

        ApplyColours();
        Palette.Recoloured += OnRecoloured;
        Hook(Source);

        _ = AttachTypefaceAsync();

        Wake();
        Invalidate();
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Palette.Recoloured -= OnRecoloured;
        Unhook(Source);
        Sleep();
        StopIdle();

        _field.ClearSource();
        _field.Dispose();
    }

    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _field.Resize((float)e.NewSize.Width, (float)e.NewSize.Height);
        Invalidate();
    }

    void OnRecoloured(object? sender, EventArgs e)
    {
        ApplyColours();
        Invalidate();
    }

    async Task AttachTypefaceAsync()
    {
        var typeface = await LoadTypefaceAsync();

        // the control can be gone by the time the font lands
        if (!IsLoaded)
        {
            return;
        }

        _field.SetTypeface(typeface);
        Invalidate();
    }

    void ApplyColours()
    {
        _field.Ground = ToSkia(Ground, SKColors.Transparent);
        _field.Ink = ToSkia(Ink, FromPalette(Palette.Token.Ink, InkAlpha));
        _field.Accent = ToSkia(Accent, FromPalette(Palette.Token.Ai, 1));
    }

    /// <summary>
    /// Runs the field at display refresh. A DispatcherTimer at 16ms beats
    /// against vsync and the field visibly judders; the per-frame callback is
    /// the only clock that lands on the frames actually being presented. It is
    /// dropped again the moment the field settles.
    /// </summary>
    void Wake()
    {
        if (_field.ReducedMotion || _rendering is not null || !IsLoaded)
        {
            return;
        }

        StopIdle();

        _lastTicks = Environment.TickCount64;
        _rendering = OnRendering;
        CompositionTarget.Rendering += _rendering;
    }

    void OnRendering(object? sender, object e)
    {
        if (Advance() is not true)
        {
            return;
        }

        Sleep();
        StartIdle();
    }

    /// <summary>One frame. True when the field has settled and the loop can be dropped.</summary>
    bool Advance()
    {
        var now = Environment.TickCount64;
        var dt = Math.Clamp(now - _lastTicks, 1, 100);
        _lastTicks = now;

        ReadSource();
        _field.Step(dt);
        Invalidate();

        return _field.IsAtRest;
    }

    void Sleep()
    {
        if (_rendering is null)
        {
            return;
        }

        CompositionTarget.Rendering -= _rendering;
        _rendering = null;
    }

    /// <summary>The heartbeat that keeps the rare character rerolls alive while nothing moves.</summary>
    void StartIdle()
    {
        if (_idle is not null || _field.ReducedMotion)
        {
            return;
        }

        _idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IdleTickMs) };
        _idle.Tick += OnIdle;
        _idle.Start();
    }

    void OnIdle(object? sender, object e)
    {
        if (Advance() is false)
        {
            Wake();
        }
    }

    void StopIdle()
    {
        if (_idle is null)
        {
            return;
        }

        _idle.Stop();
        _idle.Tick -= OnIdle;
        _idle = null;
    }

    void Hook(FrameworkElement? source)
    {
        if (source is null)
        {
            _field.ClearSource();
            return;
        }

        if (source is IFieldSource live)
        {
            _hooked = live;
            live.Moved += OnSourceMoved;
            live.Released += OnSourceReleased;
        }

        source.SizeChanged += OnSourceLayoutChanged;
        ReadSource();
    }

    void Unhook(FrameworkElement? source)
    {
        if (_hooked is not null)
        {
            _hooked.Moved -= OnSourceMoved;
            _hooked.Released -= OnSourceReleased;
            _hooked = null;
        }

        if (source is not null)
        {
            source.SizeChanged -= OnSourceLayoutChanged;
        }
    }

    void OnSourceMoved(object? sender, EventArgs e) => Wake();

    void OnSourceLayoutChanged(object sender, SizeChangedEventArgs e) => Wake();

    /// <summary>The card landing hits the field like a dropped weight.</summary>
    void OnSourceReleased(object? sender, EventArgs e)
    {
        if (Source is { ActualWidth: > 0 } source)
        {
            var origin = source.TransformToVisual(this).TransformPoint(new Point(0, 0));

            _field.Shockwave(
                (float)(origin.X + source.ActualWidth / 2),
                (float)(origin.Y + source.ActualHeight / 2));
        }

        Wake();
    }

    /// <summary>
    /// Re-read every frame rather than pushed on change: a drag, an entrance
    /// transform, a scroll and a relayout all move the card, and only the
    /// transform between the two elements knows about all of them.
    /// </summary>
    void ReadSource()
    {
        if (Source is not { ActualWidth: > 0, IsLoaded: true } source)
        {
            return;
        }

        var origin = source.TransformToVisual(this).TransformPoint(new Point(0, 0));

        _field.SetSource(
            (float)origin.X,
            (float)origin.Y,
            (float)(origin.X + source.ActualWidth),
            (float)(origin.Y + source.ActualHeight));
    }
}
