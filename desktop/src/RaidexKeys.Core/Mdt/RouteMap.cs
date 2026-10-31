// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Mdt;

/// <summary>A point on MDT's map in its units, y downwards; with the radius an enemy is drawn at.</summary>
public readonly record struct MapPoint(double X, double Y, double Radius = 0);

/// <summary>A pull on the map: its enemies, the outline around them and where its number goes.</summary>
/// <param name="Hull">The convex hull of the enemies, in order around; one or two points for small pulls.</param>
/// <param name="Padding">How far the outline keeps off the hull: the largest enemy's radius, and a little air.</param>
public sealed record PullShape(int Number, IReadOnlyList<MapPoint> Enemies, IReadOnlyList<MapPoint> Hull, MapPoint Center, double Padding);

/// <summary>One floor of a dungeon with a route drawn on it.</summary>
/// <param name="TileDir">The folder of the floor's 150 tiles in the installed MDT.</param>
/// <param name="Others">Every enemy of the floor the route leaves standing.</param>
public sealed record FloorMap(int Sublevel, string TileDir, IReadOnlyList<MapPoint> Others, IReadOnlyList<PullShape> Pulls);

/// <summary>
/// A route on MDT's map. The map is 840 × 555 units from the top left,
/// covered by 15 × 10 square tiles "sublevel_n.png", n counted row by row
/// from 1 - MDT's layout, read from the player's installation.
/// </summary>
public static class RouteMap
{
    public const double Width = 840;
    public const double Height = 555;
    public const int Columns = 15;
    public const int Rows = 10;

    /// <summary>The tile in row <paramref name="row"/>, column <paramref name="column"/>, both from 0.</summary>
    public static string TilePath(string tileDir, int sublevel, int row, int column) =>
        Path.Combine(tileDir, $"{sublevel}_{row * Columns + column + 1}.png");

    /// <summary>
    /// The floors the route's pulls stand on, each with its tiles; none when
    /// the installed MDT has no tiles for the dungeon.
    /// </summary>
    public static IReadOnlyList<FloorMap> Of(MdtRoute route, MdtDungeon dungeon)
    {
        var pulled = new HashSet<(int Enemy, int Clone)>();
        var byFloor = new Dictionary<int, List<PullShape>>();
        for (var i = 0; i < route.Pulls.Count; i++)
        {
            var points = new Dictionary<int, List<MapPoint>>();
            foreach (var (enemyIndex, clones) in route.Pulls[i].Clones)
            {
                if (!dungeon.Enemies.TryGetValue(enemyIndex, out var enemy))
                {
                    continue;
                }
                foreach (var cloneIndex in clones)
                {
                    if (enemy.Clones.TryGetValue(cloneIndex, out var clone))
                    {
                        pulled.Add((enemyIndex, cloneIndex));
                        points.TryAdd(clone.Sublevel, []);
                        points[clone.Sublevel].Add(PointOf(dungeon, enemy, clone));
                    }
                }
            }
            // A pull across floors shows on each, with the same number.
            foreach (var (floor, list) in points)
            {
                byFloor.TryAdd(floor, []);
                byFloor[floor].Add(ShapeOf(i + 1, list));
            }
        }

        return byFloor
            .Where(f => dungeon.TileDirs.ContainsKey(f.Key))
            .OrderBy(f => f.Key)
            .Select(f => new FloorMap(f.Key, dungeon.TileDirs[f.Key],
                dungeon.Enemies
                    .SelectMany(e => e.Value.Clones
                        .Where(c => c.Value.Sublevel == f.Key && !pulled.Contains((e.Key, c.Key)))
                        .Select(c => PointOf(dungeon, e.Value, c.Value)))
                    .ToList(),
                f.Value))
            .ToList();
    }

    /// <summary>Where a clone stands, y turned downwards; its radius as MDT sizes enemies (bosses larger).</summary>
    private static MapPoint PointOf(MdtDungeon dungeon, MdtEnemy enemy, MdtClone clone) =>
        new(clone.X, -clone.Y, 6 * enemy.Scale * clone.Scale * (enemy.IsBoss ? 1.7 : 1) * dungeon.ScaleMultiplier);

    private static PullShape ShapeOf(int number, List<MapPoint> points)
    {
        var hull = ConvexHull(points);
        var center = new MapPoint(points.Average(p => p.X), points.Average(p => p.Y));
        // Enough room that the area shows around the circles it holds, not
        // just between them - it is the shape that says which pack is one pull.
        return new PullShape(number, points, hull, center, points.Max(p => p.Radius) + 6);
    }

    /// <summary>The convex hull, counter-clockwise (Andrew's monotone chain); fewer than three points stay as they are.</summary>
    public static IReadOnlyList<MapPoint> ConvexHull(IReadOnlyList<MapPoint> points)
    {
        var sorted = points.DistinctBy(p => (p.X, p.Y)).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (sorted.Count < 3)
        {
            return sorted;
        }
        static double Cross(MapPoint o, MapPoint a, MapPoint b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var hull = new List<MapPoint>();
        foreach (var pass in new[] { sorted, Enumerable.Reverse(sorted).ToList() })
        {
            var start = hull.Count;
            foreach (var p in pass)
            {
                while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull;
    }
}
