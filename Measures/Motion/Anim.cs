using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Measures.Motion;

/// <summary>
/// The page law in one place: gravity eases in, hands ease out. Every animation
/// in the app is built here so the reduced-motion gate cannot be forgotten -
/// with animations off, the same call lands the final frame instantly.
/// WinUI has no arbitrary cubic-bezier easing function, so the curves are
/// authored as spline key frames, which take a KeySpline directly.
/// </summary>
public static class Anim
{
    static UISettings? _settings;
    static bool? _enabled;

    /// <summary>Gravity: things that fall. cubic-bezier(0.4, 0, 1, 1)</summary>
    public static KeySpline Gravity => Spline(0.4, 0, 1, 1);

    /// <summary>Hands: things a person moves. cubic-bezier(0.22, 1, 0.36, 1)</summary>
    public static KeySpline Hands => Spline(0.22, 1, 0.36, 1);

    /// <summary>The browser default, for colour and opacity settling.</summary>
    public static KeySpline Ease => Spline(0.25, 0.1, 0.25, 1);

    public static KeySpline EaseOut => Spline(0, 0, 0.58, 1);

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

    public static KeySpline Spline(double x1, double y1, double x2, double y2) =>
        new() { ControlPoint1 = new Point(x1, y1), ControlPoint2 = new Point(x2, y2) };

    /// <summary>easeInOutQuad, evaluated directly - the runner interpolates per frame.</summary>
    public static double EaseInOutQuad(double u) =>
        u < 0.5 ? 2 * u * u : 1 - Math.Pow(-2 * u + 2, 2) / 2;

    public static Func<double, double> Linear => u => u;

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
    public static Storyboard AnimateColor(DependencyObject target, string property, Color to, double ms)
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

        animation.KeyFrames.Add(new LinearColorKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(ms),
            Value = to,
        });

        return Run(target, property, animation, null);
    }

    static Storyboard Run(DependencyObject target, string property, Timeline animation, Action? completed)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);

        var board = new Storyboard();
        board.Children.Add(animation);

        if (completed is not null)
        {
            board.Completed += (_, _) => completed();
        }

        board.Begin();
        return board;
    }
}
