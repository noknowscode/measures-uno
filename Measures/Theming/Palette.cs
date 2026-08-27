using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Measures.Theming;

/// <summary>
/// Soft Materialism in two axes. The neutral axis carries everything that is
/// read - plaster, paper, charcoal. The light axis carries five temperatures
/// that exist only as translucent atmosphere.
///
/// The two axes change independently: a scheme flip swaps the neutrals outright
/// but leaves every wash hue exactly where it was and only dims the opacities.
/// One switch, five lights - which is why intensity is tokenised apart from hue.
///
/// The law beside the temperatures: hue distinguishes the instrument, never
/// the state. No reading may require the hue, and every state has to stay
/// distinguishable in two non-hue channels - contrast, size, position.
///
/// Re-colours the shared brush instances in Application.Current.Resources rather
/// than re-resolving them, so every consumer follows without a lookup.
/// </summary>
public static class Palette
{
    public enum Token
    {
        // neutral axis
        Canvas,
        Canvas2,
        Card,
        CardLit,
        Ink,
        Quiet,

        // light axis - five temperatures, one per room
        Butter,
        Sage,
        MistBlue,
        Lavender,
        Blush,
    }

    /// <summary>
    /// Wash intensity, tokenised apart from hue. Dark theme dims these and
    /// never touches the colours: a wash is light, and there is simply less of
    /// it at night.
    /// </summary>
    public enum Wash
    {
        Hi,
        Mid,
        Lo,
        Edge,
    }

    static readonly Color[] LightScheme =
    [
        Rgb(0xF5, 0xF0, 0xEC), // Canvas   - plaster
        Rgb(0xE8, 0xE5, 0xE4), // Canvas2  - the field, one step deeper
        Rgb(0xFB, 0xF8, 0xF5), // Card     - uncoated paper
        Rgb(0xFC, 0xFA, 0xF8), // CardLit  - the top of the card, one level up
        Rgb(0x1F, 0x1F, 0x1E), // Ink      - charcoal, and it carries everything
        Rgb(0xA1, 0x9A, 0x96), // Quiet    - taupe

        Rgb(0xF1, 0xE8, 0xCF), // Butter
        Rgb(0xDE, 0xE4, 0xDA), // Sage
        Rgb(0xDC, 0xE4, 0xEA), // MistBlue
        Rgb(0xE4, 0xE0, 0xE8), // Lavender
        Rgb(0xF1, 0xDE, 0xDA), // Blush
    ];

    static readonly Color[] DarkScheme =
    [
        Rgb(0x1A, 0x19, 0x18), // Canvas
        Rgb(0x15, 0x14, 0x13), // Canvas2
        Rgb(0x20, 0x1E, 0x1C), // Card     - warm charcoal
        Rgb(0x28, 0x25, 0x23), // CardLit  - the warmer one
        Rgb(0xEB, 0xE7, 0xE2), // Ink      - warm paper
        Rgb(0x8A, 0x84, 0x80), // Quiet

        // the light axis does not move between schemes
        Rgb(0xF1, 0xE8, 0xCF), // Butter
        Rgb(0xDE, 0xE4, 0xDA), // Sage
        Rgb(0xDC, 0xE4, 0xEA), // MistBlue
        Rgb(0xE4, 0xE0, 0xE8), // Lavender
        Rgb(0xF1, 0xDE, 0xDA), // Blush
    ];

    // Hi and Edge are given by the brief; Mid and Lo are interpolated on the
    // same 0.176 ratio, so a scheme flip dims all four by the same amount.
    static readonly double[] LightWash = [0.85, 0.45, 0.18, 0.55];
    static readonly double[] DarkWash = [0.15, 0.08, 0.03, 0.07];

    /// <summary>
    /// The contrast ramp: hierarchy by opacity, never by colour. Primary
    /// series at full ink; historical around 40%; references (dashed road
    /// ahead, AVG rules) at 15%; axis whispers at 45%.
    /// </summary>
    static readonly (string Key, Token Token, double Alpha)[] Solids =
    [
        ("CanvasBrush", Token.Canvas, 1.00),
        ("Canvas2Brush", Token.Canvas2, 1.00),
        ("CardBrush", Token.Card, 1.00),
        ("CardLitBrush", Token.CardLit, 1.00),
        ("InkBrush", Token.Ink, 1.00),
        ("QuietBrush", Token.Quiet, 1.00),

        // the numeral's unit sits a step back from the period label beside it
        ("UnitBrush", Token.Quiet, 0.75),

        // the head's metric name leads; everything else up there stays quiet
        ("InkLeadBrush", Token.Ink, 0.62),

        ("HairlineBrush", Token.Ink, 0.10),
        ("InkSoft12Brush", Token.Ink, 0.12),
        ("ReferenceBrush", Token.Ink, 0.15),
        ("InkSoft35Brush", Token.Ink, 0.35),
        ("InkSoft45Brush", Token.Ink, 0.45),
        ("InkSoft50Brush", Token.Ink, 0.50),
    ];

    /// <summary>
    /// Flat washes at their own light-scheme intensity. They still obey the
    /// rule the four Wash levels obey - dark dims, never recolours - by riding
    /// the same <see cref="Dim"/> ratio rather than carrying a second table.
    /// </summary>
    static readonly (string Key, Token Token, double Alpha)[] WashSolids =
    [
        // 0.6, down from the brief's opening 0.9: at full strength the wake
        // stopped reading as light on the road and started reading as "pink",
        // which is the one thing a temperature may never do
        ("AtmoDistanceBrush", Token.Blush, 0.6),
    ];

