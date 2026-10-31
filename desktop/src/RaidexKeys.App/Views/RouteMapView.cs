// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RaidexKeys.Core.Mdt;
using SkiaSharp;

namespace RaidexKeys.App.Views;

/// <summary>
/// A route on the dungeon's map: MDT's tiles from the player's installation,
/// the enemies left standing in grey, each pull as a coloured area with its
/// enemies and its number. Drawn here, in our own colours - nothing of MDT's
/// drawing code.
/// </summary>
public sealed class RouteMapView : Control
{
    public static readonly StyledProperty<FloorMap?> FloorProperty =
        AvaloniaProperty.Register<RouteMapView, FloorMap?>(nameof(Floor));

    /// <summary>Whether the way round is drawn: 1 → 2 → 3 as curved arrows between the pulls.</summary>
    public static readonly StyledProperty<bool> ArrowsProperty =
        AvaloniaProperty.Register<RouteMapView, bool>(nameof(Arrows));

    // Pull colours: bright on the parchment, apart from each other; Glow first.
    private static readonly Color[] Palette =
    [
        Color.Parse("#A78BFF"), Color.Parse("#2EC4B6"), Color.Parse("#FF9F1C"), Color.Parse("#F15BB5"),
        Color.Parse("#4CC95E"), Color.Parse("#3A86FF"), Color.Parse("#E63946"), Color.Parse("#FFD60A"),
    ];

