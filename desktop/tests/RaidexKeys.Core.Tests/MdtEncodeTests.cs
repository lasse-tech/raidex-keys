// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Mdt;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core.Tests;

/// <summary>
/// Writing an MDT export string. The rules were read off MDT 6.2.16's own
/// output (plan 5.5): a table whose keys are 1 to n - an empty one included -
/// is a CBOR array, everything else a map, and Lua strings are byte strings.
/// Checked here by writing a route and reading it back; that the bytes match
/// MDT's own was checked against a real export outside the repository, which
/// is where MDT's data stays.
/// </summary>
public sealed class MdtEncodeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("raidexkeys-mdt-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>A preset as MDT keeps one, small but with every kind of value in it.</summary>
    private const string Preset = """

        MythicDungeonToolsDB = {
        ["global"] = {
        ["presets"] = {
        [1] = {
        [1] = {
        ["text"] = "Testroute",
        ["uid"] = "abcDEF123",
        ["difficulty"] = 10,
        ["objects"] = {
        },
        ["colorPaletteInfo"] = {
        ["autoColoring"] = true,
        ["colorPaletteIdx"] = 4,
        },
        ["value"] = {
        ["currentDungeonIdx"] = 1,
        ["currentPull"] = 1,
        ["teeming"] = 0,
        ["selection"] = {
        [1] = 1,
        },
        ["pulls"] = {
        [1] = {
        ["color"] = "ff3eff",
        [7] = {
        [1] = 1,
        [2] = 2,
        },
        },
        [2] = {
        ["color"] = "3e3eff",
        [9] = {
        [1] = 1,
        },
        },
        },
        },
        },
        },
        },
        },
        }
        """;

    private LuaTable Read()
    {
        var file = Path.Combine(_dir, "MythicDungeonTools.lua");
        File.WriteAllText(file, Preset);
        var db = (LuaTable)LuaParser.ParseGlobals(File.ReadAllText(file))["MythicDungeonToolsDB"]!;
        return db.Table("global")!.Table("presets")!.Table(1L)!.Table(1L)!;
    }

    [Fact]
    public void AnEncodedRouteReadsBackAsItself()
    {
        var route = MdtString.Decode(MdtString.Encode(Read()));

        Assert.Equal("Testroute", route.Name);
        Assert.Equal("abcDEF123", route.Uid);
        Assert.Equal(1, route.DungeonIndex);
        Assert.Equal(2, route.Pulls.Count);
        Assert.Equal([1, 2], route.Pulls[0].Clones[7]);
        Assert.Equal([1], route.Pulls[1].Clones[9]);
    }

    [Fact]
    public void ExportAddsWhatMdtAddsAndKeepsTheRest()
    {
        var exported = MdtString.Export(Read(), 6216, "Zarok", "Grüne Wiese", 11);
        var back = MdtString.Decode(exported).Preset!;

        Assert.Equal(6216, back.Integer("addonVersion"));
        Assert.Equal("Zarok", back.Table("createdBy")!.String("name"));
        Assert.Equal("Grüne Wiese", back.Table("createdBy")!.String("realm"));
        Assert.Equal(11, back.Table("createdBy")!.Integer("classIdx"));
        // Everything MDT wrote is still there, down to a pull's colour.
        Assert.Equal(10, back.Integer("difficulty"));
        Assert.Equal(4, back.Table("colorPaletteInfo")!.Integer("colorPaletteIdx"));
        Assert.Equal("ff3eff", back.Table("value")!.Table("pulls")!.Table(1L)!.String("color"));
    }

    [Fact]
    public void AStringThatAlreadyHasAnAuthorKeepsIt()
    {
        var once = MdtString.Export(Read(), 6216, "Zarok", "Grüne Wiese", 11);
        var again = MdtString.Export(MdtString.Decode(once).Preset!, 9999, "Somebody", "Else", 1);

        Assert.Equal("Zarok", MdtString.Decode(again).Preset!.Table("createdBy")!.String("name"));
        Assert.Equal(6216, MdtString.Decode(again).Preset!.Integer("addonVersion"));
    }
    [Fact]
    public void WhatGoesToRaidexCarriesNoAuthor()
    {
        var foreign = MdtString.Decode(MdtString.Export(Read(), 6216, "Zarok", "Grüne Wiese", 11));
        var saved = MdtString.Decode(MdtString.WithoutAuthor(foreign.Preset!, null));

        Assert.Null(saved.Preset!["createdBy"]);
        Assert.Equal(6216, saved.Preset!.Integer("addonVersion"));
        Assert.Equal((foreign.Name, foreign.Uid, foreign.Pulls.Count), (saved.Name, saved.Uid, saved.Pulls.Count));
    }
}
