using Microsoft.UI.Xaml.Media;

namespace Measures.Motion;

/// <summary>
/// One 30 Hz timer drives every breathing element in the app.
/// A RepeatBehavior="Forever" storyboard pins the Skia compositor at display
/// refresh for the life of the process (~30% of a core per animation, whether
/// or not the target is visible); a timer that writes a transform produces
/// exactly one frame per tick and stops dead when the last subscriber leaves.
/// </summary>
public static class BreathClock
{
    const double PeriodMs = 5000;
    const double TickMs = 33;

    static readonly List<Action<double>> Subscribers = [];
    static DispatcherTimer? _timer;
    static double _elapsedMs;

    /// <summary>The eased 0..1 the keyframe describes: 0 at the ends, 1 at the middle.</summary>
    static double Swell => (1 - Math.Cos(_elapsedMs / PeriodMs * 2 * Math.PI)) / 2;

    public static IDisposable Subscribe(Action<double> onTick)
    {
        if (!Anim.Enabled)
        {
            onTick(0);
            return new Subscription(null);
        }

        Subscribers.Add(onTick);

        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        return new Subscription(onTick);
    }

    /// <summary>The breathing bead: swells and fades together.</summary>
    public static IDisposable Breathe(UIElement element, ScaleTransform scale) =>
        Subscribe(swell =>
        {
            var s = 1 + 0.45 * swell;
            scale.ScaleX = s;
            scale.ScaleY = s;
            element.Opacity = 1 - 0.45 * swell;
        });

    /// <summary>
    /// The breathing mark: swells between two scale factors, no fade.
    /// Callers pass scales, not radii, so the dot's laid-out size stays fixed.
    /// </summary>
    public static IDisposable BreatheRadius(ScaleTransform scale, double from, double to) =>
        Subscribe(swell =>
        {
            var s = from + (to - from) * swell;
            scale.ScaleX = s;
            scale.ScaleY = s;
        });

    static void OnTick(object? sender, object e)
    {
        _elapsedMs = (_elapsedMs + TickMs) % PeriodMs;

        var swell = Swell;

        for (var i = Subscribers.Count - 1; i >= 0; i--)
        {
            Subscribers[i](swell);
        }
    }

    static void Unsubscribe(Action<double> onTick)
    {
        Subscribers.Remove(onTick);

        if (Subscribers.Count != 0 || _timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    sealed class Subscription(Action<double>? onTick) : IDisposable
    {
        Action<double>? _onTick = onTick;

        public void Dispose()
        {
            if (_onTick is { } handler)
            {
                Unsubscribe(handler);
                _onTick = null;
            }
        }
    }
}
