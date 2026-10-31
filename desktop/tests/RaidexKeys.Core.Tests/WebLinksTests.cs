// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Web;

namespace RaidexKeys.Core.Tests;

/// <summary>The link schemes, as checked against the sites on 2026-09-11.</summary>
public sealed class WebLinksTests
{
    [Fact]
    public void LinksFollowTheSitesSchemes()
    {
        // Wowhead: the dungeon's zone, found through the game's tables.
        Assert.Equal("https://www.wowhead.com/de/zone=15093", WebLinks.Wowhead(503, "Ara-Kara", german: true));
        Assert.Equal("https://www.wowhead.com/zone=15093", WebLinks.Wowhead(503, "Ara-Kara", german: false));
        Assert.StartsWith("https://www.wowhead.com/search?q=", WebLinks.Wowhead(-1, "Nowhere", german: false));

        // Blizzard's slugs: lower case, no apostrophes, hyphens for spaces, umlauts kept.
        Assert.Equal("drakthul", WebLinks.RealmSlug("Drak'thul"));
        Assert.Equal("tarren-mill", WebLinks.RealmSlug("Tarren Mill"));
        Assert.Equal("festung-der-stürme", WebLinks.RealmSlug("Festung der Stürme"));

        Assert.Equal("https://raider.io/characters/eu/DieewigeWacht/Grimwald", WebLinks.RaiderIo("EU", "DieewigeWacht", "Grimwald"));
        Assert.Equal("https://raider.io/characters/eu/tarren-mill/Grimwald", WebLinks.RaiderIo("eu", "tarren-mill", "Grimwald"));

        // Raider.IO's own slugs; Seat of the Triumvirate is 583 in the game, 239 at Raider.IO.
        Assert.Equal("https://raider.io/mythic-plus-rankings/season-mn-2/altar-of-fangs/world/leaderboards-strict",
            WebLinks.RaiderIoLeaderboard(588));
        Assert.Equal("https://raider.io/mythic-plus-rankings/season-mn-1/seat-of-the-triumvirate/world/leaderboards-strict",
            WebLinks.RaiderIoLeaderboard(583));
        Assert.Null(WebLinks.RaiderIoLeaderboard(-1));

        Assert.StartsWith("https://render.worldofwarcraft.com/", WebLinks.DungeonImage(503));
    }
}
