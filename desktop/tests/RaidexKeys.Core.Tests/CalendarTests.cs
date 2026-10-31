// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Text.Json;
using RaidexKeys.Core.Raidex;

namespace RaidexKeys.Core.Tests;

public class CalendarTests
{
    /// <summary>
    /// A raid as <c>GET /raids</c> sends it: the table's columns in snake_case,
    /// PostgreSQL's flags as 0/1, Raidex's additions in camelCase.
    /// </summary>
    [Fact]
    public void ReadsARaidInBothCases()
    {
        const string json = """
            {"id":7,"guild_id":3,"title":"Leerenspitze","instance":"Die Leerenspitze","difficulty":"heroic",
             "image":null,"starts_at":1789322400000,"duration_min":180,"description":"","max_size":20,"min_size":"10",
             "created_by_is_gm":0,"created_by_is_lead":1,"createdByName":"Raidex","createdByLead":true,"createdByGM":false,
             "roster":[{"id":1,"userId":2,"name":"Eisenbart","realm":"blackrock","className":"Krieger","role":"tank","status":"confirmed","note":null,"isAlt":false,"mine":false},
                       {"id":2,"userId":5,"name":"Funkenflug","realm":"blackrock","className":"Schamanin","role":"dps","status":"absent","note":"Spätschicht","isAlt":false,"mine":true}],
             "bench":[{"userId":5,"mainName":"Funkenflug","alts":[{"id":3,"userId":5,"name":"Tarnkralle","realm":"blackrock","className":"Jäger","role":"dps","status":"confirmed","isAlt":true,"mine":true}]}],
             "mySignup":{"id":2,"userId":5,"name":"Funkenflug","realm":"blackrock","className":"Schamanin","role":"dps","status":"absent","isAlt":false,"mine":true},
             "myAlts":[],"canManage":1}
            """;
        var raid = RaidexCalendar.Raid(JsonDocument.Parse(json).RootElement);

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1789322400000).AddMinutes(180), raid.EndsAt);
        Assert.Equal(10, raid.MinSize);
        Assert.Null(raid.Description);
        Assert.Equal(RaidRank.RaidLead, raid.CreatedByRank);
        Assert.True(raid.CanManage);
        // Absent members stay in the roster but come to nothing.
        Assert.Equal(2, raid.Roster.Count);
        Assert.Equal(1, raid.Coming);
        Assert.Equal(0, raid.Count("dps"));
        Assert.Equal("Funkenflug", raid.MySignup?.Name);
        Assert.Equal("Tarnkralle", Assert.Single(Assert.Single(raid.Bench).Alts).Name);
        Assert.Equal(WowClass.Shaman, RaidexCalendar.ClassOf(raid.Roster[1].ClassName));
    }

    /// <summary>The guild's guides by author, "Mitglied" last, Raidex's own after all of them.</summary>
    [Fact]
    public void GroupsGuidesByAuthor()
    {
        const string json = """
            {"guides":[
              {"defKey":"wowhead:de","site":"wowhead","lang":"de","title":"Wowhead","url":"https://de.wowhead.com/","global":true,"scope":"system"},
              {"id":4,"site":"custom","lang":null,"title":"Video","url":"https://youtube.com/x","global":false,"scope":"instance","createdBy":"Mitglied","createdByGM":false,"createdByLead":false,"createdByQM":false},
              {"id":2,"site":"custom","title":"Discord","url":"https://discord.com/x","global":false,"createdBy":"Nachtfeder","createdByLead":true},
              {"id":3,"site":"custom","title":"Logs","url":"https://warcraftlogs.com/x","global":false,"createdBy":"Eisenbart","createdByGM":true}
            ],"canManage":true}
            """;
        var list = RaidexCalendar.Guides(JsonDocument.Parse(json).RootElement);
        var groups = list.ByAuthor();

        Assert.True(list.CanManage);
        Assert.Equal(["Eisenbart", "Nachtfeder", RaidexCalendar.UnnamedMember, null], groups.Select(g => g.Author));
        Assert.Equal(RaidRank.GuildMaster, groups[0].Rank);
        Assert.Null(Assert.Single(groups[3].Guides).Id);
        Assert.Equal("de.wowhead.com", groups[3].Guides[0].Host);
    }
}
