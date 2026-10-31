// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace RaidexKeys.Core.Web;

/// <summary>
/// A Mythic+ dungeon as the web knows it: Wowhead zone, Blizzard's picture, and
/// Raider.IO's slug of it with the latest main season it was part of.
/// </summary>
public sealed record DungeonEntry(string Name, int MapId, int? ZoneId, string? Image,
    string? RaiderIo = null, string? RaiderIoSeason = null);

/// <summary>
/// Every Mythic+ dungeon by challenge-mode id - the id the addon writes. Built
/// from the game's tables once per season and shipped with the app.
/// </summary>
public static class DungeonCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<int, DungeonEntry>> Entries = new(Load);

    public static DungeonEntry? Find(int challengeModeId) => Entries.Value.GetValueOrDefault(challengeModeId);

    private static IReadOnlyDictionary<int, DungeonEntry> Load()
    {
        using var stream = typeof(DungeonCatalog).Assembly.GetManifestResourceStream("RaidexKeys.dungeons.json");
        if (stream is null)
        {
            return new Dictionary<int, DungeonEntry>();
        }
        var raw = JsonSerializer.Deserialize<Dictionary<string, DungeonEntry>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return raw.ToDictionary(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture), kv => kv.Value);
    }
}
