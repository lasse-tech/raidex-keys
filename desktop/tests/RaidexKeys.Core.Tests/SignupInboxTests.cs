// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core.Tests;

public class SignupInboxTests
{
    private const string Game = "\r\nRaidexKeysDB = {\r\n[\"schema\"] = 1,\r\n}\r\nRaidexKeysInbox = {\r\n[\"requests\"] = {\r\n},\r\n}\r\n";

    private static SignupRequest Request(string id, string? note = null) => new(id, "f", "Player-1-0001", "Zarok-GrüneWiese",
        "Testgilde-Grüne Wiese", "77-20300101", "Testraid", DateTimeOffset.FromUnixTimeSeconds(1893528000),
        "signup", 'H', note, DateTimeOffset.FromUnixTimeSeconds(1800000000));

    [Fact]
    public void ReplacesOnlyTheInboxAndKeepsTheRestAsTheGameWroteIt()
    {
        var written = SignupInbox.Replace(Game, [Request("a", "sagt \"später\"\nkommt")]);
        Assert.StartsWith("\r\nRaidexKeysDB = {\r\n[\"schema\"] = 1,\r\n}\r\n", written);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(written, "^RaidexKeysInbox", System.Text.RegularExpressions.RegexOptions.Multiline));

        var inbox = (LuaTable)LuaParser.ParseGlobals(written)["RaidexKeysInbox"]!;
        var request = inbox.Table("requests")!.Table("a")!;
        Assert.Equal(("Player-1-0001", "77-20300101", "H", "sagt \"später\"\nkommt", 1800000000L),
            (request.String("guid"), request.String("key"), request.String("role"), request.String("note"), request.Integer("at")));
        Assert.NotNull(LuaParser.ParseGlobals(written)["RaidexKeysDB"]);

        var emptied = SignupInbox.Replace(written, []);
        Assert.Equal("\r\nRaidexKeysDB = {\r\n[\"schema\"] = 1,\r\n}\r\n", emptied);
    }
}
