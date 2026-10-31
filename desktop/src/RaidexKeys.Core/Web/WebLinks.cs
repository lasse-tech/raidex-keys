// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text.RegularExpressions;

namespace RaidexKeys.Core.Web;

/// <summary>
/// Links to a dungeon on Wowhead and to a character on Raider.IO. Schemes as
/// checked on 2026-09-11:
/// <list type="bullet">
/// <item>www.wowhead.com/[de/]zone=15093 - redirects to the zone's page in that language</item>
/// <item>raider.io/characters/eu/tarren-mill/Name - takes the realm as slug, name or name without
/// spaces, and links on to the character's armory page</item>
/// <item>raider.io/mythic-plus-rankings/season-mn-2/altar-of-fangs/world/leaderboards-strict - a
/// season's best timed runs of a dungeon, Raider.IO's default view of its rankings</item>
/// </list>
/// </summary>
public static partial class WebLinks
{
    /// <summary>The dungeon's Wowhead page; a Wowhead search when its zone is not known.</summary>
    public static string Wowhead(int challengeModeId, string dungeonName, bool german)
    {
        var site = german ? "https://www.wowhead.com/de/" : "https://www.wowhead.com/";
        return DungeonCatalog.Find(challengeModeId)?.ZoneId is { } zone
            ? $"{site}zone={zone.ToString(CultureInfo.InvariantCulture)}"
            : $"{site}search?q={Uri.EscapeDataString(dungeonName)}";
    }

    /// <summary>The addon's page on the landing page - where "Installiere das Addon Raidex Keys" leads (Lars, 2026-09-13).</summary>
    public const string AddonPage = "https://raidex.app/keys";

    /// <summary>The addon on CurseForge.</summary>
    public const string AddonCurseForge = "https://www.curseforge.com/wow/addons/raidex-keys";

    /// <summary>The dungeon's picture on Blizzard's CDN, if it has one.</summary>
    public static string? DungeonImage(int challengeModeId) => DungeonCatalog.Find(challengeModeId)?.Image;

    /// <summary>
    /// The dungeon's leaderboard on Raider.IO - the best timed runs worldwide in
    /// the latest season it was part of; null for a dungeon Raider.IO does not know.
    /// </summary>
    public static string? RaiderIoLeaderboard(int challengeModeId) =>
        DungeonCatalog.Find(challengeModeId) is { RaiderIo: { } dungeon, RaiderIoSeason: { } season }
            ? $"https://raider.io/mythic-plus-rankings/{season}/{dungeon}/world/leaderboards-strict"
            : null;

    /// <param name="realm">The realm as slug, name, or name without spaces ("DieewigeWacht") - Raider.IO takes all three.</param>
    public static string RaiderIo(string region, string realm, string name) =>
        $"https://raider.io/characters/{Region(region)}/{Uri.EscapeDataString(realm)}/{Uri.EscapeDataString(name)}";

    /// <summary>
    /// Blizzard's slug of a realm name: lower case, apostrophes gone, spaces as
    /// hyphens, umlauts kept - "Drak'thul" drakthul, "Tarren Mill" tarren-mill,
    /// "Festung der Stürme" festung-der-stürme.
    /// </summary>
    public static string RealmSlug(string realmName)
    {
        var s = realmName.ToLowerInvariant().Replace("'", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal).Replace(")", "", StringComparison.Ordinal);
        return Spaces().Replace(s.Trim(), "-");
    }

    private static string Region(string region) => string.IsNullOrWhiteSpace(region) ? "eu" : region.Trim().ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
