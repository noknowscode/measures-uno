using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Measures.Controls;

/// <summary>
/// One line of in-chart text with a 3 epx card-coloured halo, so a label stays
/// legible across fills, lines, dashes and grain. The prototype does it with
/// paint-order:stroke; XAML has no text stroke, so the halo is the same text
/// drawn eight times in the card colour around one copy in the ink. Cheap for
/// a dozen characters, and it reads exactly like the stroke does.
///
/// Mono, upper case for dates and references, no tracking:
/// TextBlock.CharacterSpacing is a no-op on this
/// target, and at these sizes the per-glyph layout SpacedText uses would cost
/// nine TextBlocks per character.
/// </summary>
public sealed partial class HaloText : Grid
{
    /// <summary>The halo reaches 1.5 epx from the glyph in every direction.</summary>
    const double Reach = 1.5;

    static readonly (double X, double Y)[] Offsets =
    [
        (Reach, 0), (-Reach, 0), (0, Reach), (0, -Reach),
        (Reach * 0.707, Reach * 0.707), (-Reach * 0.707, Reach * 0.707),
        (Reach * 0.707, -Reach * 0.707), (-Reach * 0.707, -Reach * 0.707),
    ];

    readonly List<TextBlock> _blocks = [];
    readonly TextBlock _ink;
    readonly bool _upperCase;

    public HaloText(double fontSize, Brush foreground, bool upperCase = true)
    {
        _upperCase = upperCase;

        var res = Application.Current.Resources;
        var font = (FontFamily)res["MonoFont"];
        var halo = (Brush)res["CardBrush"];

        foreach (var (x, y) in Offsets)
        {
            var copy = Block(font, fontSize, halo);
            copy.RenderTransform = new TranslateTransform { X = x, Y = y };
            Children.Add(copy);
            _blocks.Add(copy);
        }

        _ink = Block(font, fontSize, foreground);
        Children.Add(_ink);
        _blocks.Add(_ink);

        IsHitTestVisible = false;
    }

    public string Text
    {
        get => _ink.Text;
        set
        {
            var text = value ?? string.Empty;

            if (_upperCase)
            {
                text = text.ToUpperInvariant();
            }

            foreach (var block in _blocks)
            {
                block.Text = text;
            }
        }
    }

    static TextBlock Block(FontFamily font, double size, Brush brush) => new()
    {
        FontFamily = font,
        FontSize = size,
        Foreground = brush,
        TextWrapping = TextWrapping.NoWrap,
        IsTextSelectionEnabled = false,
    };
}
