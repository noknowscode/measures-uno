using System.Diagnostics;
using Microsoft.UI.Xaml.Media;

namespace Measures.Motion;

/// <summary>
/// A one-shot per-frame loop. Storyboards cover every property the framework
/// animates; this covers the ones it does not - a clip rectangle being widened,
/// a polyline being extended, a dash phase being marched.
/// It detaches the moment it finishes: a held CompositionTarget.Rendering
/// subscription pins the Skia compositor at display refresh for the life of the
/// process, whether or not anything on screen is moving.
/// </summary>
public sealed class Ticker : IDisposable
{
    readonly Stopwatch _clock = new();
    readonly double _durationMs;
    readonly double _delayMs;
    readonly Func<double, double> _ease;
    readonly Action<double> _onFrame;
    readonly Action? _done;

    EventHandler<object>? _handler;

    Ticker(double durationMs, double delayMs, Func<double, double> ease, Action<double> onFrame, Action? done)
    {
        _durationMs = Math.Max(durationMs, 1);
        _delayMs = Math.Max(delayMs, 0);
        _ease = ease;
        _onFrame = onFrame;
        _done = done;
    }

    /// <summary>
    /// Starts a loop that calls <paramref name="onFrame"/> with the eased 0..1
    /// progress each frame. With animations off it lands the final frame at once.
    /// </summary>
    public static Ticker Run(
        double durationMs,
        double delayMs,
        Func<double, double> ease,
        Action<double> onFrame,
        Action? done = null)
    {
        var ticker = new Ticker(durationMs, delayMs, ease, onFrame, done);

        if (!Anim.Enabled)
        {
            onFrame(1);
            done?.Invoke();
            return ticker;
        }

        onFrame(0);
        ticker.Start();

        return ticker;
    }

    public void Dispose() => Detach();

    void Start()
    {
        _clock.Restart();
        _handler = OnRendering;
        CompositionTarget.Rendering += _handler;
    }

    void OnRendering(object? sender, object e)
    {
        var elapsed = _clock.Elapsed.TotalMilliseconds - _delayMs;

        if (elapsed < 0)
        {
            return;
        }

        var u = Math.Min(elapsed / _durationMs, 1);

        _onFrame(_ease(u));

        if (u < 1)
        {
            return;
        }

        Detach();
        _done?.Invoke();
    }

    void Detach()
    {
        _clock.Stop();

        if (_handler is null)
        {
            return;
        }

        CompositionTarget.Rendering -= _handler;
        _handler = null;
    }
}
