// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.App.Web;
using RaidexKeys.Core;
using RaidexKeys.Core.Mdt;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.Rating;
using RaidexKeys.Core.SavedVariables;
using RaidexKeys.Core.Web;
using RaidexKeys.App.Themes;

namespace RaidexKeys.App.ViewModels;

/// <summary>One line of a row's details: a small label and its value.</summary>
public sealed record DetailLine(string Label, string Value);

/// <summary>What the slid-out part of a row shows (Views/RowDetails.axaml).</summary>
public interface IRowDetails
{
    IReadOnlyList<DetailLine> Details { get; }

    /// <summary>The dungeon on Wowhead; null without a dungeon.</summary>
    string? WowheadUrl { get; }

    /// <summary>The row's Raider.IO page - the character, or the dungeon's leaderboard; null without one.</summary>
    string? RaiderIoUrl { get; }

    IAsyncRelayCommand<string?> OpenCommand { get; }

    /// <summary>The details are out: the chevron, or a click anywhere on the row (Views/RowToggle).</summary>
    bool IsExpanded { get; set; }
}

/// <summary>One slot of the Great Vault in a row: "+12" once unlocked, "2/4" until then.</summary>
/// <param name="Level">The slot's keystone level once unlocked; 0 until then.</param>
/// <param name="Progress">The runs towards the slot, up to its threshold.</param>
public sealed record VaultPip(string Label, bool Unlocked, string Tip, int Level = 0, int Progress = 0)
{
    /// <param name="number">1 to 3, as the vault counts its slots.</param>
    public static VaultPip From(int number, VaultSlot slot) => slot.Unlocked && slot.Level > 0
        ? new VaultPip($"+{slot.Level}", true, Show.VaultSlot(number, slot), slot.Level, slot.Threshold)
        : new VaultPip($"{Math.Min(slot.Progress, slot.Threshold)}/{slot.Threshold}", false, Show.VaultSlot(number, slot),
            0, Math.Min(slot.Progress, slot.Threshold));
}

/// <summary>Numbers as the rows show them.</summary>
internal static class Show
{
    /// <summary>28:22 - minutes and seconds, as the game's timer.</summary>
    public static string Clock(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    /// <summary>"30:00 · +2 unter 24:00 · +3 unter 18:00" - the chests at 80 % and 60 % of the timer.</summary>
    public static string Timer(int limitSec) => string.Format(CultureInfo.CurrentCulture, Strings.TimerValue,
        Clock(limitSec), Clock((int)(limitSec * 0.8)), Clock((int)(limitSec * 0.6)));

    /// <summary>"+11 in 28:22 · 1:38 unter der Zeit · 285 Punkte".</summary>
    public static string Run(int level, int durationSec, bool overTime, int? limitSec, double score)
    {
        var parts = new List<string> { string.Format(CultureInfo.CurrentCulture, Strings.RunValue, level, Clock(durationSec)) };
        if (limitSec is { } limit and > 0)
        {
            parts.Add(overTime || durationSec > limit
                ? string.Format(CultureInfo.CurrentCulture, Strings.OverTime, Clock(Math.Max(0, durationSec - limit)))
                : string.Format(CultureInfo.CurrentCulture, Strings.UnderTime, Clock(limit - durationSec)));
        }
        if (score > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Points, Math.Round(score)));
        }
        return string.Join(" · ", parts);
    }

    public static string Moment(DateTimeOffset at) => at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    /// <summary>"99,39 %" or "99.39%": two decimals as MDT shows them, in the reader's number format.</summary>
    public static string Percent(double percent) => (percent / 100).ToString("P2", CultureInfo.CurrentCulture);

    /// <summary>"Platz 1: +12, Gegenstandsstufe 694" or "Platz 2: noch 2 Läufe".</summary>
    public static string VaultSlot(int number, VaultSlot slot) => (slot.Unlocked && slot.Level > 0, slot.ItemLevel, slot.RunsMissing) switch
    {
        (true, { } itemLevel, _) => string.Format(CultureInfo.CurrentCulture, Strings.VaultSlotOpenIlvl, number, slot.Level, itemLevel),
        (true, null, _) => string.Format(CultureInfo.CurrentCulture, Strings.VaultSlotOpen, number, slot.Level),
        (false, _, 1) => string.Format(CultureInfo.CurrentCulture, Strings.VaultSlotRun, number),
        _ => string.Format(CultureInfo.CurrentCulture, Strings.VaultSlotRuns, number, slot.RunsMissing),
    };

    /// <summary>"+11 in der Zeit: +38 bis +53 Punkte" - right on the timer up to 40 % faster.</summary>
    /// <param name="currentBest">The score of the best run in that dungeon so far; 0 for none.</param>
    public static string Gain(int level, double currentBest)
    {
        var (min, max) = KeystoneRating.Gain(level, currentBest);
        return max == 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.GainNone, level)
            : string.Format(CultureInfo.CurrentCulture, Strings.GainValue, level, min, max);
    }
}

