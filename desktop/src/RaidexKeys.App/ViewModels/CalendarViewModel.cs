// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.App.Raidex;
using RaidexKeys.Core;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.Rating;
using RaidexKeys.App.Themes;

namespace RaidexKeys.App.ViewModels;

/// <summary>Raidex's words for roles, states, difficulties and ranks, as the calendar says them.</summary>
internal static class CalendarWords
{
    public static string Role(string role) => role switch
    {
        "tank" => Strings.RoleTank,
        "healer" => Strings.RoleHealer,
        _ => Strings.RoleDamage,
    };

    public static GroupRole GroupRoleOf(string role) => role switch
    {
        "tank" => GroupRole.Tank,
        "healer" => GroupRole.Healer,
        _ => GroupRole.Damage,
    };

    public static string Status(string status) => status switch
    {
        "tentative" => Strings.CalendarStatusTentative,
        "absent" => Strings.CalendarStatusAbsent,
        _ => Strings.CalendarStatusConfirmed,
    };

    public static string? Difficulty(string? difficulty) => difficulty switch
    {
        null => null,
        "lfr" => Strings.CalendarDifficultyLfr,
        "normal" => Strings.CalendarDifficultyNormal,
        "heroic" => Strings.CalendarDifficultyHeroic,
        "mythic" => Strings.CalendarDifficultyMythic,
        "mplus" => Strings.CalendarDifficultyMplus,
        _ => difficulty,
    };

    /// <summary>The item quality colours Raidex gives the difficulties.</summary>
    public static string DifficultyColor(string? difficulty) => difficulty switch
    {
        "lfr" => "#1EFF00",
        "normal" => "#0070DD",
        "heroic" => "#A335EE",
        "mythic" or "mplus" => "#FF8000",
        _ => AppThemes.Hex("TextMuted"),
    };

    public static string Rank(RaidRank rank) => rank switch
    {
        RaidRank.GuildMaster => Strings.CalendarRankGuildMaster,
        RaidRank.RaidLead => Strings.CalendarRankRaidLead,
        RaidRank.Quartermaster => Strings.CalendarRankQuartermaster,
        _ => Strings.CalendarRankMember,
    };

    public static string ClassColor(string? className) =>
        RaidexCalendar.ClassOf(className) is { } c ? ClassColors.Hex(c) : AppThemes.Hex("TextBody");

    /// <summary>"in 2 Tagen, 3 Std", "läuft" or "vor 4 Tagen" - from the raid's start and end.</summary>
    public static string Countdown(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        var c = CultureInfo.CurrentCulture;
        if (now >= start && now < end)
        {
            return Strings.CalendarRunning;
        }
        if (start > now)
        {
            var until = start - now;
            return until.TotalDays >= 1
                ? (int)until.TotalDays == 1
                    ? string.Format(c, Strings.CalendarInOneDay, until.Hours)
                    : string.Format(c, Strings.CalendarInDays, (int)until.TotalDays, until.Hours)
                : until.TotalHours >= 1
                    ? string.Format(c, Strings.CalendarInHours, (int)until.TotalHours, until.Minutes)
                    : string.Format(c, Strings.CalendarInMinutes, Math.Max(1, (int)until.TotalMinutes));
        }
        var since = now - end;
        return since.TotalDays >= 1
            ? (int)since.TotalDays == 1
                ? Strings.CalendarAgoOneDay
                : string.Format(c, Strings.CalendarAgoDays, (int)since.TotalDays)
            : since.TotalHours >= 1
                ? string.Format(c, Strings.CalendarAgoHours, (int)since.TotalHours)
                : string.Format(c, Strings.CalendarAgoMinutes, Math.Max(1, (int)since.TotalMinutes));
    }
}

/// <summary>One character in a raid's roster or on its bench.</summary>
public sealed partial class RosterEntry(RaidSignup signup, bool canRemove, Func<RaidSignup, Task> remove) : ViewModelBase
{
    public string Name { get; } = signup.Name;
    public string ClassColor { get; } = CalendarWords.ClassColor(signup.ClassName);
    public GroupRole Role { get; } = CalendarWords.GroupRoleOf(signup.Role);
    public string RoleLabel { get; } = CalendarWords.Role(signup.Role);

    /// <summary>"Vielleicht" or "Abwesend"; null for who comes for sure - the usual case says nothing.</summary>
    public string? StatusLabel { get; } = signup.Status == "confirmed" ? null : CalendarWords.Status(signup.Status);

    public string? Note { get; } = signup.Note;

    /// <summary>Class and realm, for the name's tooltip.</summary>
    public string Tip { get; } = string.Join(" · ", new[] { signup.ClassName, signup.Realm }.Where(s => !string.IsNullOrEmpty(s)));

    public bool Mine { get; } = signup.Mine;
    public bool CanRemove { get; } = canRemove;

    [RelayCommand]
    private Task Remove() => remove(signup);
}

/// <summary>A heading and the characters under it: a role in the roster, or a member's alts on the bench.</summary>
public sealed record RosterGroup(string Label, IReadOnlyList<RosterEntry> Entries);

