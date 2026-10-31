// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using RaidexKeys.App.Raidex;
using RaidexKeys.Core;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.SavedVariables;
using RaidexKeys.Core.Web;

namespace RaidexKeys.App;

/// <summary>
/// The raid calendar of the preview's guild: three raids ahead, one behind,
/// the guides of a raid of the season - answered in memory, so signing up,
/// planning and deleting work in the preview as they would on Raidex, and are
/// forgotten with the window. The player is Raidex, raid lead of the guild.
/// </summary>
/// <remarks>
/// Instance and class names come in German, as Raidex serves them from
/// Blizzard's German journal whatever language the app speaks. The raids are
/// Midnight's (JournalInstance 1307, 1314), their German names checked
/// against the game's table on wago.tools.
/// </remarks>
internal sealed class PreviewCalendar : ICalendarBackend
{
    /// <summary>The same evenings as the game's calendar has them, signed up with the addon.</summary>
    public static IReadOnlyList<GameRaid> GameRaids(DateTimeOffset now)
    {
        var today = new DateTimeOffset(now.LocalDateTime.Date, now.Offset);
        static GameRaidSignup S(string who, char role, WowClass c, int ilvl, string? note = null, bool assigned = false) =>
            new(who + "-Blackmoore", role, c, ilvl, assigned, note);
        return
        [
            new("Nachtwache-Blackmoore", "4711-1", "Leerenspitze – Heroisch", today.AddDays(1).AddHours(20), "Raidex",
                Voidspire, "Heroisch", 15, 4, 8,
                [S("Eisenbart", 'T', WowClass.Warrior, 712, assigned: true), S("Raidex", 'T', WowClass.DeathKnight, 709),
                 S("Nachtfeder", 'H', WowClass.Druid, 710, "ab 20:15"), S("Grimwald", 'H', WowClass.Priest, 702),
                 S("Schattenweb", 'D', WowClass.Rogue, 714), S("Frostbann", 'D', WowClass.Mage, 708),
                 S("Glutschwinge", 'D', WowClass.Evoker, 706)]),
            new("Nachtwache-Blackmoore", "4712-2", "Traumriss – Mythisch", today.AddDays(5).AddHours(20), "Raidex",
                Dreamrift, "Mythisch", 16, 1, 6,
                [S("Eisenbart", 'T', WowClass.Warrior, 712), S("Nachtfeder", 'H', WowClass.Druid, 710)]),
            new("Nachtwache-Blackmoore", "4710-0", "Leerenspitze – Normal", today.AddDays(-3).AddHours(20), "Eisenbart",
                Voidspire, "Normal", 14, 8, 8,
                [S("Eisenbart", 'T', WowClass.Warrior, 705), S("Grimwald", 'H', WowClass.Priest, 698),
                 S("Frostbann", 'D', WowClass.Mage, 701)]),
        ];
    }

    private const long Me = 1;
    private const string Voidspire = "Die Leerenspitze";
    private const string Dreamrift = "Der Traumriss";

    private sealed class Sample
    {
        public required long Id;
        public required string Title;
        public string? Instance;
        public string? Difficulty;
        public string? Image;
        public DateTimeOffset StartsAt;
        public int DurationMin = 180;
        public string? Description;
        public int MinSize = 10;
        public int MaxSize = 20;
        public string? CreatedBy;
        public RaidRank Rank;
        public long CreatedById;
        public List<(long User, RaidSignup Signup)> Signups = [];
    }

    private readonly List<Sample> _raids = [];
    private readonly List<RaidGuide> _guides = [];
    private long _next = 100;

    private static readonly IReadOnlyList<AccountCharacter> Account =
    [
        new("blackrock:raidex", "Raidex", "Blackrock", 90, "Todesritter", 6),
        new("blackrock:frostbann", "Frostbann", "Blackrock", 90, "Magier", 8),
        new("blackrock:tarnkralle", "Tarnkralle", "Blackrock", 90, "Jäger", 3),
        new("blackrock:donnerhuf", "Donnerhuf", "Blackrock", 90, "Druide", 11),
    ];