/// <summary>
/// A row with a keystone: an own character or a guild member. The dungeon's
/// picture runs in the row; the chevron slides the details out.
/// </summary>
public sealed partial class KeyRowViewModel : ViewModelBase, IRowDetails
{
    /// <summary>The columns of Views/KeyTable, by the names its headers sort with.</summary>
    public static IReadOnlyDictionary<string, SortColumn<KeyRowViewModel>> Columns { get; } = new Dictionary<string, SortColumn<KeyRowViewModel>>
    {
        ["character"] = new(r => r.Name),
        ["dungeon"] = new(r => r.Key?.Dungeon),
        ["key"] = new(r => r.Key?.Level, DescendingFirst: true),
        ["score"] = new(r => r.Rating, DescendingFirst: true),
        ["vault"] = new(r => r.VaultRank, DescendingFirst: true),
    };

    /// <param name="realmName">The realm as the game shows it ("Die ewige Wacht"), or null when only its spaceless form is known.</param>
    /// <param name="spacelessRealm">The realm without spaces, as guild keys carry it ("DieewigeWacht").</param>
    /// <param name="timeLimitSec">The key dungeon's timer, if known.</param>
    /// <param name="details">Lines after the timer: best run, vault, when seen.</param>
    /// <param name="vault">The vault's slots this week, for the own characters; none for guild members.</param>
    /// <param name="rewardWaiting">Last week's vault still holds a reward: nobody logged in since the reset.</param>
    /// <param name="spec">Their specialisation from Raidex's roster, under the name; null where nobody knows it.</param>
    /// <param name="specIcon">Blizzard's icon for it; the drawn role glyph stands in without one.</param>
    /// <param name="role">Tank, healer or damage, for that glyph and the icon's tooltip.</param>
    public KeyRowViewModel(string name, WowClass? wowClass, string region, string? realmName, string spacelessRealm,
        Keystone? key, int rating, int? timeLimitSec, IEnumerable<DetailLine> details,
        IReadOnlyList<VaultPip>? vault = null, bool rewardWaiting = false,
        string? spec = null, string? specIcon = null, GroupRole role = GroupRole.Unknown)
    {
        Spec = spec;
        SpecIcon = specIcon;
        Role = role;
        Name = name;
        Vault = vault ?? [];
        RewardWaiting = rewardWaiting;
        ClassColor = wowClass is { } c ? ClassColors.Hex(c) : AppThemes.Hex("TextBody");
        Realm = realmName ?? spacelessRealm;
        Key = key;
        Rating = rating;

        var german = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        ImageUrl = key is null ? null : WebLinks.DungeonImage(key.MapId);
        WowheadUrl = key is null ? null : WebLinks.Wowhead(key.MapId, key.Dungeon, german);
        // Raider.IO takes the slug and the spaceless form alike, and links on to the armory.
        RaiderIoUrl = WebLinks.RaiderIo(region, realmName is null ? spacelessRealm : WebLinks.RealmSlug(realmName), name);

        var lines = new List<DetailLine>();
        if (key is not null && timeLimitSec is { } limit and > 0)
        {
            lines.Add(new DetailLine(Strings.DetailTimer, Show.Timer(limit)));
        }
        lines.AddRange(details);
        Details = lines;
    }

    public string Name { get; }
    public string Realm { get; }
    public string ClassColor { get; }

    /// <summary>"Wiederherstellung" under the name; null where the roster knows no spec.</summary>
    public string? Spec { get; }

    /// <summary>Blizzard's icon for the spec, off their CDN; null before Raidex knows it.</summary>
    public string? SpecIcon { get; }

    public GroupRole Role { get; }

    public bool HasSpecIcon => SpecIcon is not null;

    /// <summary>The rows carry a role at all: the guild board does, the own characters do not.</summary>
    public bool HasRole => Spec is not null || Role != GroupRole.Unknown;