/// <summary>A raid in the calendar's list, and everything its detail shows.</summary>
public sealed class RaidRowViewModel : ViewModelBase
{
    public RaidRowViewModel(RaidEvent raid, DateTimeOffset now, Func<RaidSignup, Task> remove)
    {
        Raid = raid;
        var c = CultureInfo.CurrentCulture;
        var start = raid.StartsAt.ToLocalTime();
        var end = raid.EndsAt.ToLocalTime();
        DayName = c.DateTimeFormat.GetAbbreviatedDayName(start.DayOfWeek).TrimEnd('.').ToUpper(c);
        DayNumber = start.Day.ToString(c);
        MonthName = c.DateTimeFormat.GetAbbreviatedMonthName(start.Month).TrimEnd('.').ToUpper(c);
        TimeRange = string.Format(c, Strings.CalendarTimeRange, start.ToString("t", c), end.ToString("t", c));
        WhenLong = string.Format(c, Strings.CalendarWhen, start.ToString(Strings.CalendarDateFormat, c), TimeRange);
        Countdown = CalendarWords.Countdown(raid.StartsAt, raid.EndsAt, now);
        IsRunning = now >= raid.StartsAt && now < raid.EndsAt;
        DifficultyLabel = CalendarWords.Difficulty(raid.Difficulty);
        DifficultyColor = CalendarWords.DifficultyColor(raid.Difficulty);
        SubLine = string.Join(" · ", new[] { raid.Instance, DifficultyLabel }.Where(s => !string.IsNullOrEmpty(s)));

        var coming = raid.Coming;
        SizeLabel = raid.MaxSize > 0 ? $"{coming}/{raid.MaxSize}" : coming.ToString(c);
        SizeTip = raid.MaxSize > 0
            ? string.Format(c, Strings.CalendarSizeTipMax, coming, raid.MinSize, raid.MaxSize)
            : string.Format(c, Strings.CalendarSizeTip, coming, raid.MinSize);
        Tanks = raid.Count("tank");
        Healers = raid.Count("healer");
        Damage = raid.Count("dps");

        if (raid.MySignup is { } mine)
        {
            MyLine = string.Join(" · ", mine.Name, CalendarWords.Role(mine.Role), CalendarWords.Status(mine.Status));
            MyStatus = mine.Status;
        }
        CreatedLine = raid.CreatedByName is { } by ? string.Format(c, Strings.CalendarInvitedBy, by) : null;
        RankLabel = CalendarWords.Rank(raid.CreatedByRank).ToUpper(c);

        RosterEntry Entry(RaidSignup s) => new(s, s.Mine || raid.CanManage, remove);
        var roster = new List<RosterGroup>();
        foreach (var (role, label) in new[] { ("tank", Strings.CalendarGroupTanks), ("healer", Strings.CalendarGroupHealers), ("dps", Strings.CalendarGroupDamage) })
        {
            var entries = raid.Roster.Where(s => s.Role == role && s.Status != "absent").Select(Entry).ToList();
            if (entries.Count > 0)
            {
                roster.Add(new RosterGroup(string.Format(c, label, entries.Count), entries));
            }
        }
        var absent = raid.Roster.Where(s => s.Status == "absent").Select(Entry).ToList();
        if (absent.Count > 0)
        {
            roster.Add(new RosterGroup(string.Format(c, Strings.CalendarGroupAbsent, absent.Count), absent));
        }
        Roster = roster;
        RosterLabel = string.Format(c, Strings.CalendarRoster, SizeLabel);
        Bench = raid.Bench
            .Select(b => new RosterGroup(b.MainName, b.Alts.Select(Entry).ToList()))
            .ToList();
        MyAlts = raid.MyAlts.Select(Entry).ToList();
    }

    public RaidEvent Raid { get; }
    public long Id => Raid.Id;
    public string Title => Raid.Title;
    public string? Instance => Raid.Instance;

    /// <summary>"SA" over "13" over "SEP" - the date block of the row.</summary>
    public string DayName { get; }
    public string DayNumber { get; }
    public string MonthName { get; }

    /// <summary>"20:00 – 23:00".</summary>
    public string TimeRange { get; }

    /// <summary>"Samstag, 13. September 2026 · 20:00 – 23:00".</summary>
    public string WhenLong { get; }

    public string Countdown { get; }
    public bool IsRunning { get; }

    /// <summary>"Die Leerenspitze · Heroisch".</summary>
    public string SubLine { get; }

