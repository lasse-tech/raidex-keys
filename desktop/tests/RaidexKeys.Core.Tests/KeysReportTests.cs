// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Raidex;

namespace RaidexKeys.Core.Tests;

/// <summary>A realm meets itself across its spellings - the same letters Raidex's realm_key keeps.</summary>
public sealed class KeysReportTests
{
    [Fact]
    public void RealmSpellingsMeet()
    {
        // The addon's display name, a guild key's spaceless form, Blizzard's slug.
        Assert.Equal("festungderstürme", KeysReport.RealmKey("Festung der Stürme"));
        Assert.Equal("festungderstürme", KeysReport.RealmKey("FestungderStürme"));
        Assert.Equal("festungderstürme", KeysReport.RealmKey("festung-der-stürme"));
        Assert.Equal("kelthuzad", KeysReport.RealmKey("Kel'Thuzad"));
    }
}