    /// <summary>"Wiederherstellung · Heiler", for the icon.</summary>
    public string RoleTip => Spec is null
        ? Views.RoleIcon.Label(Role)
        : Spec + " · " + Views.RoleIcon.Label(Role);
    public Keystone? Key { get; }
    public bool HasKey => Key is not null;
    public string DungeonLabel => Key?.Dungeon ?? Strings.NoKeystone;
    public string LevelLabel => Key?.LevelLabel ?? "—";
    public int Rating { get; }
    public string? ImageUrl { get; }
    public string? WowheadUrl { get; }
    public string RaiderIoUrl { get; }
    public IReadOnlyList<DetailLine> Details { get; }
    public IReadOnlyList<VaultPip> Vault { get; }
    public bool RewardWaiting { get; }
    public bool ShowPips => Vault.Count > 0 && !RewardWaiting;

    /// <summary>
    /// How far the vault is this week: slots unlocked, then their levels, then
    /// the runs towards the rest. None this week while last week's reward waits;
    /// null without a vault (the guild's rows).
    /// </summary>
    private IComparable? VaultRank => Vault.Count == 0
        ? null
        : RewardWaiting ? (0, 0, 0) : (Vault.Count(p => p.Unlocked), Vault.Sum(p => p.Level), Vault.Sum(p => p.Progress));

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [RelayCommand]
    private Task Open(string? url) => Browser.OpenAsync(url);
}

/// <summary>A season dungeon on the overview: the character's best run in it, and its details.</summary>
public sealed partial class DungeonRowViewModel : ViewModelBase, IRowDetails
{
    /// <summary>The columns of the overview's table, by the names its headers sort with.</summary>
    public static IReadOnlyDictionary<string, SortColumn<DungeonRowViewModel>> Columns { get; } = new Dictionary<string, SortColumn<DungeonRowViewModel>>
    {
        ["dungeon"] = new(r => r.Dungeon),
        ["time"] = new(r => r.BestSeconds),
        ["key"] = new(r => r.KeyLevel, DescendingFirst: true),
    };

    /// <param name="highestTimed">The highest level the character has timed anywhere: the step offered for a dungeon not run yet.</param>
    public DungeonRowViewModel(int mapId, string dungeon, int? timeLimitSec, int? level, int? durationSec,
        bool overTime, double score, Keystone? ownKey, int highestTimed = 0)
    {
        Dungeon = dungeon;
        KeyLevel = level;
        BestSeconds = durationSec;
        BestTimeLabel = durationSec is { } d ? Show.Clock(d) : "—";
        KeyLabel = level is { } l ? $"+{l}" : "—";
        var german = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        ImageUrl = WebLinks.DungeonImage(mapId);
        WowheadUrl = WebLinks.Wowhead(mapId, dungeon, german);
        RaiderIoUrl = WebLinks.RaiderIoLeaderboard(mapId);

        var lines = new List<DetailLine>();
        if (timeLimitSec is { } limit and > 0)
        {
            lines.Add(new DetailLine(Strings.DetailTimer, Show.Timer(limit)));
        }
        lines.Add(new DetailLine(Strings.DetailBest, level is { } lv && durationSec is { } dur
            ? Show.Run(lv, dur, overTime, timeLimitSec, score)
            : Strings.NotRunYet));
        // The next step: one up from a timed best, the same level again after one over time.
        var next = level switch
        {
            null => Math.Max(KeystoneRating.MinLevel, highestTimed),
            { } run when overTime => run,
            { } run => run + 1,
        };
        lines.Add(new DetailLine(Strings.DetailNextLevel, Show.Gain(next, score)));
        if (ownKey is not null && ownKey.MapId == mapId)
        {
            lines.Add(new DetailLine(Strings.DetailOwnKey, Show.Gain(ownKey.Level, score)));
        }
        Details = lines;
    }

    public string Dungeon { get; }
    public int? KeyLevel { get; }
    public bool HasRun => KeyLevel is not null;
    public int? BestSeconds { get; }
    public string BestTimeLabel { get; }
    public string KeyLabel { get; }
    public string? ImageUrl { get; }
    public string? WowheadUrl { get; }
    public IReadOnlyList<DetailLine> Details { get; }

    /// <summary>The dungeon's leaderboard on Raider.IO: the season's best timed runs.</summary>
    public string? RaiderIoUrl { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [RelayCommand]
    private Task Open(string? url) => Browser.OpenAsync(url);
}

/// <summary>A pull as the map window lists it: its number in the pull's map colour, forces, enemies.</summary>
public sealed record PullLine(int Number, string Color, string Forces, string Enemies);

/// <summary>A route from Mythic Dungeon Tools: its forces, and its pulls in the details.</summary>
public sealed partial class RouteRowViewModel : ViewModelBase, IRowDetails
{
    /// <summary>The columns of the route tables, by the names their headers sort with.</summary>
    public static IReadOnlyDictionary<string, SortColumn<RouteRowViewModel>> Columns { get; } = new Dictionary<string, SortColumn<RouteRowViewModel>>
    {
        ["dungeon"] = new(r => r.Dungeon),
        ["route"] = new(r => r.Name),
        ["pulls"] = new(r => r.Pulls, DescendingFirst: true),
        ["forces"] = new(r => r.Percent, DescendingFirst: true),
    };