    public string? DifficultyLabel { get; }
    public string DifficultyColor { get; }
    public bool HasDifficulty => DifficultyLabel is not null;
    public string? ImageUrl => Raid.Image;
    public string? Description => Raid.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Raid.Description);

    /// <summary>"14/20", or "14" without a limit.</summary>
    public string SizeLabel { get; }

    public string SizeTip { get; }

    public int Tanks { get; }
    public int Healers { get; }
    public int Damage { get; }

    /// <summary>"Nachtfeder · Heiler · Vielleicht" - the caller's own main in it; null when not signed up.</summary>
    public string? MyLine { get; }

    /// <summary>The caller's state, for the dot in the row; null when not signed up.</summary>
    public string? MyStatus { get; }

    public bool IsSignedUp => MyStatus is not null;
    public bool IsComing => MyStatus is "confirmed";
    public bool IsMaybe => MyStatus is "tentative";
    public bool IsAway => MyStatus is "absent";

    public string? CreatedLine { get; }
    public string RankLabel { get; }
    public bool CanManage => Raid.CanManage;

    public IReadOnlyList<RosterGroup> Roster { get; }
    public bool HasRoster => Roster.Count > 0;
    public string RosterLabel { get; }
    public IReadOnlyList<RosterGroup> Bench { get; }
    public bool HasBench => Bench.Count > 0;
    public IReadOnlyList<RosterEntry> MyAlts { get; }

    public string SignupLabel => IsSignedUp ? Strings.CalendarChangeSignup : Strings.CalendarSignup;
}

/// <summary>A character to sign up with, as the list shows it.</summary>
public sealed record CharacterOption(AccountCharacter Character, string Label, string ClassColor);

/// <summary>The sign-up of one raid, as a card over its roster: main or alt, character, role, state and a note.</summary>
public sealed partial class SignupFormViewModel : ViewModelBase
{
    private readonly RaidRowViewModel _raid;
    private readonly Func<SignupDraft, Task<string?>> _save;
    private readonly Func<Task<string?>> _withdraw;
    private readonly Action _close;

    /// <param name="characters">The caller's characters; null while they are still on their way.</param>
    /// <param name="altMode">Opened again after an alt was added: stay on the bench.</param>
    public SignupFormViewModel(RaidRowViewModel raid, IReadOnlyList<AccountCharacter>? characters, string? activeCharacter,
        Func<SignupDraft, Task<string?>> save, Func<Task<string?>> withdraw, Action close, bool altMode = false)
    {
        _raid = raid;
        _save = save;
        _withdraw = withdraw;
        _close = close;
        var c = CultureInfo.CurrentCulture;
        Characters = (characters ?? [])
            .Select(ch => new CharacterOption(ch,
                string.Format(c, Strings.CalendarCharacterOption, ch.Name, ch.ClassName ?? "?", ch.Level),
                ch.ClassId is { } id && Enum.IsDefined(typeof(WowClass), id) ? ClassColors.Hex((WowClass)id) : CalendarWords.ClassColor(ch.ClassName)))
            .ToList();
        IsLoadingCharacters = characters is null;
        KindIndex = altMode ? 1 : 0;
        var mine = raid.Raid.MySignup;
        Character = (altMode ? null : Characters.FirstOrDefault(o => mine is not null && Same(o.Character, mine)))
            ?? Characters.FirstOrDefault(o => !altMode && string.Equals(o.Character.Name, activeCharacter, StringComparison.OrdinalIgnoreCase))
            ?? Characters.FirstOrDefault(o => altMode && (mine is null || !Same(o.Character, mine)))
            ?? Characters.FirstOrDefault();
        // A new sign-up starts without a role: every player picks tank, healer or damage themselves.
        RoleIndex = Array.IndexOf(RaidexCalendar.Roles, altMode ? null : mine?.Role);
        StatusIndex = Math.Max(0, Array.IndexOf(RaidexCalendar.Statuses, (altMode ? null : mine?.Status) ?? "confirmed"));
        Note = altMode ? null : mine?.Note;
    }

    private static bool Same(AccountCharacter ch, RaidSignup s) =>
        string.Equals(ch.Name, s.Name, StringComparison.OrdinalIgnoreCase)
        && ch.Id.StartsWith(s.Realm + ":", StringComparison.OrdinalIgnoreCase);

    public string Heading => string.Format(CultureInfo.CurrentCulture, Strings.CalendarSignupHeading, _raid.Title);