    /// <summary>The pull numbers are in the monospace font; a new choice in the settings redraws them.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Application.Current is { } app)
        {
            app.ResourcesChanged += Redraw;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Application.Current is { } app)
        {
            app.ResourcesChanged -= Redraw;
        }
    }

    private void Redraw(object? sender, ResourcesChangedEventArgs e) => InvalidateVisual();

    static RouteMapView()
    {
        AffectsMeasure<RouteMapView>(FloorProperty);
        AffectsRender<RouteMapView>(FloorProperty, ArrowsProperty);
    }

    /// <summary>A pull's colour on the map, "#RRGGBB" - the route's text list uses the same.</summary>
    public static string PullColor(int number) => Palette[(number - 1) % Palette.Length].ToString();

    public FloorMap? Floor
    {
        get => GetValue(FloorProperty);
        set => SetValue(FloorProperty, value);
    }

    public bool Arrows
    {
        get => GetValue(ArrowsProperty);
        set => SetValue(ArrowsProperty, value);
    }

    /// <summary>As large as there is room, in the map's proportions: a row's width, or all of a window.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? RouteMap.Width : availableSize.Width;
        if (!double.IsInfinity(availableSize.Height))
        {
            width = Math.Min(width, availableSize.Height * RouteMap.Width / RouteMap.Height);
        }
        return new Size(width, width * RouteMap.Height / RouteMap.Width);
    }

    public override void Render(DrawingContext context)
    {
        if (Floor is not { } floor)
        {
            return;
        }
        var scale = Bounds.Width / RouteMap.Width;
        var area = new Rect(Bounds.Size);
        using var clip = context.PushClip(new RoundedRect(area, 6));

        if (MapImages.Get(floor.TileDir, floor.Sublevel, InvalidateVisual) is { } map)
        {
            // The tiles cover 10 rows of 56 units; the map ends at 555.
            var source = new Rect(0, 0, map.Size.Width, map.Size.Width * RouteMap.Height / RouteMap.Width);
            context.DrawImage(map, source, area);
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(Themes.AppThemes.Current.Colors["RowLine"]), area);
        }

        var standing = new SolidColorBrush(Color.FromArgb(150, 40, 36, 30));
        foreach (var p in floor.Others)
        {
            context.DrawEllipse(standing, null, At(p, scale), p.Radius * scale * 0.7, p.Radius * scale * 0.7);
        }

        var outline = new Pen(new SolidColorBrush(Color.FromArgb(200, 20, 16, 12)), Math.Max(1, scale));
        foreach (var pull in floor.Pulls)
        {
            var color = Palette[(pull.Number - 1) % Palette.Length];
            DrawArea(context, pull, scale, color);
            var fill = new SolidColorBrush(color);
            foreach (var p in pull.Enemies)
            {
                context.DrawEllipse(fill, outline, At(p, scale), p.Radius * scale, p.Radius * scale);
            }
        }
        if (Arrows)
        {
            DrawArrows(context, floor, scale);
        }
        var placed = new List<Point>();
        foreach (var pull in floor.Pulls)
        {
            DrawNumber(context, pull, scale, Palette[(pull.Number - 1) % Palette.Length], placed);
        }
    }

    private static Point At(MapPoint p, double scale) => new(p.X * scale, p.Y * scale);

    /// <summary>
    /// The area a pull covers: the hull grown by the padding on every side,
    /// filled in the pull's colour and drawn round with an edge of its own.
    /// </summary>
    /// <remarks>
    /// The fill alone vanished over a real dungeon map - light, busy artwork
    /// under a third of a colour leaves the enemies looking like loose circles
    /// (Lars, 2026-09-12). The edge is what makes it one shape on any ground,
    /// so the outline of the widened path is taken and filled and stroked in
    /// one go, rather than painting a wide translucent stroke and hoping.
    /// </remarks>
    private static void DrawArea(DrawingContext context, PullShape pull, double scale, Color color)
    {
        var body = new SolidColorBrush(Color.FromArgb(115, color.R, color.G, color.B));
        var edge = new Pen(new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B)),
            Math.Max(1.4, 1.6 * scale), lineJoin: PenLineJoin.Round);
        if (pull.Hull.Count == 1)
        {
            var r = pull.Padding * scale;
            context.DrawEllipse(body, edge, At(pull.Hull[0], scale), r, r);
            return;
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(At(pull.Hull[0], scale), isFilled: pull.Hull.Count > 2);
            foreach (var p in pull.Hull.Skip(1))
            {
                g.LineTo(At(p, scale));
            }
            g.EndFigure(isClosed: true);
        }
        // The hull walked with a pen as wide as the padding is round; its own
        // outline is that rounded area as a shape, which can be filled.
        var wide = new Pen(Brushes.Black, 2 * pull.Padding * scale,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(body, edge, geometry.GetWidenedGeometry(wide));
    }

    /// <summary>
    /// The way round: an arrow from each pull to the next, curved around
    /// whatever lies between (Core/Mdt/RouteArrows). Light, in a dark casing,
    /// so it reads over parchment as well as over a dark floor.
    /// </summary>
    private void DrawArrows(DrawingContext context, FloorMap floor, double scale)
    {
        if (_arrows?.Floor != floor)
        {
            _arrows = (floor, RouteArrows.Of(floor));
        }
        var casing = new Pen(new SolidColorBrush(Color.FromArgb(165, 10, 12, 24)), Math.Max(3.6, 3.9 * scale),
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var line = new Pen(new SolidColorBrush(Color.FromArgb(240, 238, 242, 255)), Math.Max(1.5, 1.6 * scale),
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        foreach (var arrow in _arrows.Value.Arrows)
        {
            var path = new StreamGeometry();
            using (var g = path.Open())
            {
                g.BeginFigure(At(arrow.From, scale), isFilled: false);
                g.QuadraticBezierTo(At(arrow.Control, scale), At(arrow.To, scale));
                g.EndFigure(isClosed: false);
            }
            context.DrawGeometry(null, casing, path);
            context.DrawGeometry(null, line, path);
            DrawHead(context, arrow, scale, casing, line);
        }
    }

    /// <summary>The head at the arrow's end, pointing the way the curve leaves it.</summary>
    private static void DrawHead(DrawingContext context, RouteArrow arrow, double scale, Pen casing, Pen line)
    {
        var tip = At(arrow.To, scale);
        var back = At(arrow.Control, scale);
        var dx = tip.X - back.X;
        var dy = tip.Y - back.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.01)
        {
            return;
        }
        var ux = dx / length;
        var uy = dy / length;
        var size = Math.Max(7, 8 * scale);
        var foot = new Point(tip.X - (ux * size), tip.Y - (uy * size));
        var wing = size * 0.5;
        var head = new StreamGeometry();
        using (var g = head.Open())
        {
            g.BeginFigure(tip, isFilled: true);
            g.LineTo(new Point(foot.X - (uy * wing), foot.Y + (ux * wing)));
            g.LineTo(new Point(foot.X + (uy * wing), foot.Y - (ux * wing)));
            g.EndFigure(isClosed: true);
        }
        // The dark halo first and the light head over it: stroking the small
        // triangle with the line's own casing would swallow it.
        var halo = new Pen(casing.Brush, Math.Max(2, casing.Thickness - line.Thickness), lineJoin: PenLineJoin.Round);
        context.DrawGeometry(casing.Brush, halo, head);
        context.DrawGeometry(line.Brush, null, head);
    }

    /// <summary>The arrows of the floor last drawn: the same ones until the route or the floor changes.</summary>
    private (FloorMap Floor, IReadOnlyList<RouteArrow> Arrows)? _arrows;

    /// <summary>The pull's number in a disc at its centre - moved aside when an earlier one sits there.</summary>
    private static void DrawNumber(DrawingContext context, PullShape pull, double scale, Color color, List<Point> placed)
    {
        var radius = Math.Max(9, 8 * scale);
        var center = At(pull.Center, scale);
        // A pull inside another's ring (one enemy amid many) has its centre on the other's: step around it.
        for (var step = 0; step < 8 && placed.Any(p => Point.Distance(p, center) < radius * 1.9); step++)
        {
            var angle = Math.PI / 2 + step * Math.PI / 4;
            center = At(pull.Center, scale) + new Point(Math.Cos(angle), Math.Sin(angle)) * radius * 2;
        }
        placed.Add(center);
        context.DrawEllipse(new SolidColorBrush(color), new Pen(new SolidColorBrush(Themes.AppThemes.Current.Colors["Sidebar"]), 1.5), center, radius, radius);
        var text = new FormattedText(pull.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Themes.MonoFonts.Current, FontStyle.Normal, FontWeight.Bold),
            radius * 1.1, new SolidColorBrush(Themes.AppThemes.Current.Colors["Sidebar"]));
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    /// <summary>
    /// A floor's 150 tiles stitched into one image, once per floor while the
    /// app runs - in the background, so a row opens at once and the map
    /// appears when it is there.
    /// </summary>
    private static class MapImages
    {
        // The tiles' own resolution, 15 × 128: sharp in a large window too.
        private const int Width = 1920;

        private static readonly ConcurrentDictionary<(string Dir, int Sublevel), Lazy<Task<Bitmap?>>> Cache = new();

        public static Bitmap? Get(string dir, int sublevel, Action loaded)
        {
            var task = Cache.GetOrAdd((dir, sublevel), key => new Lazy<Task<Bitmap?>>(() => Task.Run(() => Stitch(key.Dir, key.Sublevel)))).Value;
            if (task.IsCompletedSuccessfully)
            {
                return task.Result;
            }
            task.ContinueWith(_ => Dispatcher.UIThread.Post(loaded), TaskScheduler.Default);
            return null;
        }

        private static Bitmap? Stitch(string dir, int sublevel)
        {
            var tile = (float)Width / RouteMap.Columns;
            using var surface = SKSurface.Create(new SKImageInfo(Width, (int)Math.Ceiling(tile * RouteMap.Rows)));
            if (surface is null)
            {
                return null;
            }
            var canvas = surface.Canvas;
            var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
            var any = false;
            for (var row = 0; row < RouteMap.Rows; row++)
            {
                for (var column = 0; column < RouteMap.Columns; column++)
                {
                    using var image = SKBitmap.Decode(RouteMap.TilePath(dir, sublevel, row, column));
                    if (image is null)
                    {
                        continue;
                    }
                    any = true;
                    // A hair larger than the tile, so no seam shows between neighbours.
                    using var tileImage = SKImage.FromBitmap(image);
                    canvas.DrawImage(tileImage, new SKRect(column * tile, row * tile, (column + 1) * tile + 0.5f, (row + 1) * tile + 0.5f), sampling);
                }
            }
            if (!any)
            {
                return null;
            }
            using var snapshot = surface.Snapshot();
            using var png = snapshot.Encode(SKEncodedImageFormat.Png, 90);
            return new Bitmap(png.AsStream());
        }
    }
}
