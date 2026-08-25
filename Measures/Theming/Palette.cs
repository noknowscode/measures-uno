using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Measures.Theming;

/// <summary>
/// Owns the two schemes and re-colours the shared brush instances in
/// Application.Current.Resources when the OS scheme flips. Because every
/// consumer holds the same brush instance, nothing has to be re-resolved.
/// </summary>
public static class Palette
{
    public enum Token { Mist, Mist2, Card, CardLit, Ink, Quiet, Ai }

    static readonly Color[] Light =
    [
        Color.FromArgb(255, 0xF6, 0xF7, 0xF5), // Mist
        Color.FromArgb(255, 0xF1, 0xF3, 0xF1), // Mist2
        Color.FromArgb(255, 0xFB, 0xFC, 0xFB), // Card
        Color.FromArgb(255, 0xFC, 0xFD, 0xFC), // CardLit
        Color.FromArgb(255, 0x2C, 0x2E, 0x2C), // Ink
        Color.FromArgb(255, 0x9A, 0xA0, 0x9B), // Quiet
        Color.FromArgb(255, 0x5A, 0x6C, 0x8F), // Ai
    ];

    static readonly Color[] Dark =
    [
        Color.FromArgb(255, 0x15, 0x17, 0x14), // Mist
        Color.FromArgb(255, 0x10, 0x12, 0x10), // Mist2
        Color.FromArgb(255, 0x1B, 0x1E, 0x1B), // Card
        Color.FromArgb(255, 0x1D, 0x20, 0x1D), // CardLit
        Color.FromArgb(255, 0xE6, 0xE8, 0xE4), // Ink
        Color.FromArgb(255, 0x87, 0x8E, 0x87), // Quiet
        Color.FromArgb(255, 0x8F, 0xA3, 0xCB), // Ai
    ];

    static readonly (string Key, Token Token, double Alpha)[] Solids =
    [
        ("MistBrush",      Token.Mist,  1.00),
        ("Mist2Brush",     Token.Mist2, 1.00),
        ("CardBrush",      Token.Card,    1.00),
        ("CardLitBrush",   Token.CardLit, 1.00),
        ("InkBrush",       Token.Ink,   1.00),
        ("QuietBrush",     Token.Quiet, 1.00),
        ("AiBrush",        Token.Ai,    1.00),
        ("HairlineBrush",  Token.Ink,   0.10),
        ("InkSoft12Brush", Token.Ink,   0.12),
        ("InkSoft22Brush", Token.Ink,   0.22),
        ("InkSoft35Brush", Token.Ink,   0.35),
        ("AiSoft50Brush",  Token.Ai,    0.50),
        ("AiSoft40Brush",  Token.Ai,    0.40),
        ("AiSoft35Brush",  Token.Ai,    0.35),
    ];

    static readonly (string Key, (Token Token, double Alpha)[] Stops)[] Gradients =
    [
        ("PageBrush",        [(Token.Mist, 1.00), (Token.Mist2, 1.00)]),
        ("CardSurfaceBrush", [(Token.CardLit, 1.00), (Token.Card, 1.00), (Token.Card, 1.00), (Token.Mist, 1.00)]),
        ("RidgeMistBrush", [(Token.Ai, 0.16), (Token.Ai, 0.00)]),
        ("NightSkyBrush",  [(Token.Ai, 0.00), (Token.Ai, 0.10), (Token.Ai, 0.10), (Token.Ai, 0.00)]),
        ("RestFillBrush",  [(Token.Ai, 0.20), (Token.Ai, 0.02)]),
        ("StemPourBrush",  [(Token.Ai, 1.00), (Token.Ai, 0.35)]),
    ];

    static Color[] _scheme = Light;

    public static bool IsDark { get; private set; }

    public static Color Get(Token token) => _scheme[(int)token];

    public static Color Get(Token token, double alpha) =>
        Blend(_scheme[(int)token], alpha);

    static Color Blend(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B);

    public static void Apply(ElementTheme theme)
    {
        IsDark = theme == ElementTheme.Dark;
        _scheme = IsDark ? Dark : Light;

        var res = Application.Current.Resources;

        foreach (var (key, token, alpha) in Solids)
        {
            if (res[key] is SolidColorBrush brush)
            {
                brush.Color = Get(token, alpha);
            }
        }

        foreach (var (key, stops) in Gradients)
        {
            if (res[key] is not LinearGradientBrush brush)
            {
                continue;
            }

            for (var i = 0; i < stops.Length && i < brush.GradientStops.Count; i++)
            {
                brush.GradientStops[i].Color = Get(stops[i].Token, stops[i].Alpha);
            }
        }

        Recoloured?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Raised after every scheme change, for the handful of brushes charts own
    /// privately (per-dot fills that animate, so they cannot be shared).
    /// </summary>
    public static event EventHandler? Recoloured;
}