    /// <summary>
    /// The atmospheres, named for what they light rather than for their hue:
    /// effort, frequency, rest and depth, distance. Defined here and only here
    /// - a gradient stop with no colour renders black, so no chart may build
    /// its own wash inline, and <see cref="Verify"/> checks every stop at start.
    /// </summary>
    static readonly (string Key, (Token Token, Wash Wash)[] Stops)[] Washes =
    [
        // the mist under the ridge: dawn light pooling in the valley
        ("AtmoEffortBrush", [(Token.Butter, Wash.Hi), (Token.Butter, Wash.Lo)]),

        // air after rain, cast under the step line
        ("AtmoFrequencyBrush", [(Token.Sage, Wash.Hi), (Token.Sage, Wash.Lo)]),

        // dusk: lavender across the sky, nothing at either edge of the night
        ("AtmoRestBrush", [(Token.Lavender, Wash.Lo), (Token.Lavender, Wash.Mid), (Token.Lavender, Wash.Mid), (Token.Lavender, Wash.Lo)]),

        // and mist-blue for the depth of the valley itself
        ("AtmoDepthBrush", [(Token.MistBlue, Wash.Mid), (Token.MistBlue, Wash.Lo)]),
    ];

    static readonly (string Key, (Token Token, double Alpha)[] Stops)[] Gradients =
    [
        ("PageBrush", [(Token.Canvas, 1.00), (Token.Canvas2, 1.00)]),
        ("CardSurfaceBrush", [(Token.CardLit, 1.00), (Token.Card, 1.00), (Token.Card, 1.00), (Token.Canvas, 1.00)]),
    ];

    static Color[] _scheme = LightScheme;
    static double[] _wash = LightWash;
    static bool _verified;

    public static bool IsDark { get; private set; }

    public static Color Get(Token token) => _scheme[(int)token];

    public static Color Get(Token token, double alpha) => Blend(_scheme[(int)token], alpha);

    /// <summary>
    /// A temperature at one of the four tokenised intensities. Deliberately not
    /// an overload of Get: an enum sits next to a double in overload resolution,
    /// and Get(token, 0) would be ambiguous for the compiler and worse for a
    /// reader.
    /// </summary>
    public static Color Light(Token token, Wash wash) => Blend(_scheme[(int)token], Intensity(wash));

    public static double Intensity(Wash wash) => _wash[(int)wash];

    /// <summary>
    /// How much of its daylight strength a wash keeps in the current scheme:
    /// 1 in light, and the same fraction every tokenised level is dimmed by at
    /// night. Anything painting a temperature outside the four levels scales by
    /// this, so one switch really does dim all five lights together.
    /// </summary>
    static double Dim => _wash[(int)Wash.Hi] / LightWash[(int)Wash.Hi];

    public static void Apply(ElementTheme theme)
    {
        IsDark = theme == ElementTheme.Dark;
        _scheme = IsDark ? DarkScheme : LightScheme;
        _wash = IsDark ? DarkWash : LightWash;

        var res = Application.Current.Resources;

        if (!_verified)
        {
            Verify(res);
            _verified = true;
        }

        foreach (var (key, token, alpha) in Solids)
        {
            ((SolidColorBrush)res[key]).Color = Get(token, alpha);
        }

        foreach (var (key, stops) in Gradients)
        {
            Recolour(res, key, stops.Select(s => Get(s.Token, s.Alpha)));
        }

        foreach (var (key, token, alpha) in WashSolids)
        {
            ((SolidColorBrush)res[key]).Color = Get(token, alpha * Dim);
        }

        foreach (var (key, stops) in Washes)
        {
            Recolour(res, key, stops.Select(s => Light(s.Token, s.Wash)));
        }

        Recoloured?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Raised after every scheme change, for the brushes controls own privately
    /// - per-card edge lights and per-mark fills, which animate and so cannot
    /// be shared instances.
    /// </summary>
    public static event EventHandler? Recoloured;

    /// <summary>
    /// The guard from a real incident: a wash whose dictionary entry went
    /// missing, or whose stop count drifted from this table, painted black.
    /// Every key this class recolours has to exist with the shape it expects,
    /// and it is cheaper to find that out at the first Apply than on a device.
    /// </summary>
    static void Verify(ResourceDictionary res)
    {
        var faults = new List<string>();

        foreach (var (key, _, _) in Solids.Concat(WashSolids))
        {
            if (!res.TryGetValue(key, out var found) || found is not SolidColorBrush)
            {
                faults.Add($"{key} is not a SolidColorBrush in Tokens.xaml");
            }
        }

        foreach (var (key, stops) in Washes.Select(w => (w.Key, w.Stops.Length)).Concat(Gradients.Select(g => (g.Key, g.Stops.Length))))
        {
            if (!res.TryGetValue(key, out var found) || found is not LinearGradientBrush brush)
            {
                faults.Add($"{key} is not a LinearGradientBrush in Tokens.xaml");
            }
            else if (brush.GradientStops.Count != stops)
            {
                faults.Add($"{key} has {brush.GradientStops.Count} stops in Tokens.xaml and {stops} in Palette");
            }
        }

        if (faults.Count > 0)
        {
            throw new InvalidOperationException("Palette and Tokens.xaml disagree:\n" + string.Join("\n", faults));
        }
    }

    static void Recolour(ResourceDictionary res, string key, IEnumerable<Color> colours)
    {
        var brush = (LinearGradientBrush)res[key];
        var i = 0;

        foreach (var colour in colours)
        {
            brush.GradientStops[i++].Color = colour;
        }
    }

    static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    static Color Blend(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), c.R, c.G, c.B);
}