    /// <param name="selected">The route chosen in MDT for its dungeon.</param>
    /// <param name="floors">The route on the map, one per floor; none without MDT's map tiles.</param>
    /// <param name="lead">Lines before the pulls - for a stored route, who sees it.</param>
    public RouteRowViewModel(int mapId, string dungeon, string name, bool selected, RouteForces forces,
        IReadOnlyList<FloorMap>? floors = null, IEnumerable<DetailLine>? lead = null)
    {
        Dungeon = dungeon;
        Floors = floors ?? [];
        Name = name;
        Selected = selected;
        Percent = forces.Percent;
        Pulls = forces.Pulls.Count;
        ForcesLabel = Show.Percent(forces.Percent);
        PullsLabel = forces.Pulls.Count == 1
            ? Strings.PullsOne
            : string.Format(CultureInfo.CurrentCulture, Strings.PullsCount, forces.Pulls.Count);
        var german = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        ImageUrl = WebLinks.DungeonImage(mapId);
        WowheadUrl = WebLinks.Wowhead(mapId, dungeon, german);

        PullLines = forces.Pulls.Select(p => new PullLine(p.Number, Views.RouteMapView.PullColor(p.Number),
                string.Format(CultureInfo.CurrentCulture, Strings.PullForces,
                    Show.Percent(100.0 * p.Count / Math.Max(1, forces.DungeonTotal)),
                    Show.Percent(100.0 * p.Cumulative / Math.Max(1, forces.DungeonTotal))),
                p.Enemies.Count == 0
                    ? Strings.PullEmpty
                    : string.Join(", ", p.Enemies.Select(e => string.Format(CultureInfo.CurrentCulture, Strings.EnemyKills, e.Kills, e.Name)))))
            .ToList();
        Missing = forces.Percent < 100 ? Show.Percent(100 - forces.Percent) : null;

        var lines = forces.Pulls.Select(p => new DetailLine(
            string.Format(CultureInfo.CurrentCulture, Strings.PullLabel, p.Number),
            string.Format(CultureInfo.CurrentCulture, Strings.PullValue,
                Show.Percent(100.0 * p.Count / Math.Max(1, forces.DungeonTotal)),
                Show.Percent(100.0 * p.Cumulative / Math.Max(1, forces.DungeonTotal)),
                p.Enemies.Count == 0
                    ? Strings.PullEmpty
                    : string.Join(", ", p.Enemies.Select(e => string.Format(CultureInfo.CurrentCulture, Strings.EnemyKills, e.Kills, e.Name))))))
            .ToList();
        if (forces.Percent < 100)
        {
            lines.Add(new DetailLine(Strings.DetailMissing, Show.Percent(100 - forces.Percent)));
        }
        if (selected)
        {
            lines.Insert(0, new DetailLine(Strings.DetailRouteSelected, Strings.RouteSelectedTip));
        }
        lines.InsertRange(0, lead ?? []);
        Details = lines;
    }

