// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Mdt;

namespace RaidexKeys.Core.Tests;

/// <summary>
/// The arrows from pull to pull (Lars, 2026-09-12): they start and end clear
/// of the two areas they join, and they go around what lies between.
/// </summary>
public sealed class RouteArrowsTests
{
    /// <summary>A pull of one enemy at a point, drawn with a padding of 12 units.</summary>
    private static PullShape Pull(int number, double x, double y)
    {
        var point = new MapPoint(x, y, 6);
        return new PullShape(number, [point], [point], new MapPoint(x, y), 12);
    }

    private static FloorMap Floor(params PullShape[] pulls) => new(1, "", [], pulls);

    /// <summary>Distance from the curve, sampled, to a point.</summary>
    private static double Nearest(RouteArrow arrow, MapPoint to)
    {
        var least = double.MaxValue;
        for (var i = 0; i <= 40; i++)
        {
            var t = i / 40.0;
            var x = ((1 - t) * (1 - t) * arrow.From.X) + (2 * (1 - t) * t * arrow.Control.X) + (t * t * arrow.To.X);
            var y = ((1 - t) * (1 - t) * arrow.From.Y) + (2 * (1 - t) * t * arrow.Control.Y) + (t * t * arrow.To.Y);
            least = Math.Min(least, Math.Sqrt(((x - to.X) * (x - to.X)) + ((y - to.Y) * (y - to.Y))));
        }
        return least;
    }

    private static double Apart(MapPoint a, MapPoint b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    [Fact]
    public void OneArrowPerStepStartingAndEndingClearOfBothAreas()
    {
        var pulls = new[] { Pull(1, 100, 100), Pull(2, 300, 100), Pull(3, 300, 300) };

        var arrows = RouteArrows.Of(Floor(pulls));

        Assert.Equal(2, arrows.Count);
        for (var i = 0; i < arrows.Count; i++)
        {
            // The areas reach 12 units around the enemy, and the arrow keeps 5 more.
            Assert.True(Apart(arrows[i].From, pulls[i].Center) >= 17);
            Assert.True(Apart(arrows[i].To, pulls[i + 1].Center) >= 17);
        }
    }

    [Fact]
    public void ItBendsAroundAPullInTheWay()
    {
        var between = Pull(9, 200, 100);
        var floor = Floor(Pull(1, 100, 100), Pull(2, 300, 100), between);

        var arrow = RouteArrows.Of(floor).Single(a => a.From.X < a.To.X);

        // Straight it would run right through the pull standing in the middle.
        Assert.True(Nearest(arrow, between.Center) > 17, $"only {Nearest(arrow, between.Center):F1} units off");
    }

    [Fact]
    public void APullOnAnotherFloorBreaksTheLine()
    {
        // Pull 2 is not on this floor: nothing is drawn from 1 to 3, since the
        // way between them does not run across this map.
        var arrows = RouteArrows.Of(Floor(Pull(1, 100, 100), Pull(3, 300, 100)));

        Assert.Empty(arrows);
    }
}
