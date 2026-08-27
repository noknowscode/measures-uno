using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Shapes;

namespace Measures.Controls;

/// <summary>
/// One day of the step line as a hit zone: the full height of the plot and the
/// full width of its slot, so a fingertip lands on a day rather than on a
/// line. It draws nothing of its own; the chart draws the marks. It is a real
/// focus stop, and the ring shows only for keyboard focus.
/// </summary>
public sealed partial class DaySlot : ButtonBase
{
    Rectangle? _focus;

    public DaySlot()
    {
        DefaultStyleKey = typeof(DaySlot);

        // :focus-visible, not :focus - a pointer press leaves no ring
        GotFocus += (_, _) => ShowFocus(FocusState == FocusState.Keyboard);
        LostFocus += (_, _) => ShowFocus(false);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _focus = GetTemplateChild("PART_Focus") as Rectangle;

        if (_focus is not null)
        {
            _focus.Opacity = 0;
        }
    }

    void ShowFocus(bool visible)
    {
        if (_focus is not null)
        {
            _focus.Opacity = visible ? 1 : 0;
        }
    }
}