    public PreviewCalendar()
    {
        var today = DateTimeOffset.Now.Date;
        DateTimeOffset At(int days, int hour, int minute = 0) =>
            new DateTimeOffset(today.AddDays(days).AddHours(hour).AddMinutes(minute), TimeZoneInfo.Local.GetUtcOffset(today.AddDays(days)));

        var progress = new Sample
        {
            Id = 11,
            Title = "Leerenspitze – Heroisch",
            Instance = Voidspire,
            Difficulty = "heroic",
            StartsAt = At(1, 20),
            CreatedBy = "Raidex",
            Rank = RaidRank.RaidLead,
            CreatedById = Me,
            Description = Say("Progress-Abend: die ersten vier Bosse, danach Farm. Fläschchen und Essen bitte selbst mitbringen.",
                "Progress night: the first four bosses, farm after that. Bring your own flasks and food, please."),
        };
        Add(progress, 2, "Eisenbart", "Krieger", "tank");
        Add(progress, Me, "Raidex", "Todesritter", "tank");
        Add(progress, 3, "Nachtfeder", "Priester", "healer");
        Add(progress, 4, "Mondlicht", "Druide", "healer");
        Add(progress, 5, "Glutschwinge", "Rufer", "dps");
        Add(progress, 6, "Schattenweb", "Schurke", "dps", "tentative", Say("komme evtl. 10 Min. später", "might be 10 min late"));
        Add(progress, 7, "Sturmklinge", "Paladin", "dps");
        Add(progress, 8, "Aschenherz", "Hexenmeister", "dps");
        Add(progress, 9, "Kaltblut", "Dämonenjäger", "dps");
        Add(progress, 10, "Funkenflug", "Schamane", "dps", "absent", Say("Spätschicht", "late shift"));
        Add(progress, Me, "Tarnkralle", "Jäger", "dps", alt: true);
        Add(progress, 3, "Lichtschwur", "Paladin", "healer", alt: true);
        _raids.Add(progress);

        var keys = new Sample
        {
            Id = 12,
            Title = Say("M+ Abend", "M+ night"),
            Instance = Say("Mördergasse", "Murder Row"),
            Difficulty = "mplus",
            Image = WebLinks.DungeonImage(587),
            StartsAt = At(3, 19, 30),
            DurationMin = 150,
            MinSize = 5,
            MaxSize = 10,
            CreatedBy = "Glutschwinge",
            Rank = RaidRank.Member,
            CreatedById = 5,
            Description = Say("Keys ab +10, wer da ist, geht mit.", "Keys from +10, whoever is around joins."),
        };
        Add(keys, 5, "Glutschwinge", "Rufer", "dps");
        Add(keys, 3, "Nachtfeder", "Priester", "healer", "tentative");
        Add(keys, 2, "Eisenbart", "Krieger", "tank");
        _raids.Add(keys);

        var mythic = new Sample
        {
            Id = 13,
            Title = "Traumriss – Mythisch",
            Instance = Dreamrift,
            Difficulty = "mythic",
            StartsAt = At(6, 20),
            MinSize = 20,
            MaxSize = 20,
            CreatedBy = "Eisenbart",
            Rank = RaidRank.GuildMaster,
            CreatedById = 2,
        };
        Add(mythic, 2, "Eisenbart", "Krieger", "tank");
        Add(mythic, 3, "Nachtfeder", "Priester", "healer");
        _raids.Add(mythic);

        var last = new Sample
        {
            Id = 9,
            Title = "Leerenspitze – Normal",
            Instance = Voidspire,
            Difficulty = "normal",
            StartsAt = At(-5, 20),
            CreatedBy = "Raidex",
            Rank = RaidRank.RaidLead,
            CreatedById = Me,
        };
        Add(last, Me, "Raidex", "Todesritter", "tank");
        Add(last, 3, "Nachtfeder", "Priester", "healer");
        Add(last, 5, "Glutschwinge", "Rufer", "dps");
        _raids.Add(last);

        _guides.Add(new RaidGuide(1, "custom", "de", Say("Taktiken im Gilden-Discord", "Tactics in the guild Discord"),
            "https://discord.com/channels/nachtwache/taktik", null, false, "Raidex", RaidRank.RaidLead));
        _guides.Add(new RaidGuide(2, "custom", "de", Say("Boss 1 – Video", "Boss 1 – video"),
            "https://www.youtube.com/watch?v=leerenspitze", null, false, "Schattenweb", RaidRank.Member));
    }

