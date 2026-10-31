// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace RaidexKeys.Core.Raidex;

/// <summary>A guild member's place in a raid: one character, its role, whether it comes.</summary>
/// <param name="Role">"tank", "healer" or "dps" - Raidex's own words.</param>
/// <param name="Status">"confirmed", "tentative" or "absent".</param>
/// <param name="Realm">The realm as slug.</param>
/// <param name="ClassName">The class in Raidex's language (German); null when it knows none.</param>
public sealed record RaidSignup(
    long Id, long UserId, string Name, string Realm, string? ClassName, string Role, string Status,
    string? Note, bool IsAlt, bool Mine);

/// <summary>One member's alts on the bench, under the name of their main.</summary>
public sealed record RaidBench(long UserId, string MainName, IReadOnlyList<RaidSignup> Alts);

/// <summary>
/// A date in the guild's raid calendar, as <c>GET /raids</c> hands it out:
/// what is played, when, who comes - and what the caller may do with it.
/// </summary>
/// <param name="Instance">The raid or dungeon by its German journal name, free text; what the guides are looked up by.</param>
/// <param name="Difficulty">"lfr", "normal", "heroic", "mythic" or "mplus"; null when none was set.</param>
/// <param name="MaxSize">0 for no limit.</param>
/// <param name="Roster">The mains, those who are absent included, tanks first.</param>
/// <param name="CanManage">The caller created it or leads the guild's raids: they may change and delete it.</param>
public sealed record RaidEvent(
    long Id, string Title, string? Instance, string? Difficulty, string? Image,
    DateTimeOffset StartsAt, int DurationMin, string? Description, int MaxSize, int MinSize,
    string? CreatedByName, RaidRank CreatedByRank,
    IReadOnlyList<RaidSignup> Roster, IReadOnlyList<RaidBench> Bench,
    RaidSignup? MySignup, IReadOnlyList<RaidSignup> MyAlts, bool CanManage)
{
    public DateTimeOffset EndsAt => StartsAt.AddMinutes(DurationMin);

    /// <summary>The mains who come or may come, per role - "absent" does not count, as on Raidex.</summary>
    public int Count(string role) => Roster.Count(s => s.Role == role && s.Status != "absent");

    public int Coming => Roster.Count(s => s.Status != "absent");
}

/// <summary>Who created a raid or added a guide, as far as the guild's ranks go.</summary>
public enum RaidRank
{
    Member,
    Quartermaster,
    RaidLead,
    GuildMaster,
}

/// <summary>
/// A guide for an instance: always a link. Raidex's own four per instance
/// (Wowhead and buffed in German, Wowhead and Method in English) come
/// without an id; those the guild added carry who added them.
/// </summary>
/// <param name="Lang">"de", "en" or null.</param>
/// <param name="IconUrl">The site's icon as Raidex cached it; null for Raidex's own links.</param>
/// <param name="CreatedBy">The author's main, never their BattleTag; null for Raidex's own links.</param>
public sealed record RaidGuide(
    long? Id, string Site, string? Lang, string Title, string Url, string? IconUrl, bool Global,
    string? CreatedBy, RaidRank CreatedByRank)
{
    public string Host => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host : Url;
}

/// <summary>An instance's guides, and whether the caller may add and remove them.</summary>
public sealed record GuideList(IReadOnlyList<RaidGuide> Guides, bool CanManage)
{
    /// <summary>
    /// The guild's guides by who added them, sorted by name with "Mitglied"
    /// last, then Raidex's own under a null author - the grouping Raidex's
    /// web and mobile clients show.
    /// </summary>
    public IReadOnlyList<(string? Author, RaidRank Rank, IReadOnlyList<RaidGuide> Guides)> ByAuthor()
    {
        var groups = Guides.Where(g => !g.Global)
            .GroupBy(g => g.CreatedBy ?? RaidexCalendar.UnnamedMember)
            .OrderBy(g => g.Key == RaidexCalendar.UnnamedMember)
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => ((string?)g.Key, g.Max(x => x.CreatedByRank), (IReadOnlyList<RaidGuide>)g.ToList()))
            .ToList();
        var own = Guides.Where(g => g.Global).ToList();
        if (own.Count > 0)
        {
            groups.Add((null, RaidRank.Member, own));
        }
        return groups;
    }
}

/// <summary>A raid or dungeon of Blizzard's journal, to plan a date for.</summary>
public sealed record JournalInstance(long Id, string Name, IReadOnlyList<string> Difficulties, string? Image, string Expansion);

/// <summary>One of the caller's characters on Battle.net, to sign up with.</summary>
/// <param name="Id">"realmslug:name" in lower case, as Raidex keys characters.</param>
public sealed record AccountCharacter(string Id, string Name, string Realm, int Level, string? ClassName, int? ClassId);

/// <summary>A raid as it is created or changed.</summary>
public sealed record RaidDraft(
    string Title, string? Instance, string? Difficulty, DateTimeOffset StartsAt, int DurationMin,
    string? Description, int MinSize, int MaxSize);

/// <summary>A sign-up as it is sent: which character, as main or alt, in which role and state.</summary>
public sealed record SignupDraft(AccountCharacter Character, string Role, string Status, string? Note, bool IsAlt);

/// <summary>Reads the calendar's answers - their columns come in snake_case, Raidex's additions in camelCase.</summary>
public static class RaidexCalendar
{
    /// <summary>The name Raidex gives an author without a main.</summary>
    public const string UnnamedMember = "Mitglied";