    public IReadOnlyList<string> Kinds { get; } = [Strings.CalendarKindMain, Strings.CalendarKindAlt];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlt), nameof(KindHint), nameof(SaveLabel), nameof(CanWithdraw))]
    public partial int KindIndex { get; set; }

    public bool IsAlt => KindIndex == 1;

    public string KindHint => IsAlt ? Strings.CalendarKindAltHint : Strings.CalendarKindMainHint;

    public IReadOnlyList<CharacterOption> Characters { get; }

    public bool IsLoadingCharacters { get; }

    public bool HasNoCharacters => !IsLoadingCharacters && Characters.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial CharacterOption? Character { get; set; }

    public IReadOnlyList<string> Roles { get; } = RaidexCalendar.Roles.Select(CalendarWords.Role).ToList();

    /// <summary>-1 until a role is picked; there is no sign-up without one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRole))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial int RoleIndex { get; set; }

    public bool HasRole => RoleIndex >= 0;

    public IReadOnlyList<string> Statuses { get; } = RaidexCalendar.Statuses.Select(CalendarWords.Status).ToList();

    [ObservableProperty]
    public partial int StatusIndex { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial string? Problem { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(WithdrawCommand))]
    public partial bool IsBusy { get; set; }

    public string SaveLabel => IsAlt ? Strings.CalendarAddAlt : _raid.IsSignedUp ? Strings.CalendarUpdateSignup : Strings.CalendarSignup;

    public bool CanWithdraw => !IsAlt && _raid.IsSignedUp;

    public IReadOnlyList<RosterEntry> MyAlts => _raid.MyAlts;
    public bool HasAlts => MyAlts.Count > 0;

    private bool CanSave => !IsBusy && Character is not null && HasRole;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        IsBusy = true;
        Problem = await _save(new SignupDraft(Character!.Character, RaidexCalendar.Roles[RoleIndex],
            RaidexCalendar.Statuses[Math.Max(0, StatusIndex)], IsAlt ? null : Note, IsAlt));
        IsBusy = false;
    }

    private bool CanWithdrawNow => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanWithdrawNow))]
    private async Task Withdraw()
    {
        IsBusy = true;
        Problem = await _withdraw();
        IsBusy = false;
    }

    [RelayCommand]
    private void Close() => _close();
}

/// <summary>A raid being planned or changed: the card that stands in for its detail meanwhile.</summary>
public sealed partial class RaidFormViewModel : ViewModelBase
{
    private static readonly string[] RaidDifficulties = ["lfr", "normal", "heroic", "mythic"];
    private static readonly string[] DungeonDifficulties = ["normal", "heroic", "mythic", "mplus"];

    private readonly Func<RaidDraft, Task<string?>> _save;
    private readonly Action _close;
    private readonly Func<bool, IReadOnlyList<string>> _journal;

    /// <param name="raid">The raid to change; null for a new one.</param>
    /// <param name="journal">The journal's raid names (false) or dungeon names (true) as far as they are loaded.</param>
    public RaidFormViewModel(RaidEvent? raid, Func<bool, IReadOnlyList<string>> journal,
        Func<RaidDraft, Task<string?>> save, Action close)
    {
        _save = save;
        _close = close;
        _journal = journal;
        IsNew = raid is null;
        // A new raid as Raidex's web form starts one: in two days, heroic, 10 to 20.
        var start = raid?.StartsAt.ToLocalTime() ?? DateTimeOffset.Now.Date.AddDays(2).AddHours(20);
        var end = raid?.EndsAt.ToLocalTime() ?? start.AddHours(3);
        TypeIndex = raid?.Difficulty == "mplus" ? 1 : 0;
        Title = raid?.Title;
        Instance = raid?.Instance;
        Date = start.Date;
        StartTime = start.TimeOfDay;
        EndTime = end.TimeOfDay;
        MinSize = raid?.MinSize ?? 10;
        MaxSize = raid?.MaxSize ?? 20;
        Description = raid?.Description;
        DifficultyIndex = Math.Max(0, Array.IndexOf(DifficultyKeys, raid?.Difficulty ?? "heroic"));
    }

    public bool IsNew { get; }

    public string Heading => IsNew ? Strings.CalendarNewRaid : Strings.CalendarEditRaid;
    public string SaveLabel => IsNew ? Strings.CalendarCreate : Strings.CalendarSave;

