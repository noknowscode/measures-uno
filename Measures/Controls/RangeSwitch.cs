using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Measures.Controls;

/// <summary>
/// A period label that is also a switch: "7 DAYS" with a caret, chromeless,
/// and a faint ink tint only under the pointer. It sits where a plain label
/// sits, at the same size and tracking, so the head reads the same whether or
/// not it can change - the caret is the whole affordance, and it is only ever
/// drawn where a tap does something.
/// </summary>
public sealed partial class RangeSwitch : ButtonBase
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(RangeSwitch), new PropertyMetadata(string.Empty, OnTextChanged));

    public RangeSwitch()
    {
        DefaultStyleKey = typeof(RangeSwitch);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        AutomationProperties.SetName(d, $"Range, {e.NewValue}. Switches between 7 and 14 days.");
}
