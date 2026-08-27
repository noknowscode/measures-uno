using Measures.Motion;
using Measures.Theming;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Measures.Controls;

/// <summary>
/// The one line each card speaks with. It owns the 140 ms fade-swap, the
/// stale-write token that keeps a slow swap from overwriting a fast one, and
/// the invitation: the caption teaches its gesture once, then summarises.
///
/// A live reading signals by weight of colour - quiet taupe up to full charcoal
/// - never by hue. Readings never resize the card, so the text never wraps,
/// never grows, and a line too long for the box ellipses instead.
/// </summary>
public sealed partial class CaptionPresenter : Control
{
    const double SwapMs = 140;

    readonly SolidColorBrush _brush = new();

    TextBlock? _text;
    int _token;
    bool _live;

    public CaptionPresenter()
    {
        DefaultStyleKey = typeof(CaptionPresenter);

        _brush.Color = Palette.Get(Palette.Token.Quiet);
        Palette.Recoloured += OnRecoloured;
        Unloaded += (_, _) => Palette.Recoloured -= OnRecoloured;
    }

    /// <summary>True once the reader has performed this card's gesture.</summary>
    public bool Invited { get; private set; }

    public string CurrentText { get; private set; } = string.Empty;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _text = GetTemplateChild("PART_Text") as TextBlock;

        if (_text is null)
        {
            return;
        }

        _text.Foreground = _brush;
        _text.Text = CurrentText;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CaptionAutomationPeer(this);

    /// <summary>Marks the gesture as learned; the default line changes from here on.</summary>
    public void Invite() => Invited = true;

    /// <summary>A committed reading: dips, swaps, comes back.</summary>
    public void SetReading(string text, bool live) => Swap(text, live, immediate: false);

    /// <summary>A hover preview: no dip, because the pointer is already moving.</summary>
    public void SetPreview(string text, bool live) => Swap(text, live, immediate: true);

    public void SetDefault(string text) => Swap(text, live: false, immediate: false);

    void Swap(string text, bool live, bool immediate)
    {
        var token = ++_token;

        SetLive(live);

        if (_text is null)
        {
            CurrentText = text;
            return;
        }

        if (immediate || !Anim.Enabled)
        {
            CurrentText = text;
            _text.Text = text;
            _text.Opacity = 1;
            AnnounceChange();
            return;
        }

        Anim.Animate(_text, "Opacity", null, 0, SwapMs, Anim.EaseHands, completed: () =>
        {
            if (token != _token)
            {
                return;
            }

            CurrentText = text;
            _text.Text = text;
            Anim.Animate(_text, "Opacity", null, 1, SwapMs, Anim.EaseHands);
            AnnounceChange();
        });
    }

    void SetLive(bool live)
    {
        if (_live == live)
        {
            return;
        }

        _live = live;

        Anim.AnimateColor(
            _brush,
            "Color",
            Palette.Get(live ? Palette.Token.Ink : Palette.Token.Quiet),
            400);
    }

    void OnRecoloured(object? sender, EventArgs e) =>
        _brush.Color = Palette.Get(_live ? Palette.Token.Ink : Palette.Token.Quiet);

    void AnnounceChange()
    {
        if (FrameworkElementAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    sealed class CaptionAutomationPeer(CaptionPresenter owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetNameCore() => ((CaptionPresenter)Owner).CurrentText;
    }
}
