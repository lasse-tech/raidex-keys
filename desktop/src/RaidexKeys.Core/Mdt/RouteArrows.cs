// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Mdt;

/// <summary>
/// One arrow from a pull to the next, as a quadratic Bézier in map units:
/// it starts and ends outside the two areas it joins.
/// </summary>
public sealed record RouteArrow(MapPoint From, MapPoint Control, MapPoint To);

/// <summary>
/// The way round a floor, pull by pull: 1 → 2 → 3 … as curved arrows between
/// the coloured areas (Lars, 2026-09-12).
/// </summary>
/// <remarks>
/// The arrow is bent away from whatever lies between: several bends are tried
/// and the one that runs into the fewest other pulls wins, the straight line
/// counting first when nothing is in the way. On a crowded floor there is not
/// always a way round - then the least bad one is drawn.
/// </remarks>
public static class RouteArrows
{
    /// <summary>How far an arrow tries to keep off an area, in map units.</summary>
    private const double Gap = 5;

    /// <summary>
    /// The bends tried, as a share of the distance between the two pulls. The
    /// smallest is not zero: an arrow that bows a little reads as a way walked,
    /// a straight one as a measurement (Lars, 2026-09-12).
    /// </summary>
    private static readonly double[] Bends = [0.10, -0.10, 0.20, -0.20, 0.32, -0.32, 0.46, -0.46, 0.64, -0.64];

    private const int Samples = 28;

    /// <summary>An arrow between every two pulls that follow one another on this floor.</summary>
    public static IReadOnlyList<RouteArrow> Of(FloorMap floor)
    {
        var pulls = floor.Pulls.OrderBy(p => p.Number).ToList();
        var arrows = new List<RouteArrow>();
        for (var i = 0; i + 1 < pulls.Count; i++)
        {
            // A pull the route takes on another floor breaks the line: the way
            // between them runs up some stairs, not across this map.
            if (pulls[i + 1].Number == pulls[i].Number + 1 && Between(pulls[i], pulls[i + 1], pulls) is { } arrow)
            {
                arrows.Add(arrow);
            }
        }
        return arrows;
    }

    private static RouteArrow? Between(PullShape from, PullShape to, IReadOnlyList<PullShape> all)
    {
        var a = Of(from.Center);
        var b = Of(to.Center);
        var span = b - a;
        var length = span.Length;
        if (length < 1)
        {
            return null;
        }
        var normal = new Vec(-span.Y, span.X) / length;

        RouteArrow? best = null;
        var bestCost = double.MaxValue;
        foreach (var bend in Bends)
        {
            var control = ((a + b) / 2) + (normal * (bend * length));
            if (Trim(a, control, b, from, to) is not { } cut || cut.End - cut.Start < 0.08)
            {
                continue;
            }
            var (p0, p1, p2) = Part(a, control, b, cut.Start, cut.End);
            // A wider bend than the gentle one costs: of two ways round
            // something, the shorter one reads as the way you walk.
            var cost = Cost(p0, p1, p2, from, to, all) + ((Math.Abs(bend) - Bends[0]) * 8);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = new RouteArrow(Point(p0), Point(p1), Point(p2));
            }
        }
        return best;
    }

    /// <summary>
    /// Where the curve leaves the first area and where it reaches the second,
    /// as the two parameters; null when it never gets clear of either.
    /// </summary>
    private static (double Start, double End)? Trim(Vec a, Vec control, Vec b, PullShape from, PullShape to)
    {
        double? start = null;
        double? end = null;
        for (var i = 0; i <= Samples; i++)
        {
            var t = (double)i / Samples;
            var p = At(a, control, b, t);
            if (start is null && Distance(p, from) >= from.Padding + Gap)
            {
                start = t;
            }
            if (start is not null && Distance(p, to) >= to.Padding + Gap)
            {
                end = t;
            }
        }
        return start is { } s && end is { } e && e > s ? (s, e) : null;
    }

    /// <summary>How far the arrow reaches into the areas it is not joining, plus what it does off the map.</summary>
    private static double Cost(Vec p0, Vec p1, Vec p2, PullShape from, PullShape to, IReadOnlyList<PullShape> all)
    {
        var cost = 0.0;
        for (var i = 0; i <= Samples; i++)
        {
            var p = At(p0, p1, p2, (double)i / Samples);
            foreach (var other in all)
            {
                if (other.Number == from.Number || other.Number == to.Number)
                {
                    continue;
                }
                cost += Math.Max(0, other.Padding + Gap - Distance(p, other));
            }
            if (p.X < 0 || p.Y < 0 || p.X > RouteMap.Width || p.Y > RouteMap.Height)
            {
                cost += 40;
            }
        }
        return cost;
    }

    /// <summary>The piece of the curve between two parameters, as a curve of its own (the blossom of the quadratic).</summary>
    private static (Vec P0, Vec P1, Vec P2) Part(Vec p0, Vec p1, Vec p2, double start, double end)
    {
        Vec Blossom(double u, double v) =>
            (p0 * ((1 - u) * (1 - v))) + (p1 * (((1 - u) * v) + (u * (1 - v)))) + (p2 * (u * v));
        return (Blossom(start, start), Blossom(start, end), Blossom(end, end));
    }

    private static Vec At(Vec p0, Vec p1, Vec p2, double t) =>
        (p0 * ((1 - t) * (1 - t))) + (p1 * (2 * (1 - t) * t)) + (p2 * (t * t));

    /// <summary>How far a point lies from a pull's hull - 0 inside it; the drawn area reaches its padding beyond.</summary>
    private static double Distance(Vec p, PullShape pull)
    {
        var hull = pull.Hull;
        if (hull.Count == 1)
        {
            return (p - Of(hull[0])).Length;
        }
        if (hull.Count == 2)
        {
            return ToSegment(p, Of(hull[0]), Of(hull[1]));
        }
        var inside = false;
        var least = double.MaxValue;
        for (var i = 0; i < hull.Count; i++)
        {
            var u = Of(hull[i]);
            var v = Of(hull[(i + 1) % hull.Count]);
            least = Math.Min(least, ToSegment(p, u, v));
            if (u.Y > p.Y != v.Y > p.Y && p.X < (((v.X - u.X) * (p.Y - u.Y)) / (v.Y - u.Y)) + u.X)
            {
                inside = !inside;
            }
        }
        return inside ? 0 : least;
    }

    private static double ToSegment(Vec p, Vec a, Vec b)
    {
        var span = b - a;
        var square = (span.X * span.X) + (span.Y * span.Y);
        if (square < 1e-9)
        {
            return (p - a).Length;
        }
        var t = Math.Clamp((((p.X - a.X) * span.X) + ((p.Y - a.Y) * span.Y)) / square, 0, 1);
        return (p - (a + (span * t))).Length;
    }

    private static Vec Of(MapPoint p) => new(p.X, p.Y);

    private static MapPoint Point(Vec v) => new(v.X, v.Y);

    private readonly record struct Vec(double X, double Y)
    {
        public double Length => Math.Sqrt((X * X) + (Y * Y));

        public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec operator *(Vec a, double f) => new(a.X * f, a.Y * f);
        public static Vec operator /(Vec a, double f) => new(a.X / f, a.Y / f);
    }
}
