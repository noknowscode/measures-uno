using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Measures.Controls;

/// <summary>
/// Letterspaced label type. TextBlock.CharacterSpacing is a silent no-op on the
/// Skia text stack, so the tracking is laid out one glyph per TextBlock in a
/// StackPanel with Spacing. Exact, and it leaves the glyph shapes alone - a
/// horizontal ScaleTransform would thicken every vertical stem by the same
/// percentage it widens, which is very visible at these weights.
/// </summary>
public sealed partial class SpacedText : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SpacedText), new PropertyMetadata(string.Empty, OnVisualChanged));

    /// <summary>Tracking in em, matching the CSS letter-spacing it replaces.</summary>
    public static readonly DependencyProperty TrackingProperty = DependencyProperty.Register(
        nameof(Tracking), typeof(double), typeof(SpacedText), new PropertyMetadata(0.18, OnVisualChanged));

    public static readonly DependencyProperty IsUpperCaseProperty = DependencyProperty.Register(
        nameof(IsUpperCase), typeof(bool), typeof(SpacedText), new PropertyMetadata(false, OnVisualChanged));

    StackPanel? _host;

    public SpacedText()
    {
        DefaultStyleKey = typeof(SpacedText);

        RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => Rebuild());
        RegisterPropertyChangedCallback(FontFamilyProperty, (_, _) => Rebuild());
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => Rebuild());
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double Tracking
    {
        get => (double)GetValue(TrackingProperty);
        set => SetValue(TrackingProperty, value);
    }

    /// <summary>Label case, kept out of the source strings so they stay readable.</summary>
    public bool IsUpperCase
    {
        get => (bool)GetValue(IsUpperCaseProperty);
        set => SetValue(IsUpperCaseProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _host = GetTemplateChild("PART_Host") as StackPanel;
        Rebuild();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SpacedTextAutomationPeer(this);

    static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SpacedText)d).Rebuild();

    void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        var source = Text ?? string.Empty;
        var text = IsUpperCase ? source.ToUpperInvariant() : source;

        // the whole word, so assistive tech never reads it letter by letter
        AutomationProperties.SetName(this, source);

        _host.Children.Clear();
        _host.Spacing = FontSize * Tracking;

        foreach (var glyph in text)
        {
            var block = new TextBlock
            {
                Text = glyph.ToString(),
                FontFamily = FontFamily,
                FontSize = FontSize,
                FontStyle = FontStyle,
                FontWeight = FontWeight,
                Foreground = Foreground,
            };

            if (glyph == ' ')
            {
                // a lone space measures inconsistently once it is its own run
                block.Width = FontSize * 0.34;
            }

            _host.Children.Add(block);
        }
    }

    sealed class SpacedTextAutomationPeer(SpacedText owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetNameCore() => ((SpacedText)Owner).Text ?? string.Empty;


        protected override IList<AutomationPeer> GetChildrenCore() => [];
    }
}