    private static string Say(string german, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? german : english;

    private void Add(Sample raid, long user, string name, string className, string role,
        string status = "confirmed", string? note = null, bool alt = false) =>
        raid.Signups.Add((user, new RaidSignup(_next++, user, name, "blackrock", className, role, status, note, alt, user == Me)));

    private RaidEvent Event(Sample r)
    {
        int Order(RaidSignup s) => Array.IndexOf(RaidexCalendar.Roles, s.Role);
        var mains = r.Signups.Where(s => !s.Signup.IsAlt).Select(s => s.Signup)
            .OrderBy(Order).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var bench = r.Signups.Where(s => s.Signup.IsAlt).GroupBy(s => s.User)
            .Select(g => new RaidBench(g.Key, mains.FirstOrDefault(m => m.UserId == g.Key)?.Name ?? "—",
                g.Select(s => s.Signup).OrderBy(Order).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(b => b.MainName, StringComparer.OrdinalIgnoreCase).ToList();
        return new RaidEvent(r.Id, r.Title, r.Instance, r.Difficulty, r.Image, r.StartsAt, r.DurationMin, r.Description,
            r.MaxSize, r.MinSize, r.CreatedBy, r.Rank, mains, bench,
            mains.FirstOrDefault(s => s.Mine), r.Signups.Where(s => s.Signup.IsAlt && s.Signup.Mine).Select(s => s.Signup).ToList(),
            CanManage: true);
    }

    private Sample Find(long id) => _raids.First(r => r.Id == id);

    public bool IsConnected => true;

    public string? ActiveCharacter => "Raidex";

    public event EventHandler? ConnectionChanged
    {
        add { }
        remove { }
    }

    public Task<IReadOnlyList<RaidEvent>> RaidsAsync(bool past)
    {
        var now = DateTimeOffset.Now;
        var raids = past
            ? _raids.Where(r => r.StartsAt.AddMinutes(r.DurationMin) < now).OrderByDescending(r => r.StartsAt)
            : _raids.Where(r => r.StartsAt.AddMinutes(r.DurationMin) >= now).OrderBy(r => r.StartsAt);
        return Task.FromResult<IReadOnlyList<RaidEvent>>(raids.Select(Event).ToList());
    }

    public Task<RaidEvent> CreateAsync(RaidDraft draft)
    {
        var raid = new Sample { Id = _next++, Title = draft.Title, CreatedBy = "Raidex", Rank = RaidRank.RaidLead, CreatedById = Me };
        Apply(raid, draft);
        _raids.Add(raid);
        return Task.FromResult(Event(raid));
    }

    public Task<RaidEvent> UpdateAsync(long id, RaidDraft draft)
    {
        var raid = Find(id);
        Apply(raid, draft);
        return Task.FromResult(Event(raid));
    }

    private static void Apply(Sample raid, RaidDraft d)
    {
        raid.Title = d.Title;
        raid.Instance = d.Instance;
        raid.Difficulty = d.Difficulty;
        raid.StartsAt = d.StartsAt;
        raid.DurationMin = d.DurationMin;
        raid.Description = d.Description;
        raid.MinSize = d.MinSize;
        raid.MaxSize = d.MaxSize;
    }

    public Task DeleteAsync(long id)
    {
        _raids.RemoveAll(r => r.Id == id);
        return Task.CompletedTask;
    }

    public Task<RaidEvent> SignupAsync(long raidId, SignupDraft s)
    {
        var raid = Find(raidId);
        var name = s.Character.Name;
        raid.Signups.RemoveAll(x => x.User == Me && (x.Signup.Name == name || (!s.IsAlt && !x.Signup.IsAlt)));
        raid.Signups.Add((Me, new RaidSignup(_next++, Me, name, "blackrock", s.Character.ClassName, s.Role, s.Status,
            s.Note, s.IsAlt, true)));
        return Task.FromResult(Event(raid));
    }

    public Task<RaidEvent> WithdrawAsync(long raidId)
    {
        var raid = Find(raidId);
        raid.Signups.RemoveAll(x => x.User == Me && !x.Signup.IsAlt);
        return Task.FromResult(Event(raid));
    }

    public Task<RaidEvent> RemoveSignupAsync(long raidId, long signupId)
    {
        var raid = Find(raidId);
        raid.Signups.RemoveAll(x => x.Signup.Id == signupId);
        return Task.FromResult(Event(raid));
    }

    public Task<Uri> ShareAsync(long raidId) => Task.FromResult(new Uri($"https://raidex.app/r/preview{raidId}"));

    public Task<GuideList> GuidesAsync(string instance)
    {
        // Raidex's own four links for every instance, as guides.php builds them.
        var own = new List<RaidGuide>
        {
            new(null, "wowhead", "de", "Wowhead", "https://de.wowhead.com/search?q=" + Uri.EscapeDataString(instance), null, true, null, RaidRank.Member),
            new(null, "buffed", "de", "buffed", "https://www.buffed.de/?s=" + Uri.EscapeDataString(instance), null, true, null, RaidRank.Member),
            new(null, "wowhead", "en", "Wowhead", "https://www.wowhead.com/search?q=" + Uri.EscapeDataString(instance), null, true, null, RaidRank.Member),
            new(null, "method", "en", "Method", "https://www.method.gg/guides", null, true, null, RaidRank.Member),
        };
        var guild = instance == Voidspire ? _guides : [];
        return Task.FromResult(new GuideList([.. own, .. guild], CanManage: true));
    }

    public Task AddGuideAsync(string instance, string title, string url)
    {
        _guides.Add(new RaidGuide(_next++, "custom", null, title, url, null, false, "Raidex", RaidRank.RaidLead));
        return Task.CompletedTask;
    }

    public Task DeleteGuideAsync(long id)
    {
        _guides.RemoveAll(g => g.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JournalInstance>> JournalAsync(bool dungeons) =>
        Task.FromResult<IReadOnlyList<JournalInstance>>(dungeons
            ? [new(1, Say("Mördergasse", "Murder Row"), [], null, "Midnight")]
            : [new(1307, Voidspire, [], null, "Midnight"), new(1314, Dreamrift, [], null, "Midnight"),
               new(1308, "Marsch auf Quel'Danas", [], null, "Midnight")]);

    public Task<IReadOnlyList<AccountCharacter>> CharactersAsync() => Task.FromResult(Account);
}