    public IReadOnlyList<string> Types { get; } = [Strings.CalendarTypeRaid, Strings.CalendarTypeDungeon];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Difficulties), nameof(InstanceNames), nameof(InstanceLabel), nameof(InstancePlaceholder))]
    public partial int TypeIndex { get; set; }

    partial void OnTypeIndexChanged(int value) => DifficultyIndex = Math.Min(2, Difficulties.Count - 1);

    private string[] DifficultyKeys => TypeIndex == 1 ? DungeonDifficulties : RaidDifficulties;

    public IReadOnlyList<string> Difficulties => DifficultyKeys.Select(d => CalendarWords.Difficulty(d)!).ToList();

    [ObservableProperty]
    public partial int DifficultyIndex { get; set; }

    public IReadOnlyList<string> InstanceNames => _journal(TypeIndex == 1);

    /// <summary>The journal arrived: the field's suggestions anew.</summary>
    public void JournalLoaded() => OnPropertyChanged(nameof(InstanceNames));

    public string InstanceLabel => TypeIndex == 1 ? Strings.CalendarFieldDungeon : Strings.CalendarFieldRaid;
    public string InstancePlaceholder => TypeIndex == 1 ? Strings.CalendarDungeonPlaceholder : Strings.CalendarRaidPlaceholder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Instance { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial TimeSpan? StartTime { get; set; }

    [ObservableProperty]
    public partial TimeSpan? EndTime { get; set; }

    [ObservableProperty]
    public partial decimal? MinSize { get; set; }

    [ObservableProperty]
    public partial decimal? MaxSize { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial string? Problem { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    private bool CanSave => !IsBusy && !string.IsNullOrWhiteSpace(Title) && Date is not null && StartTime is not null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        var local = Date!.Value.Date + StartTime!.Value;
        var start = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        // The end is a time of day; one before the start lies after midnight, as on Raidex.
        var minutes = EndTime is { } end ? (int)(end - StartTime.Value).TotalMinutes : 0;
        if (minutes < 0)
        {
            minutes += 24 * 60;
        }
        IsBusy = true;
        Problem = await _save(new RaidDraft(Title!.Trim(), string.IsNullOrWhiteSpace(Instance) ? null : Instance.Trim(),
            DifficultyKeys[Math.Clamp(DifficultyIndex, 0, DifficultyKeys.Length - 1)], start, minutes == 0 ? 120 : minutes,
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Math.Max(1, (int)(MinSize ?? 1)), Math.Max(0, (int)(MaxSize ?? 0))));
        IsBusy = false;
    }

    [RelayCommand]
    private void Cancel() => _close();
}

/// <summary>A guide in the Guides tab: a link with its site's icon.</summary>
public sealed partial class GuideItemViewModel(RaidGuide guide, bool canDelete, Func<RaidGuide, Task> delete) : ViewModelBase
{
    public string Title { get; } = guide.Title;

    /// <summary>"DE" or "EN"; null when the guide names no language.</summary>
    public string? LangLabel { get; } = guide.Lang?.ToUpperInvariant();

    public bool HasLang => LangLabel is not null;
    public string Host { get; } = guide.Host;
    public string Url { get; } = guide.Url;
    public string? IconUrl { get; } = guide.IconUrl;
    public bool HasIcon => IconUrl is not null;
    public bool CanDelete { get; } = canDelete && !guide.Global && guide.Id is not null;

    [RelayCommand]
    private Task Open() => Web.Browser.OpenAsync(Url);

    [RelayCommand]
    private Task Delete() => delete(guide);
}

/// <summary>The guides one author added, or Raidex's own.</summary>
public sealed record GuideGroupViewModel(string Heading, string? RankLabel, IReadOnlyList<GuideItemViewModel> Guides)
{
    public bool HasRank => RankLabel is not null;
}

/// <summary>
/// "Raidkalender": the raids of the guild active in Raidex - who comes, and
/// signing up - on one tab, the guides of their instances on the other. Only
/// with a Raidex connection; without one the sidebar greys the entry out.
/// </summary>
public sealed partial class CalendarViewModel : SectionViewModel
{
    private readonly ICalendarBackend _backend;
    private IReadOnlyList<AccountCharacter>? _characters;
    private readonly Dictionary<bool, IReadOnlyList<string>> _journal = [];
    private DateTimeOffset? _loadedAt;
    private bool _isShown;

    public CalendarViewModel(ICalendarBackend backend) : base(ShellViewModel.CalendarKey, Strings.NavCalendar)
    {
        _backend = backend;
        // The page lives as long as the window, and so does the backend.
        backend.ConnectionChanged += (_, _) => OnConnectionChanged();
        FollowConnection();
    }

    private bool _auto;

    /// <summary>Until a source is chosen, Raidex while connected, else the game's calendar.</summary>
    private void FollowConnection()
    {
        _auto = true;
        SourceIndex = AppSettings.Load().CalendarSource switch
        {
            "raidex" => 0,
            "wow" => 1,
            _ => _backend.IsConnected ? 0 : 1,
        };
        _auto = false;
    }

    private bool Connected => _backend.IsConnected;

    // ===== Raidex or the game's calendar =====

    /// <summary>The guild raids of the game's calendar, from the addon's file.</summary>
    public GameCalendarViewModel Game { get; } = new();

    public IReadOnlyList<string> Sources { get; } = [Strings.CalendarSourceRaidex, Strings.CalendarSourceGame];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRaidex), nameof(IsGame), nameof(ShowsRaidex), nameof(ShowsRaidexHint), nameof(Intro))]
    public partial int SourceIndex { get; set; }

    partial void OnSourceIndexChanged(int value)
    {
        var settings = AppSettings.Load();
        var source = value == 0 ? "raidex" : "wow";
        if (!_auto && settings.CalendarSource != source)
        {
            settings.CalendarSource = source;
            settings.Save();
        }
        if (value == 0 && _isShown && Connected && _loadedAt is null)
        {
            _ = LoadAsync();
        }
    }

    public bool IsRaidex => SourceIndex == 0;
    public bool IsGame => SourceIndex == 1;

    /// <summary>Raidex chosen and connected: its raids and guides.</summary>
    public bool ShowsRaidex => IsRaidex && Connected;

    /// <summary>Raidex chosen, not connected: the way to connect.</summary>
    public bool ShowsRaidexHint => IsRaidex && !Connected;

    public string Intro => IsRaidex ? Strings.CalendarIntro : Strings.CalendarIntroGame;

    private void OnConnectionChanged()
    {
        FollowConnection();
        OnPropertyChanged(nameof(ShowsRaidex));
        OnPropertyChanged(nameof(ShowsRaidexHint));
        if (!Connected)
        {
            // Another account may connect next: nothing of this one stays.
            _loadedAt = null;
            _characters = null;
            Raids = [];
            SelectedRaid = null;
            Form = null;
            GuideInstances = [];
            GuideInstance = null;
            GuideGroups = [];
            Problem = null;
        }
        else if (_isShown && IsRaidex && _loadedAt is null)
        {
            _ = LoadAsync();
        }
    }

    /// <summary>The page came into view: its raids, unless they are fresh.</summary>
    public void Shown(bool shown)
    {
        _isShown = shown;
        if (shown && Connected && IsRaidex && (_loadedAt is null || DateTimeOffset.Now - _loadedAt > TimeSpan.FromMinutes(2)))
        {
            _ = LoadAsync();
        }
    }

    // ===== Tabs =====

    public IReadOnlyList<string> Tabs { get; } = [Strings.CalendarTabDates, Strings.CalendarTabGuides];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDates), nameof(ShowsGuides))]
    public partial int TabIndex { get; set; }

    public bool ShowsDates => TabIndex == 0;
    public bool ShowsGuides => TabIndex == 1;

    partial void OnTabIndexChanged(int value)
    {
        if (value == 1)
        {
            _ = LoadJournalAsync();
            GuideInstance ??= GuideInstances.FirstOrDefault();
        }
    }

    // ===== The raids =====

    public IReadOnlyList<string> Scopes { get; } = [Strings.CalendarUpcoming, Strings.CalendarPast];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoRaidsText))]
    public partial int ScopeIndex { get; set; }

    partial void OnScopeIndexChanged(int value)
    {
        SelectedRaid = null;
        _ = LoadAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRaids), nameof(ShowsNoRaids), nameof(ShowsNothingSelected))]
    public partial IReadOnlyList<RaidRowViewModel> Raids { get; set; } = [];

    public bool HasRaids => Raids.Count > 0;

    public bool ShowsNoRaids => !HasRaids && !IsLoading && Problem is null && _loadedAt is not null;

    public string NoRaidsText => ScopeIndex == 0 ? Strings.CalendarNoUpcoming : Strings.CalendarNoPast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDetail), nameof(ShowsNothingSelected))]
    public partial RaidRowViewModel? SelectedRaid { get; set; }

    partial void OnSelectedRaidChanged(RaidRowViewModel? value)
    {
        // A sign-up, a confirmation or a note belongs to the raid it was opened for.
        if (Signup is not null && value?.Id != _signupRaidId)
        {
            Signup = null;
        }
        ConfirmsDelete = false;
        Notice = null;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoRaids))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem), nameof(ShowsNoRaids))]
    public partial string? Problem { get; set; }

    public bool HasProblem => Problem is not null;

    /// <summary>What just happened and needs no answer: "Link kopiert".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; set; }

    public bool HasNotice => Notice is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDetail), nameof(ShowsNothingSelected), nameof(HasForm))]
    public partial RaidFormViewModel? Form { get; set; }

    public bool HasForm => Form is not null;

    public bool ShowsDetail => SelectedRaid is not null && Form is null;

    public bool ShowsNothingSelected => SelectedRaid is null && Form is null && HasRaids;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSignup))]
    public partial SignupFormViewModel? Signup { get; set; }

    public bool HasSignup => Signup is not null;

    private long? _signupRaidId;

    [ObservableProperty]
    public partial bool ConfirmsDelete { get; set; }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    private async Task LoadAsync()
    {
        if (!Connected || IsLoading)
        {
            return;
        }
        IsLoading = true;
        var keep = SelectedRaid?.Id;
        try
        {
            var raids = await _backend.RaidsAsync(ScopeIndex == 1);
            _loadedAt = DateTimeOffset.Now;
            Problem = null;
            Show(raids, keep);
            if (ScopeIndex == 0)
            {
                GuideInstancesFrom(raids);
            }
        }
        catch (RaidexException e)
        {
            Problem = RaidexAccount.Describe(e);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Show(IReadOnlyList<RaidEvent> raids, long? keep)
    {
        var now = DateTimeOffset.Now;
        Raids = raids.Select(r => new RaidRowViewModel(r, now, RemoveSignupAsync)).ToList();
        SelectedRaid = Raids.FirstOrDefault(r => r.Id == keep) ?? Raids.FirstOrDefault();
    }

    /// <summary>A raid as Raidex handed it back after a change: in its place in the list, still selected.</summary>
    private void Replace(RaidEvent raid)
    {
        var row = new RaidRowViewModel(raid, DateTimeOffset.Now, RemoveSignupAsync);
        var found = Raids.Any(r => r.Id == raid.Id);
        Raids = found
            ? Raids.Select(r => r.Id == raid.Id ? row : r).ToList()
            : Raids.Append(row).OrderBy(r => r.Raid.StartsAt).ToList();
        _signupRaidId = raid.Id;
        SelectedRaid = row;
    }

    /// <summary>Runs a change; what went wrong comes back as the line to show.</summary>
    private static async Task<string?> Attempt(Func<Task> call)
    {
        try
        {
            await call();
            return null;
        }
        catch (RaidexException e)
        {
            return RaidexAccount.Describe(e);
        }
    }

    // ===== Signing up =====

    [RelayCommand]
    private Task OpenSignup() => OpenSignupFor(SelectedRaid, altMode: false);

    private async Task OpenSignupFor(RaidRowViewModel? raid, bool altMode)
    {
        if (raid is null)
        {
            return;
        }
        ConfirmsDelete = false;
        _signupRaidId = raid.Id;
        Signup = NewSignup(raid, altMode);
        if (_characters is null)
        {
            try
            {
                _characters = await _backend.CharactersAsync();
            }
            catch (RaidexException e)
            {
                _characters = null;
                if (Signup is not null && SelectedRaid?.Id == raid.Id)
                {
                    Signup = NewSignup(raid, altMode, []);
                    Signup.Problem = RaidexAccount.Describe(e);
                }
                return;
            }
            if (Signup is not null && SelectedRaid?.Id == raid.Id)
            {
                Signup = NewSignup(raid, altMode);
            }
        }
    }

    private SignupFormViewModel NewSignup(RaidRowViewModel raid, bool altMode, IReadOnlyList<AccountCharacter>? characters = null) =>
        new(raid, characters ?? _characters, _backend.ActiveCharacter,
            async draft =>
            {
                RaidEvent? changed = null;
                var problem = await Attempt(async () => changed = await _backend.SignupAsync(raid.Id, draft));
                if (changed is not null)
                {
                    Replace(changed);
                    // An alt added keeps the card open for the next one, as on Raidex.
                    Signup = draft.IsAlt ? NewSignup(SelectedRaid!, altMode: true) : null;
                }
                return problem;
            },
            async () =>
            {
                RaidEvent? changed = null;
                var problem = await Attempt(async () => changed = await _backend.WithdrawAsync(raid.Id));
                if (changed is not null)
                {
                    Replace(changed);
                    Signup = null;
                }
                return problem;
            },
            () => Signup = null,
            altMode);

    private async Task RemoveSignupAsync(RaidSignup signup)
    {
        if (SelectedRaid is not { } raid)
        {
            return;
        }
        RaidEvent? changed = null;
        Problem = await Attempt(async () => changed = await _backend.RemoveSignupAsync(raid.Id, signup.Id));
        if (changed is not null)
        {
            var reopen = Signup is not null;
            var alt = Signup?.IsAlt ?? false;
            Replace(changed);
            if (reopen)
            {
                Signup = NewSignup(SelectedRaid!, alt);
            }
        }
    }

    // ===== Sharing =====

    [RelayCommand]
    private async Task Share()
    {
        if (SelectedRaid is not { } raid)
        {
            return;
        }
        Uri? link = null;
        Problem = await Attempt(async () => link = await _backend.ShareAsync(raid.Id));
        if (link is not null)
        {
            Notice = await Web.Clipboard.SetTextAsync(link.ToString())
                ? string.Format(CultureInfo.CurrentCulture, Strings.CalendarLinkCopied, link)
                : link.ToString();
        }
    }

    [RelayCommand]
    private void ShowGuides()
    {
        if (SelectedRaid?.Instance is { } instance)
        {
            TabIndex = 1;
            GuideInstance = instance;
        }
    }

    // ===== Planning, changing, deleting =====

    [RelayCommand]
    private Task NewRaid() => OpenForm(null);

    [RelayCommand]
    private Task EditRaid() => OpenForm(SelectedRaid?.Raid);

    private async Task OpenForm(RaidEvent? raid)
    {
        Signup = null;
        ConfirmsDelete = false;
        Form = new RaidFormViewModel(raid, dungeons => _journal.GetValueOrDefault(dungeons) ?? [],
            async draft =>
            {
                RaidEvent? saved = null;
                var problem = await Attempt(async () => saved = raid is null
                    ? await _backend.CreateAsync(draft)
                    : await _backend.UpdateAsync(raid.Id, draft));
                if (saved is not null)
                {
                    Form = null;
                    if (ScopeIndex != 0 && saved.EndsAt >= DateTimeOffset.Now)
                    {
                        ScopeIndex = 0;
                    }
                    Replace(saved);
                }
                return problem;
            },
            () => Form = null);
        await LoadJournalAsync();
    }

    [RelayCommand]
    private void DeleteRaid() => ConfirmsDelete = true;

    [RelayCommand]
    private void CancelDelete() => ConfirmsDelete = false;

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (SelectedRaid is not { } raid)
        {
            return;
        }
        Problem = await Attempt(() => _backend.DeleteAsync(raid.Id));
        ConfirmsDelete = false;
        if (Problem is null)
        {
            Raids = Raids.Where(r => r.Id != raid.Id).ToList();
            SelectedRaid = Raids.FirstOrDefault();
        }
    }

    /// <summary>Blizzard's journal, raids and dungeons, once per connection - for the form and the guide search.</summary>
    private async Task LoadJournalAsync()
    {
        foreach (var dungeons in new[] { false, true })
        {
            if (_journal.ContainsKey(dungeons))
            {
                continue;
            }
            try
            {
                _journal[dungeons] = (await _backend.JournalAsync(dungeons)).Select(i => i.Name).Distinct().ToList();
            }
            catch (RaidexException)
            {
                // Without the journal the fields take any name typed; nothing to say.
                continue;
            }
        }
        Form?.JournalLoaded();
        JournalNames = _journal.Values.SelectMany(n => n).Distinct().ToList();
    }

    // ===== Guides =====

    /// <summary>The instances of the upcoming raids, soonest first: what the guild is about to play.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGuideInstances))]
    public partial IReadOnlyList<string> GuideInstances { get; set; } = [];

    public bool HasGuideInstances => GuideInstances.Count > 0;

    private void GuideInstancesFrom(IReadOnlyList<RaidEvent> upcoming)
    {
        GuideInstances = upcoming.Select(r => r.Instance).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        SelectedGuideInstance = GuideInstances.FirstOrDefault(i => i == GuideInstance);
    }

    /// <summary>The instance whose guides show - picked in the list, searched, or from a raid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGuideInstance))]
    public partial string? GuideInstance { get; set; }

    public bool HasGuideInstance => GuideInstance is not null;

    partial void OnGuideInstanceChanged(string? value)
    {
        SelectedGuideInstance = GuideInstances.FirstOrDefault(i => i == value);
        _ = LoadGuidesAsync();
    }

    /// <summary>The list's selection; a searched instance outside it leaves the list without one.</summary>
    [ObservableProperty]
    public partial string? SelectedGuideInstance { get; set; }

    partial void OnSelectedGuideInstanceChanged(string? value)
    {
        if (value is not null)
        {
            GuideInstance = value;
        }
    }

    [ObservableProperty]
    public partial IReadOnlyList<string> JournalNames { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchGuidesCommand))]
    public partial string? GuideSearch { get; set; }

    private bool CanSearch => !string.IsNullOrWhiteSpace(GuideSearch);

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void SearchGuides() => GuideInstance = GuideSearch!.Trim();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGuides))]
    public partial IReadOnlyList<GuideGroupViewModel> GuideGroups { get; set; } = [];

    public bool HasGuides => GuideGroups.Count > 0;

    [ObservableProperty]
    public partial bool CanManageGuides { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingGuides { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGuideProblem))]
    public partial string? GuideProblem { get; set; }

    public bool HasGuideProblem => GuideProblem is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGuideCommand))]
    public partial string? NewGuideTitle { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGuideCommand))]
    public partial string? NewGuideUrl { get; set; }

    private async Task LoadGuidesAsync()
    {
        if (GuideInstance is not { } instance || !Connected)
        {
            GuideGroups = [];
            return;
        }
        IsLoadingGuides = true;
        GuideProblem = null;
        try
        {
            var list = await _backend.GuidesAsync(instance);
            if (instance != GuideInstance)
            {
                return;
            }
            CanManageGuides = list.CanManage;
            GuideGroups = list.ByAuthor()
                .Select(g => new GuideGroupViewModel(
                    g.Author is null ? Strings.CalendarGuidesByRaidex
                        : string.Format(CultureInfo.CurrentCulture, Strings.CalendarGuidesBy,
                            g.Author == RaidexCalendar.UnnamedMember ? Strings.CalendarGuidesByMember : g.Author),
                    g.Author is null || g.Rank == RaidRank.Member ? null : CalendarWords.Rank(g.Rank).ToUpper(CultureInfo.CurrentCulture),
                    g.Guides.Select(guide => new GuideItemViewModel(guide, list.CanManage, DeleteGuideAsync)).ToList()))
                .ToList();
        }
        catch (RaidexException e)
        {
            GuideGroups = [];
            GuideProblem = RaidexAccount.Describe(e);
        }
        finally
        {
            IsLoadingGuides = false;
        }
    }

    private bool CanAddGuide => !string.IsNullOrWhiteSpace(NewGuideTitle) && !string.IsNullOrWhiteSpace(NewGuideUrl);

    [RelayCommand(CanExecute = nameof(CanAddGuide))]
    private async Task AddGuide()
    {
        if (GuideInstance is not { } instance)
        {
            return;
        }
        var url = NewGuideUrl!.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            GuideProblem = Strings.CalendarGuideBadUrl;
            return;
        }
        GuideProblem = await Attempt(() => _backend.AddGuideAsync(instance, NewGuideTitle!.Trim(), url));
        if (GuideProblem is null)
        {
            NewGuideTitle = null;
            NewGuideUrl = null;
            await LoadGuidesAsync();
        }
    }

    private async Task DeleteGuideAsync(RaidGuide guide)
    {
        if (guide.Id is not { } id)
        {
            return;
        }
        GuideProblem = await Attempt(() => _backend.DeleteGuideAsync(id));
        if (GuideProblem is null)
        {
            await LoadGuidesAsync();
        }
    }
}
