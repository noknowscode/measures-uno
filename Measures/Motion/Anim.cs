using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Measures.Motion;

/// <summary>
/// The page law in one place: gravity eases in, hands ease out. There are
/// exactly two easings and every animation in the app is built here, so the
/// vocabulary cannot drift and the reduced-motion gate cannot be forgotten -
/// with animations off, the same call lands the final frame instantly.
///
/// The curves are read from Tokens.xaml, so XAML and code cannot disagree about
/// what "hands" means. WinUI has no arbitrary cubic-bezier easing function, so
/// they are authored as KeySplines, which spline key frames take directly.
/// </summary>
public static class Anim
{
    static UISettings? _settings;
    static bool? _enabled;
    static KeySpline? _hands;
    static KeySpline? _gravity;

    /// <summary>Hands: things a person moves, and everything that settles. cubic-bezier(0.22, 1, 0.36, 1)</summary>
    public static KeySpline EaseHands => _hands ??= Resource("EaseHands", 0.22, 1, 0.36, 1);

    /// <summary>Gravity: things that fall, and presses. cubic-bezier(0.4, 0, 1, 1)</summary>
    public static KeySpline EaseGravity => _gravity ??= Resource("EaseGravity", 0.4, 0, 1, 1);

    public static bool Enabled
    {
        get
        {
            if (_enabled is { } cached)
            {
                return cached;
            }

            try
            {
                _settings ??= new UISettings();
                _enabled = _settings.AnimationsEnabled;
            }
            catch
            {
                _enabled = true;
            }

            return _enabled.Value;
        }
    }

    static KeySpline Spline(double x1, double y1, double x2, double y2) =>
        new() { ControlPoint1 = new Point(x1, y1), ControlPoint2 = new Point(x2, y2) };

    /// <summary>
    /// The curve as the resource dictionary declares it, so the two authorities
    /// stay one. The literal is the fallback for a design surface that has not
    /// merged Tokens.xaml yet - not a second opinion.
    /// </summary>
    static KeySpline Resource(string key, double x1, double y1, double x2, double y2) =>
        Application.Current?.Resources.TryGetValue(key, out var found) == true && found is KeySpline spline
            ? spline
            : Spline(x1, y1, x2, y2);

    /// <summary>
    /// The same curve a KeySpline describes, evaluated in code, for the motion
    /// a Storyboard cannot reach. Time is solved for by bisection: the bezier
    /// gives x and y from a parameter, and easing needs y at a given x.
    /// </summary>
    public static Func<double, double> Curve(KeySpline spline)
    {
        var x1 = spline.ControlPoint1.X;
        var y1 = spline.ControlPoint1.Y;
        var x2 = spline.ControlPoint2.X;
        var y2 = spline.ControlPoint2.Y;

        return u =>
        {
            if (u <= 0)
            {
                return 0;
            }

            if (u >= 1)
            {
                return 1;
            }

            var low = 0d;
            var high = 1d;
            var t = u;

            for (var i = 0; i < 24; i++)
            {
                var x = Bezier(t, x1, x2);

                if (Math.Abs(x - u) < 1e-5)
                {
                    break;
                }

                if (x < u)
                {
                    low = t;
                }
                else
                {
                    high = t;
                }

                t = (low + high) / 2;
            }

            return Bezier(t, y1, y2);
        };
    }

    /// <summary>A unit cubic bezier with anchors at 0 and 1.</summary>
    static double Bezier(double t, double p1, double p2)
    {
        var u = 1 - t;

        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    public static Storyboard Animate(
        DependencyObject target,
        string property,
        double? from,
        double to,
        double ms,
        KeySpline? spline = null,
        double delayMs = 0,
        Action? completed = null)
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            EnableDependentAnimation = true,
            FillBehavior = FillBehavior.HoldEnd,
        };

        if (!Enabled)
        {
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame
            {
                KeyTime = TimeSpan.Zero,
                Value = to,
            });

            return Run(target, property, animation, completed);
        }

        animation.BeginTime = TimeSpan.FromMilliseconds(delayMs);

        if (from is { } start)
        {
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame
            {
                KeyTime = TimeSpan.Zero,
                Value = start,
            });
        }

        var end = TimeSpan.FromMilliseconds(ms);

        if (spline is null)
        {
            animation.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = end, Value = to });
        }
        else
        {
            animation.KeyFrames.Add(new SplineDoubleKeyFrame
            {
                KeyTime = end,
                Value = to,
                KeySpline = spline,
            });
        }

        return Run(target, property, animation, completed);
    }

    /// <summary>
    /// Colour crossfades are linear: SplineColorKeyFrame.KeySpline is not
    /// implemented on Uno, so an eased curve here would be silently dropped.
    /// Over the 300-400 ms these transitions run, the two are indistinguishable.
    /// </summary>
    public static Storyboard AnimateColor(
        DependencyObject target,
        string property,
        Color to,
        double ms,
        double delayMs = 0)
    {
        var animation = new ColorAnimationUsingKeyFrames
        {
            EnableDependentAnimation = true,
            FillBehavior = FillBehavior.HoldEnd,
        };

        if (!Enabled)
        {
            animation.KeyFrames.Add(new DiscreteColorKeyFrame { KeyTime = TimeSpan.Zero, Value = to });
            return Run(target, property, animation, null);
        }

        animation.BeginTime = TimeSpan.FromMilliseconds(delayMs);
        animation.KeyFrames.Add(new LinearColorKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(ms),
            Value = to,
        });

        return Run(target, property, animation, null);
    }

    /// <summary>
    /// Storyboards in flight, held so they cannot be collected. Nothing else
    /// roots them, and one waiting out a BeginTime delay is especially exposed:
    /// it can be collected before it ever starts, which reads as an element
    /// stuck at its "from" value while its undelayed siblings animate fine.
    /// </summary>
    static readonly List<Storyboard> Live = [];

    /// <summary>
    /// The board currently holding each (target, property). A finished HoldEnd
    /// board keeps overriding the local value, so a replay that sets Opacity
    /// to 0 and then animates it up after a delay would show the held 1 for
    /// the whole delay - the fills and marks never left. The previous board
    /// on the same property is stopped first, which hands the property back
    /// to the local value the caller just set.
    /// </summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, Dictionary<string, Storyboard>> Holding = new();

    static Storyboard Run(DependencyObject target, string property, Timeline animation, Action? completed)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);

        var board = new Storyboard();
        board.Children.Add(animation);

        board.Completed += (_, _) =>
        {
            Live.Remove(board);
            completed?.Invoke();
        };

        // a stopped board never raises Completed, so sweep on the way in
        Live.RemoveAll(b => b.GetCurrentState() == ClockState.Stopped);
        Live.Add(board);

        var held = Holding.GetOrCreateValue(target);

        if (held.TryGetValue(property, out var previous))
        {
            previous.Stop();
            Live.Remove(previous);
        }

        held[property] = board;

        board.Begin();
        return board;
    }
}
