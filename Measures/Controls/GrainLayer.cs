using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Measures.Controls;

/// <summary>
/// The material the field is made of. Grain is not an effect - it never
/// animates and never responds - and it belongs to the page, not to the cards:
/// the cards are smooth uncoated paper laid on grained plaster, and the two
/// materials reading differently is the point.
///
/// The prototype does this with a shared feTurbulence filter. Per-element SVG
/// filters do not port, so the noise is a precomputed tileable asset instead -
/// generated once, deterministic, and free at runtime.
///
/// It is laid out as a grid of Image tiles rather than one tiling brush because
/// WinUI's TileBrush has no TileMode and Uno inherits that gap (verified against
/// Uno.UI 6.6: the type does not exist in the assembly). The tiles are static,
/// share one decoded bitmap, and cost nothing after first layout.
/// </summary>
public sealed partial class GrainLayer : Control
{
    /// <summary>Matches the asset exactly, so a tile is never resampled.</summary>
    const double TileSize = 256;

    /// <summary>A ceiling on a pathological size; 400 tiles covers a 5K display.</summary>
    const int MaxTiles = 400;

    static BitmapImage? _shared;
    static bool _reported;

    readonly List<Image> _tiles = [];

    Canvas? _host;

    public GrainLayer()
    {
        DefaultStyleKey = typeof(GrainLayer);

        IsHitTestVisible = false;
        SizeChanged += (_, _) => Fill();
    }

    /// <summary>
    /// Touches the asset at startup so a missing or unreadable tile is reported
    /// once, loudly, in the log - rather than showing up months later as "the
    /// dark theme bands".
    /// </summary>
    public static void EnsureAsset() => _ = Source;

    static BitmapImage Source
    {
        get
        {
            if (_shared is not null)
            {
                return _shared;
            }

            _shared = new BitmapImage(new Uri("ms-appx:///Assets/Textures/grain.png"))
            {
                DecodePixelWidth = (int)TileSize,
                DecodePixelHeight = (int)TileSize,
            };

            _shared.ImageFailed += (_, e) =>
            {
                if (_reported)
                {
                    return;
                }

                _reported = true;
                Debug.WriteLine($"GrainLayer: Assets/Textures/grain.png did not load ({e.ErrorMessage}). Washes will band.");
            };

            return _shared;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _host = GetTemplateChild("PART_Tiles") as Canvas;
        Fill();
    }

    void Fill()
    {
        if (_host is null)
        {
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        // the tiles overhang the last row and column, so the layer clips itself
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };

        var columns = (int)Math.Ceiling(width / TileSize);
        var rows = (int)Math.Ceiling(height / TileSize);
        var needed = Math.Min(columns * rows, MaxTiles);

        while (_tiles.Count < needed)
        {
            var tile = new Image
            {
                Source = Source,
                Width = TileSize,
                Height = TileSize,
                Stretch = Stretch.None,
            };

            _host.Children.Add(tile);
            _tiles.Add(tile);
        }

        while (_tiles.Count > needed)
        {
            _host.Children.Remove(_tiles[^1]);
            _tiles.RemoveAt(_tiles.Count - 1);
        }

        for (var i = 0; i < needed; i++)
        {
            Canvas.SetLeft(_tiles[i], i % columns * TileSize);
            Canvas.SetTop(_tiles[i], i / columns * TileSize);
        }
    }
}