    /// <summary>
    /// A route known only by what Raidex stored with it - when the installed
    /// MDT does not know its dungeon: no map, no pulls, the forces as stored.
    /// </summary>
    public RouteRowViewModel(int mapId, string dungeon, string name, int pulls, double percent, IEnumerable<DetailLine> lead)
    {
        Dungeon = dungeon;
        Name = name;
        Percent = percent;
        Pulls = pulls;
        ForcesLabel = Show.Percent(percent);
        PullsLabel = pulls == 1 ? Strings.PullsOne : string.Format(CultureInfo.CurrentCulture, Strings.PullsCount, pulls);
        ImageUrl = WebLinks.DungeonImage(mapId);
        WowheadUrl = WebLinks.Wowhead(mapId, dungeon, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de");
        Floors = [];
        PullLines = [];
        Details = lead.ToList();
    }

    // ===== Stored in Raidex =====

    /// <summary>The route as Raidex stores it; null for a route from the player's MDT or a pasted one.</summary>
    public StoredRoute? Stored { get; init; }

    public bool IsStoredMine => Stored?.Mine == true;

    public string? ShareLabel => Stored is { } s ? (s.SharedWithGuild ? Strings.StoredUnshare : Strings.StoredShare) : null;

    /// <summary>
    /// The route's MDT string on the clipboard, to paste into MDT in game or
    /// pass on in chat. The app never writes into MDT (plan 5.5), so this is
    /// how a route somebody shared here gets to where it can be run.
    /// </summary>
    /// <summary>
    /// The route as an export string, for one out of the player's own MDT:
    /// built from the preset MDT wrote, with what MDT adds when exporting
    /// (Core/Mdt/MdtString.Export). Null for a route that has no preset.
    /// </summary>
    public Func<string?>? Exported { get; init; }

    public bool CanCopy => Stored is not null || Exported is not null;

    /// <summary>What saving it in Raidex sends, for one out of the player's own MDT; null where there is no preset.</summary>
    public Func<RouteToSave?>? ToSave { get; init; }

    /// <summary>Saves it in Raidex, for the player alone; set by the page while logged in.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial IAsyncRelayCommand? SaveCommand { get; set; }

    /// <summary>Raidex holds a route of the player's with this route's MDT id.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaved { get; set; }

    public bool CanSave => SaveCommand is not null && !IsSaved;

    /// <summary>Read from the player's own MDT: it lives there, and this app writes into no addon.</summary>
    public bool IsFromMdt => Stored is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyLabel))]
    public partial bool JustCopied { get; set; }

    public string CopyLabel => JustCopied ? Strings.StoredCopied : Strings.StoredCopy;

    [RelayCommand]
    private async Task Copy()
    {
        var text = Stored?.Mdt ?? Exported?.Invoke();
        if (!await Clipboard.SetTextAsync(text))
        {
            return;
        }
        JustCopied = true;
        await Task.Delay(TimeSpan.FromSeconds(2));
        JustCopied = false;
    }

    /// <summary>Shares with the guild or takes the route back; set by the page for the owner's routes.</summary>
    public IAsyncRelayCommand? ShareCommand { get; set; }

    /// <summary>Asks first ("Wirklich löschen?"), deletes on the second click; set by the page.</summary>
    public IAsyncRelayCommand? DeleteCommand { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteLabel))]
    public partial bool ConfirmingDelete { get; set; }

    public string DeleteLabel => ConfirmingDelete ? Strings.StoredDeleteConfirm : Strings.StoredDelete;

    public string Dungeon { get; }
    public string Name { get; }
    public bool Selected { get; }
    public double Percent { get; }
    public int Pulls { get; }
    public string ForcesLabel { get; }
    public string PullsLabel { get; }
    public string? ImageUrl { get; }
    public string? WowheadUrl { get; }
    public string? RaiderIoUrl => null;
    public IReadOnlyList<DetailLine> Details { get; }
    public IReadOnlyList<FloorMap> Floors { get; }
    public bool HasMap => Floors.Count > 0;

    /// <summary>The route as text, pull by pull - beside the map in its own window.</summary>
    public IReadOnlyList<PullLine> PullLines { get; }

    /// <summary>"0,61 %" short of 100 %, or null.</summary>
    public string? Missing { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private bool _mapOpen = true;
    private bool _textOpen = true;

    /// <summary>
    /// Whether the open row shows the map. Folding it away leaves the row and
    /// the line with the two buttons standing, and the map itself untouched -
    /// it is only hidden, so the window button still opens it and unfolding
    /// costs nothing.
    /// </summary>
    public bool MapOpen
    {
        get => _mapOpen;
        set => SetProperty(ref _mapOpen, value);
    }

    private bool _arrows = MapOptions.Arrows;

    /// <summary>
    /// Whether the map draws the way round, 1 → 2 → 3. A row keeps its own
    /// choice; the last one made is what the next row and the map window
    /// start with (MapOptions).
    /// </summary>
    public bool Arrows
    {
        get => _arrows;
        set
        {
            if (SetProperty(ref _arrows, value))
            {
                MapOptions.Arrows = value;
            }
        }
    }

    /// <summary>The same fold for the route as text, pull by pull.</summary>
    public bool TextOpen
    {
        get => _textOpen;
        set => SetProperty(ref _textOpen, value);
    }

    [RelayCommand]
    private Task Open(string? url) => Browser.OpenAsync(url);

    /// <summary>The map in a window of its own, sized and placed freely.</summary>
    [RelayCommand]
    private void OpenMapWindow() => Views.MapWindow.Show(this);
}
