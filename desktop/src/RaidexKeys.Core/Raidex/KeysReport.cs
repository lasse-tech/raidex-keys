// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core.Raidex;

/// <summary>
/// What the app reports to Raidex (plan 7.1): the key of every own character,
/// and the guild keys the addon heard in game - each with when it was seen,
/// and the weekly reset the addon last knew, from which Raidex dates them.
/// </summary>
public static class KeysReport
{
    /// <summary>The report of one data state; null when there is nothing to date it by.</summary>
    public static object? From(AddonData data)
    {
        if (data.ResetAt is not { } reset)
        {
            return null;
        }
        return new
        {
            resetAt = reset.ToUnixTimeSeconds(),
            characters = data.Characters.Select(c => new
            {
                name = c.Name,
                realm = c.Realm,
                mapId = c.KeyMapId,
                level = c.KeyLevel,
                rating = c.Rating,
                observedAt = c.UpdatedAt.ToUnixTimeSeconds(),
            }),
            // The newest of a character's keys, when several guilds heard it.
            guildKeys = data.Guilds.SelectMany(g => g.Keys)
                .GroupBy(k => k.Character).Select(g => g.MaxBy(k => k.SeenAt)!)
                .Select(k => new
                {
                    character = k.Character,
                    mapId = k.MapId,
                    level = k.Level,
                    rating = k.Rating,
                    observedAt = k.SeenAt.ToUnixTimeSeconds(),
                }),
        };
    }

    /// <summary>A realm as letters only: display name, spaceless form and slug meet (Raidex's realm_key).</summary>
    public static string RealmKey(string realm) =>
        new string(realm.Where(c => !char.IsWhiteSpace(c) && c is not ('\'' or '’' or '-' or '.')).ToArray()).ToLowerInvariant();
}