    public static readonly string[] Roles = ["tank", "healer", "dps"];
    public static readonly string[] Statuses = ["confirmed", "tentative", "absent"];
    public static readonly string[] Difficulties = ["lfr", "normal", "heroic", "mythic", "mplus"];

    public static RaidEvent Raid(JsonElement r) => new(
        Long(r, "id"),
        Str(r, "title") ?? "",
        Str(r, "instance"),
        Str(r, "difficulty"),
        Str(r, "image"),
        DateTimeOffset.FromUnixTimeMilliseconds(Long(r, "starts_at")),
        (int)Long(r, "duration_min", 120),
        Str(r, "description"),
        (int)Long(r, "max_size"),
        (int)Long(r, "min_size"),
        Str(r, "createdByName") ?? Str(r, "created_by_name"),
        Rank(Bool(r, "createdByGM"), Bool(r, "createdByLead"), Bool(r, "createdByQM")),
        List(r, "roster", Signup),
        List(r, "bench", b => new RaidBench(Long(b, "userId"), Str(b, "mainName") ?? "—", List(b, "alts", Signup))),
        r.TryGetProperty("mySignup", out var mine) && mine.ValueKind == JsonValueKind.Object ? Signup(mine) : null,
        List(r, "myAlts", Signup),
        Bool(r, "canManage"));

    public static RaidSignup Signup(JsonElement s) => new(
        Long(s, "id"), Long(s, "userId"), Str(s, "name") ?? "", Str(s, "realm") ?? "", Str(s, "className"),
        Str(s, "role") ?? "dps", Str(s, "status") ?? "confirmed", Str(s, "note"), Bool(s, "isAlt"), Bool(s, "mine"));

    public static GuideList Guides(JsonElement root) => new(
        List(root, "guides", g => new RaidGuide(
            g.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : null,
            Str(g, "site") ?? "custom", Str(g, "lang"), Str(g, "title") ?? "Guide", Str(g, "url") ?? "",
            Str(g, "iconUrl"), Bool(g, "global"), Str(g, "createdBy"),
            Rank(Bool(g, "createdByGM"), Bool(g, "createdByLead"), Bool(g, "createdByQM")))),
        Bool(root, "canManage"));

    public static JournalInstance Instance(JsonElement i) => new(
        Long(i, "id"), Str(i, "name") ?? "",
        List(i, "difficulties", d => d.GetString() ?? ""), Str(i, "image"), Str(i, "expansion") ?? "");

    public static AccountCharacter Character(JsonElement c) => new(
        Str(c, "id") ?? "", Str(c, "name") ?? "", Str(c, "realm") ?? "", (int)Long(c, "level"),
        Str(c, "className"), c.TryGetProperty("classId", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt32() : null);

    /// <summary>The body of <c>POST /raids</c> and <c>PATCH /raids/{id}</c>.</summary>
    public static object Body(RaidDraft d) => new Dictionary<string, object?>
    {
        ["title"] = d.Title,
        ["instance"] = d.Instance,
        ["difficulty"] = d.Difficulty,
        ["starts_at"] = d.StartsAt.ToUnixTimeMilliseconds(),
        ["duration_min"] = d.DurationMin,
        ["description"] = d.Description,
        ["min_size"] = d.MinSize,
        ["max_size"] = d.MaxSize,
    };

    /// <summary>
    /// A class as Raidex names it - in German from Blizzard's German profile,
    /// either form - or in English; null for anything else.
    /// </summary>
    public static WowClass? ClassOf(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "krieger" or "kriegerin" or "warrior" => WowClass.Warrior,
        "paladin" or "paladina" => WowClass.Paladin,
        "jäger" or "jägerin" or "hunter" => WowClass.Hunter,
        "schurke" or "schurkin" or "rogue" => WowClass.Rogue,
        "priester" or "priesterin" or "priest" => WowClass.Priest,
        "todesritter" or "todesritterin" or "death knight" => WowClass.DeathKnight,
        "schamane" or "schamanin" or "shaman" => WowClass.Shaman,
        "magier" or "magierin" or "mage" => WowClass.Mage,
        "hexenmeister" or "hexenmeisterin" or "warlock" => WowClass.Warlock,
        "mönch" or "mönchin" or "monk" => WowClass.Monk,
        "druide" or "druidin" or "druid" => WowClass.Druid,
        "dämonenjäger" or "dämonenjägerin" or "demon hunter" => WowClass.DemonHunter,
        "rufer" or "ruferin" or "evoker" => WowClass.Evoker,
        _ => null,
    };

    private static RaidRank Rank(bool gm, bool lead, bool qm) =>
        gm ? RaidRank.GuildMaster : lead ? RaidRank.RaidLead : qm ? RaidRank.Quartermaster : RaidRank.Member;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    /// <summary>A number, also when PostgreSQL's driver sent it as a string.</summary>
    private static long Long(JsonElement e, string name, long fallback = 0)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return fallback;
        }
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt64(out var n) ? n : (long)v.GetDouble(),
            JsonValueKind.String when long.TryParse(v.GetString(), out var n) => n,
            _ => fallback,
        };
    }

    /// <summary>true, 1 or "1" - Raidex sends its flags all three ways.</summary>
    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => v.GetDouble() != 0,
            JsonValueKind.String => v.GetString() is "1" or "true" or "t",
            _ => false,
        };

    private static IReadOnlyList<T> List<T>(JsonElement e, string name, Func<JsonElement, T> item) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(item).ToList()
            : [];
}
