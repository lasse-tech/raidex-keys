// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core.Tests;

/// <summary>The addon's file as the desktop reads it. All data is made up.</summary>
public sealed class AddonDataTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("raidexkeys-test-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>A RaidexKeys.lua as WoW writes it: one key per line, no indentation.</summary>
    private const string File = """

        RaidexKeysDB = {
        ["schema"] = 1,
        ["season"] = 18,
        ["resetAt"] = 1800000000,
        ["maps"] = {
        [9001] = {
        ["name"] = "Testgrotte",
        ["timeLimit"] = 1800,
        },
        },
        ["chars"] = {
        ["Player-1-0001"] = {
        ["name"] = "Zarok",
        ["realm"] = "Grüne Wiese",
        ["classId"] = 11,
        ["rating"] = 2197,
        ["best"] = {
        },
        ["updatedAt"] = 1799990000,
        },
        ["Player-1-0002"] = {
        ["name"] = "Lyriana",
        ["realm"] = "Grüne Wiese",
        ["classId"] = 5,
        ["rating"] = 2311.5,
        ["key"] = {
        ["mapId"] = 9001,
        ["level"] = 12,
        },
        ["best"] = {
        [9001] = {
        ["level"] = 11,
        ["durationSec"] = 1705,
        ["overTime"] = false,
        ["score"] = 285.5,
        },
        },
        ["vault"] = {
        {
        ["threshold"] = 1,
        ["progress"] = 2,
        ["level"] = 12,
        ["itemLevel"] = 694,
        },
        {
        ["threshold"] = 4,
        ["progress"] = 2,
        ["level"] = 0,
        },
        },
        ["week"] = {
        {
        ["mapId"] = 9001,
        ["level"] = 10,
        ["timed"] = false,
        },
        {
        ["mapId"] = 9001,
        ["level"] = 12,
        ["timed"] = true,
        },
        },
        ["updatedAt"] = 1799990500,
        },
        },
        ["guilds"] = {
        ["Testgilde-Grüne Wiese"] = {
        ["name"] = "Testgilde",
        ["realm"] = "Grüne Wiese",
        ["keys"] = {
        ["Auróra-Kel'Test"] = {
        ["mapId"] = 9001,
        ["level"] = 2,
        ["rating"] = 1147,
        ["seenAt"] = 1799990000,
        },
        ["Ohnekey-Testrealm"] = {
        ["rating"] = 1127,
        ["seenAt"] = 1799990000,
        },
        },
        },
        },
        }
        """;

    private AddonData Read()
    {
        var path = Path.Combine(_dir, AddonDataReader.FileName);
        System.IO.File.WriteAllText(path, File);
        return AddonDataReader.Read(path);
    }

    [Fact]
    public void ReadsWhatTheAddonWrites()
    {
        var data = Read();
        var lyriana = data.Characters.Single(c => c.Name == "Lyriana");
        Assert.Equal("Grüne Wiese", lyriana.Realm);
        Assert.Equal(WowClass.Priest, lyriana.Class);
        Assert.Equal((9001, 12), (lyriana.KeyMapId, lyriana.KeyLevel));
        Assert.Equal(new BestRun(11, 1705, false, 285.5), lyriana.Best[9001]);
        Assert.Equal(new VaultSlot(1, 2, 12, 694), lyriana.Vault[0]);
        Assert.Equal(2, lyriana.Vault[1].RunsMissing);
        Assert.Equal([12, 10], lyriana.WeekRuns.Select(r => r.Level));

        // After the reset the vault starts empty, and last week's slot waits to be claimed.
        var nextWeek = lyriana.UpdatedAt.AddDays(7);
        Assert.All(lyriana.VaultIn(nextWeek), slot => Assert.Equal(0, slot.Progress));
        Assert.Equal(1, lyriana.RewardsWaiting(nextWeek));
        Assert.Equal(0, lyriana.RewardsWaiting(lyriana.UpdatedAt.AddDays(-1)));

        var keys = data.Guilds.Single().Keys;
        Assert.Equal(2, keys.Single(k => k.Character == "Auróra-Kel'Test").Level);
        Assert.Null(keys.Single(k => k.Character == "Ohnekey-Testrealm").Level);
    }

    /// <summary>
    /// A rating above 0 is only earned by running keys. A character stored with
    /// a rating but without runs is incomplete data, never "no runs yet".
    /// </summary>
    [Theory]
    [InlineData(2197, 0, true)]
    [InlineData(2197, 1, false)]
    [InlineData(0, 0, false)]
    public void RatingAboveZeroNeedsRuns(int rating, int runs, bool incomplete)
    {
        var best = Enumerable.Range(9001, runs).ToDictionary(id => id, _ => new BestRun(10, 1700, false, 280));
        var character = new AddonCharacter("Player-1-0001", "Zarok", "Testrealm", WowClass.Druid, 90,
            null, null, rating, best, [], DateTimeOffset.UnixEpoch);
        Assert.Equal(incomplete, character.IsIncomplete);
    }

    [Fact]
    public void FileWithRatingButNoRunsReadsAsIncomplete()
    {
        var data = Read();
        Assert.True(data.Characters.Single(c => c.Name == "Zarok").IsIncomplete);
        Assert.False(data.Characters.Single(c => c.Name == "Lyriana").IsIncomplete);
    }

    [Fact]
    public void AnyFolderOfTheInstallationLeadsToTheSameAddOnsFolder()
    {
        var wow = Path.Combine(_dir, "World of Warcraft");
        var addOns = Path.Combine(wow, "_retail_", "Interface", "AddOns");
        Directory.CreateDirectory(addOns);
        Directory.CreateDirectory(Path.Combine(wow, "_classic_", "Interface", "AddOns"));

        foreach (var input in new[] { wow, Path.Combine(wow, "_retail_"), Path.Combine(wow, "_retail_", "Interface"), addOns })
        {
            Assert.Equal(addOns, AddOnsFolder.Inspect(input).AddOnsDir);
        }
        Assert.False(AddOnsFolder.Inspect(Path.Combine(wow, "_classic_", "Interface", "AddOns")).IsValid);
        Assert.False(AddOnsFolder.Inspect(_dir).IsValid);
    }
    [Fact]
    public void ReadsTheGameCalendarsRaidsWithTheirSignups()
    {
        var path = Path.Combine(_dir, "raids.lua");
        System.IO.File.WriteAllText(path, """
            RaidexKeysDB = {
            ["schema"] = 1,
            ["raids"] = {
            ["Testgilde-Grüne Wiese"] = {
            ["calendar"] = {
            ["77-20300101"] = { ["key"] = "77-20300101", ["title"] = "Testraid", ["at"] = 1893528000,
              ["creator"] = "Zarok", ["instanceName"] = "Testhalle", ["difficultyName"] = "Heroisch",
              ["instanceMap"] = 9100, ["difficultyId"] = 15 },
            },
            ["events"] = {
            ["77-20300101"] = { ["at"] = 1893528000, ["signups"] = {
              ["Lyriana-Grüne Wiese"] = { ["who"] = "Lyriana-Grüne Wiese", ["role"] = "H", ["classFile"] = "PRIEST",
                ["itemLevel"] = 700, ["assigned"] = true, ["note"] = "später" },
              ["Zarok-Grüne Wiese"] = { ["who"] = "Zarok-Grüne Wiese", ["role"] = "T", ["removed"] = true },
            } },
            },
            ["progress"] = { ["9100|15"] = { ["killed"] = 3, ["bosses"] = 8 } },
            },
            },
            }
            """);

        var raid = Assert.Single(AddonDataReader.Read(path).Raids);
        Assert.Equal("Testraid", raid.Title);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893528000), raid.At);
        Assert.Equal((15, 3, 8), (raid.DifficultyId, raid.Killed, raid.Bosses));
        var signup = Assert.Single(raid.Signups);
        Assert.Equal(("Lyriana-Grüne Wiese", 'H', WowClass.Priest, 700, true, "später"),
            (signup.Who, signup.Role, signup.Class, signup.ItemLevel, signup.Assigned, signup.Note));
    }
}
