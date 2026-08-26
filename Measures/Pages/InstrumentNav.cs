using Measures.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Measures.Pages;

/// <summary>The four instruments, one page each.</summary>
public enum Instrument
{
    Ridge,
    Rain,
    Night,
    Road,
}

/// <summary>
/// The masthead every instrument page wears: the wordmark, the four names, and
/// the replay control. It owns the navigation itself, so a page only has to say
/// which instrument it is.
/// </summary>
public sealed partial class InstrumentNav : Control
{
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(
        nameof(Current), typeof(Instrument), typeof(InstrumentNav),
        new PropertyMetadata(Instrument.Ridge, OnCurrentChanged));

    static readonly (Instrument Which, string Label)[] Items =
    [
        (Instrument.Ridge, "Deep work"),
        (Instrument.Rain, "Sessions"),
        (Instrument.Night, "Sleep"),
        (Instrument.Road, "Running"),
    ];

    /// <summary>The line under the current name. One pixel, and the only indigo up here.</summary>
    const double RuleHeight = 1;

    StackPanel? _items;
    Button? _replay;

    public InstrumentNav()
    {
        DefaultStyleKey = typeof(InstrumentNav);
    }

    /// <summary>Raised when the page should play its entrance again.</summary>
    public event EventHandler? ReplayRequested;

    public Instrument Current
    {
        get => (Instrument)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public static Type PageType(Instrument which) => which switch
    {
        Instrument.Rain => typeof(RainPage),
        Instrument.Night => typeof(NightPage),
        Instrument.Road => typeof(RoadPage),
        _ => typeof(RidgePage),
    };

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_replay is not null)
        {
            _replay.Click -= OnReplayClick;
        }

        _items = GetTemplateChild("PART_Items") as StackPanel;
        _replay = GetTemplateChild("PART_Replay") as Button;

        if (_replay is not null)
        {
            _replay.Click += OnReplayClick;
        }

        BuildItems();
    }

    static void OnCurrentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((InstrumentNav)d).BuildItems();

    static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

    void OnReplayClick(object sender, RoutedEventArgs e) =>
        ReplayRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Built in code rather than as an ItemsControl: four fixed names with two
    /// states between them is less markup than the template plumbing would be.
    /// </summary>
    void BuildItems()
    {
        if (_items is null)
        {
            return;
        }

        _items.Children.Clear();

        foreach (var (which, label) in Items)
        {
            var isCurrent = which == Current;

            var name = new SpacedText
            {
                Text = label,
                Style = (Style)Application.Current.Resources["LabelStyle"],
                Foreground = Resource(isCurrent ? "InkBrush" : "QuietBrush"),
            };

            var rule = new Border
            {
                Height = RuleHeight,
                Margin = new Thickness(0, 5, 0, 0),
                // the living page is marked in indigo; the rest carry no line
                Background = isCurrent ? Resource("AiSoft50Brush") : null,
                IsHitTestVisible = false,
            };

            var stack = new StackPanel { Children = { name, rule } };

            var button = new Button
            {
                Content = stack,
                Style = (Style)Application.Current.Resources["NavLinkStyle"],
                Tag = which,
                IsEnabled = !isCurrent,
            };

            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label);
            button.Click += OnItemClick;

            if (!isCurrent)
            {
                // hover lifts the name to ink. The button template cannot reach
                // the Foreground of a SpacedText inside its own content, so the
                // two states are swapped here instead of in a VisualState.
                button.PointerEntered += (_, _) => name.Foreground = Resource("InkBrush");
                button.PointerExited += (_, _) => name.Foreground = Resource("QuietBrush");
            }

            _items.Children.Add(button);
        }
    }

    void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Instrument which } || which == Current)
        {
            return;
        }

        if (FindFrame() is not { } frame)
        {
            return;
        }

        // no frame transition: the card rise and the instrument drawing itself
        // in are the arrival, and two arrivals at once read as a flinch
        frame.Navigate(PageType(which), null, new SuppressNavigationTransitionInfo());
    }

    Frame? FindFrame()
    {
        DependencyObject? node = this;

        while (node is not null)
        {
            if (node is Frame frame)
            {
                return frame;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return XamlRoot?.Content as Frame;
    }
}
