// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Formats.Cbor;
using System.IO.Compression;
using System.Text;
using RaidexKeys.Core.Mdt;

namespace RaidexKeys.Core.Tests;

/// <summary>
/// Mythic Dungeon Tools as the app reads it. The files are made up in MDT's
/// layout - nothing of MDT itself (GPL) is in this repository.
/// </summary>
public sealed class MdtTests
{
    /// <summary>A dungeon file: data inside a little code, which is never run.</summary>
    private const string DungeonFile = """
        local _, MDT = ...
        local L = MDT.L
        local dungeonIndex = 900
        MDT.dungeonList[dungeonIndex] = L["Testgrotte"]
        MDT.mapInfo[dungeonIndex] = {
          shortName = L["TestgrotteShort"],
          englishName = "Test Grotto",
          mapID = 9001
        };
        MDT.dungeonMaps[dungeonIndex] = { [1] = { customTextures = 'Interface\\AddOns\\'..'X' } }
        MDT.dungeonTotalCount[dungeonIndex] = { normal = 200 }
        MDT.dungeonEnemies[dungeonIndex] = {
          [1] = {
            ["name"] = "Höhlenratte",
            ["id"] = 1001,
            ["count"] = 10,
            ["clones"] = {
              [1] = { ["x"] = 1.5, ["y"] = -2.5, ["sublevel"] = 1 },
              [2] = { ["x"] = 3, ["y"] = -4, ["sublevel"] = 1, ["count"] = 30 },
              -- [3] = { ["x"] = 0, ["y"] = 0 },
            },
          },
          [2] = {
            ["name"] = "Grottenwächter",
            ["id"] = 1002,
            ["count"] = 0,
            ["isBoss"] = true,
            ["clones"] = { [1] = { ["x"] = 9, ["y"] = -9, ["sublevel"] = 1 } },
          },
        };
        """;

    /// <summary>MDT's SavedVariables as AceDB writes them: defaults left out.</summary>
    private const string SavedVariables = """
        MythicDungeonToolsDB = {
        ["global"] = {
        ["presets"] = {
        [900] = {
        {
        ["value"] = { ["pulls"] = { { [1] = { 1, 2 }, ["color"] = "228b22" }, { [2] = { 1 }, [7] = { 1 } } } },
        },
        {
        ["text"] = "Nur Ratten",
        ["uid"] = "abc",
        ["value"] = { ["pulls"] = { { [1] = { 1 } } } },
        },
        { ["text"] = "<New Preset>", ["value"] = 0 },
        },
        },
        },
        }
        """;

    [Fact]
    public void RoutesAddUpTheClonesOfEachPull()
    {
        var dungeon = MdtReader.ReadDungeon(DungeonFile)!;
        Assert.Equal((900, 9001, 200), (dungeon.Index, dungeon.MapId, dungeon.TotalCount));
        Assert.Equal(30, dungeon.Enemies[1].Clones[2].Count);   // a clone worth other than its enemy
        Assert.Equal(2, dungeon.Enemies[1].Clones.Count);       // the commented-out clone is no clone

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SavedVariables);
            var routes = MdtReader.ReadRoutes(path);
            Assert.Equal(2, routes.Count);                      // "<New Preset>" is no route

            var first = routes.Single(r => r.Name is null);     // MDT's unnamed "Default"
            Assert.True(first.Selected);                        // no currentPreset stored: the first
            var forces = RouteForces.Of(first, dungeon);
            Assert.Equal([40, 40], forces.Pulls.Select(p => p.Cumulative));   // 10 + 30, then the boss; enemy 7 is gone
            Assert.Equal(20.0, forces.Percent);
            Assert.Equal([("Höhlenratte", 2)], forces.Pulls[0].Enemies);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// A made-up route shared as a string. The old "!" format comes from the
    /// real LibDeflate and AceSerializer (run once outside this repository);
    /// the current one is built here as the game builds it.
    /// </summary>
    [Fact]
    public void BothExportFormatsGiveTheRoute()
    {
        const string legacy = "!nXySoimmmq0FP2mb7SWshWD2kbIfGuWbfSrPiiF74G6akNE60PxmoIZi8mKuQxoPLcXYoLptzEFSItBhgq4UMspmHPX)qpUEe7J5uUGGZT5ORpyRZR4NOS(aHQgpeeH4MV9P5voAL3n)n6IDc9AeH6Yltg)(";

        var cbor = new CborWriter();
        cbor.WriteStartMap(null);
        Bytes(cbor, "text");
        Bytes(cbor, "Ratten ^ und ~ mehr");
        Bytes(cbor, "uid");
        Bytes(cbor, "xyz");
        Bytes(cbor, "value");
        cbor.WriteStartMap(null);
        Bytes(cbor, "currentDungeonIdx");
        cbor.WriteInt32(900);
        Bytes(cbor, "pulls");
        cbor.WriteStartArray(null);
        cbor.WriteStartMap(null);   // pull 1: enemy 1, clones 1 and 2
        cbor.WriteInt32(1);
        cbor.WriteStartArray(null);
        cbor.WriteInt32(1);
        cbor.WriteInt32(2);
        cbor.WriteEndArray();
        Bytes(cbor, "color");
        Bytes(cbor, "228b22");
        cbor.WriteEndMap();
        cbor.WriteStartMap(null);   // pull 2: the boss
        cbor.WriteInt32(2);
        cbor.WriteStartArray(null);
        cbor.WriteInt32(1);
        cbor.WriteEndArray();
        cbor.WriteEndMap();
        cbor.WriteEndArray();
        cbor.WriteEndMap();
        cbor.WriteEndMap();
        var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(cbor.Encode());
        }
        var current = MdtString.Prefix + Convert.ToBase64String(packed.ToArray());

        var dungeon = MdtReader.ReadDungeon(DungeonFile)!;
        foreach (var text in new[] { legacy, current, "```\n" + current + "\n```" })
        {
            var route = MdtString.Decode(text);
            Assert.Equal(("Ratten ^ und ~ mehr", "xyz", 900), (route.Name, route.Uid, route.DungeonIndex));
            Assert.Equal(40, RouteForces.Of(route, dungeon).Count);
        }
        Assert.Throws<FormatException>(() => MdtString.Decode(current[..40]));
        Assert.False(MdtString.LooksLikeOne("hallo"));
    }

    private static void Bytes(CborWriter cbor, string text) => cbor.WriteByteString(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void PullsBecomeShapesOnTheirFloor()
    {
        // The hull leaves out the point inside; y turns downwards on the map.
        var hull = RouteMap.ConvexHull([new(0, 0), new(10, 0), new(5, 2), new(10, 10), new(0, 10)]);
        Assert.Equal(4, hull.Count);
        Assert.DoesNotContain(new MapPoint(5, 2), hull);

        var dungeon = MdtReader.ReadDungeon(DungeonFile)! with { TileDirs = new Dictionary<int, string> { [1] = "tiles" } };
        var route = new MdtRoute(900, "Test", null, false, [new MdtPull(new Dictionary<int, IReadOnlyList<int>> { [1] = [1, 2] })]);
        var floor = Assert.Single(RouteMap.Of(route, dungeon));
        var pull = Assert.Single(floor.Pulls);
        Assert.Equal([(1.5, 2.5), (3.0, 4.0)], pull.Enemies.Select(p => (p.X, p.Y)));
        Assert.Single(floor.Others);                 // the boss, left standing
        Assert.Empty(RouteMap.Of(route, MdtReader.ReadDungeon(DungeonFile)!));   // no tiles, no map
    }
}
