// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.Core;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.Rating;
using RaidexKeys.Core.Web;
using RaidexKeys.App.Themes;

namespace RaidexKeys.App.ViewModels;

/// <summary>
/// A guild member the planner may pick: a chip on the Gilde page, with their
/// keystone. What a key would pay them comes from Raidex and is fetched once
/// an evening (<see cref="Season"/>).
/// </summary>
/// <param name="id">"name:realm" as the guild page keys its rows.</param>
/// <param name="dungeon">Their keystone's dungeon, for the row's tooltip.</param>
/// <param name="rating">Their Mythic+ rating this season, as the board has it.</param>
/// <param name="spec">Their current spec from Raidex's roster; null while it knows none.</param>
/// <param name="ownBest">
/// The best score per dungeon from the addon, for a character of this
/// account: first-hand, the same figures the Übersicht shows, and there
/// whether or not Raidex answers. Null for a guild mate.
/// </param>
/// <param name="role">"tank", "healer" or "damage" from Raidex's roster; null while it knows none.</param>
/// <param name="specIcon">Blizzard's icon for that spec; the drawn glyph stands in without one.</param>
public sealed partial class PlannerPick(string id, string name, string realmSlug, WowClass? wowClass,
    int? keyMapId, int? keyLevel, string? dungeon, int rating = 0, string? spec = null,
    IReadOnlyDictionary<int, double>? ownBest = null, string? role = null, string? specIcon = null) : ViewModelBase
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string RealmSlug { get; } = realmSlug;
    public WowClass? Class { get; } = wowClass;
    public int? KeyMapId { get; } = keyMapId;
    public int? KeyLevel { get; } = keyLevel;
    public string ClassColor { get; } = wowClass is { } c ? ClassColors.Hex(c) : AppThemes.Hex("TextBody");

    /// <summary>Their rating as the guild board has it - what the rest of the app shows.</summary>
    public int Rating { get; } = rating;

    /// <summary>"+13" at the end of the row; null without a keystone.</summary>
    public string? KeyLabel => KeyLevel is { } level ? $"+{level}" : null;

    /// <summary>"2894 · Heiler", or the rating alone while Raidex knows no spec.</summary>
    public string Line => spec is { } s
        ? string.Format(CultureInfo.CurrentCulture, Strings.PlannerRatingSpec, Rating, s)
        : Rating.ToString(CultureInfo.CurrentCulture);

    public string Tip => dungeon ?? Strings.NoKeystone;

    /// <summary>Their Mythic+ season as Raidex hands it out; null until it was fetched.</summary>
    public MplusCharacter? Season { get; set; }

    /// <summary>The addon's own best score per dungeon; null for a guild mate.</summary>
    public IReadOnlyDictionary<int, double>? OwnBest { get; } = ownBest;

    /// <summary>Enough is known to weigh a key for them: the addon's runs, or Raidex's.</summary>
    public bool CanBeWeighed => OwnBest is not null || Season is not null;

    /// <summary>What they play: the roster's role, or the one the season came with once fetched.</summary>
    public GroupRole Role => GroupPlannerViewModel.RoleOf(Season?.Role ?? role);

    public string RoleTip => spec is null
        ? Views.RoleIcon.Label(Role)
        : spec + " · " + Views.RoleIcon.Label(Role);

    /// <summary>
    /// Blizzard's icon for their specialisation, off Blizzard's own CDN as the
    /// dungeon pictures are; null before Raidex knows the spec, and then the
    /// drawn role glyph stands in.
    /// </summary>
    public string? SpecIcon => Season?.SpecIcon ?? specIcon;

    public bool HasSpecIcon => SpecIcon is not null;

    [ObservableProperty]
    public partial bool IsPicked { get; set; }
}

/// <summary>One of the five in a proposed group.</summary>
/// <param name="Line">"2544 · Schaden" - rating and role under the name.</param>
/// <param name="Gain">"+54", or "±0" for somebody the run pays nothing on the timer.</param>
/// <param name="Tip">What the run pays right on the timer, and what a good time would.</param>
/// <param name="Light">"GS 308" where the gear is light for this key; null otherwise.</param>
/// <param name="LightTip">What that key wants, for the chip.</param>
public sealed record PlannedSeat(string Name, string ClassColor, string Line, string Gain, string Tip,
    bool Gains, bool HoldsKey, string? Light = null, string? LightTip = null);

/// <summary>A proposed group: the key, who runs it, and what it pays each of them.</summary>
public sealed partial class PlannedGroup : ViewModelBase
{
    /// <param name="floor">The item level this key wants, for the chip beside a light one; 0 when nobody asks.</param>
    public PlannedGroup(GroupProposal proposal, string dungeon, bool german, int floor = 0)
    {
        Dungeon = dungeon;
        LevelLabel = $"+{proposal.Level}";
        ImageUrl = WebLinks.DungeonImage(proposal.MapId);
        WowheadUrl = WebLinks.Wowhead(proposal.MapId, dungeon, german);
        HolderName = proposal.Holder.Name;
        Total = string.Format(CultureInfo.CurrentCulture, Strings.PlannerGain, proposal.Total);
        // The planner weighs a run right on the timer; up to 40 % faster pays
        // another 15 points, which the tiles say in passing.
        int Upside(GroupSeat seat) => KeystoneRating.Gain(proposal.Level, seat.Who.BestIn(proposal.MapId)).Max;
        Seats = proposal.Seats
            .OrderByDescending(s => s.Gain).ThenBy(s => s.Who.Name, StringComparer.CurrentCulture)
            .Select(s => new PlannedSeat(s.Who.Name,
                s.Who.Class is { } c ? ClassColors.Hex(c) : AppThemes.Hex("TextBody"),
                string.Format(CultureInfo.CurrentCulture, Strings.PlannerRatingSpec, s.Who.Rating, RoleLabel(s.Who.Role)),
                s.Gain > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.PlannerGain, s.Gain) : Strings.PlannerGainNone,
                string.Format(CultureInfo.CurrentCulture, Strings.PlannerGainTip, s.Gain, Upside(s)),
                s.Gain > 0,
                s.HoldsKey,
                s.TooLight ? string.Format(CultureInfo.CurrentCulture, Strings.PlannerLight, s.Who.ItemLevel) : null,
                s.TooLight ? string.Format(CultureInfo.CurrentCulture, Strings.PlannerLightTip, proposal.Level, floor) : null))
            .ToList();
        // The group is chosen by its worst-off member, so when that one gains
        // nothing on the timer, the card says who, why, and what a good time
        // would still be worth to them.
        var empty = proposal.Seats.Where(s => s.Gain == 0).ToList();
        Note = (empty.Count, empty.Count == 1 ? Upside(empty[0]) : 0) switch
        {
            (0, _) => null,
            (1, > 0) => string.Format(CultureInfo.CurrentCulture, Strings.PlannerGainsNothingBut,
                empty[0].Who.Name, Upside(empty[0])),
            (1, _) => string.Format(CultureInfo.CurrentCulture, Strings.PlannerGainsNothing, empty[0].Who.Name),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.PlannerGainNothing,
                string.Join(", ", empty.Select(s => s.Who.Name))),
        };
    }

    public string Dungeon { get; }
    public string LevelLabel { get; }

    /// <summary>Who carries the key - the name alone (Lars, 2026-09-12).</summary>
    public string HolderName { get; }

    /// <summary>"+223" - what the five gain together.</summary>
    public string Total { get; }

    /// <summary>Who the run pays nothing, and why; null when everyone gains.</summary>
    public string? Note { get; }

    public string? ImageUrl { get; }
    public string? WowheadUrl { get; }
    public IReadOnlyList<PlannedSeat> Seats { get; }

    [RelayCommand]
    private Task Open(string? url) => Web.Browser.OpenAsync(url);

    private static string RoleLabel(GroupRole role) => role switch
    {
        GroupRole.Tank => Strings.RoleTank,
        GroupRole.Healer => Strings.RoleHealer,
        GroupRole.Damage => Strings.RoleDamage,
        _ => Strings.RoleUnknown,
    };
}

/// <summary>
/// "Gruppenplaner" on the Gilde page (plan 7.2b): the user picks who plays
/// tonight, and every keystone among them is weighed by what its run would
/// pay the five who come. What each of them has run comes from Raidex, once
/// per member and evening.
/// </summary>
public sealed partial class GroupPlannerViewModel : SectionViewModel
{
    /// <summary>How many groups are shown beside the best one.</summary>
    private const int Alternatives = 3;

    private static readonly bool German = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    private readonly Func<int, string> _dungeonName;
    private readonly Func<int, int?> _recommended;
    private readonly Func<PlannerPick, Task<MplusCharacter?>> _lookup;
    private readonly Func<string?> _problem;
    private readonly int _allowance;

    /// <param name="people">Everyone on the guild board, in its order.</param>
    /// <param name="dungeonName">The dungeon of a challenge-mode id, in the game's language.</param>
    /// <param name="recommended">The item level a key level suggests; null while the addon has not read the table.</param>
    /// <param name="isLoggedIn">Without a Raidex account nothing can be fetched, and the card says so.</param>
    /// <param name="lookup">Fetches one member's season; the preview answers from its own sample data.</param>
    /// <param name="problem">
    /// What went wrong on the way to Raidex, if anything - said before the
    /// names it could not fetch, so "Raidex does not know their runs" never
    /// stands there alone for a server that simply did not answer.
    /// </param>
    public GroupPlannerViewModel(IReadOnlyList<PlannerPick> people, Func<int, string> dungeonName,
        Func<int, int?> recommended, bool knowsRewards, bool isLoggedIn,
        Func<PlannerPick, Task<MplusCharacter?>> lookup, Func<string?>? problem = null)
        : base(ShellViewModel.PlannerKey, Strings.NavPlanner)
    {
        People = people;
        _dungeonName = dungeonName;
        _recommended = recommended;
        _lookup = lookup;
        _problem = problem ?? (() => null);
        KnowsRewards = knowsRewards;
        NeedsLogin = !isLoggedIn;

        var settings = AppSettings.Load();
        _allowance = Math.Clamp(settings.GroupItemLevelAllowance, 0, 10);
        BySpec = settings.GroupBySpec;
        ByItemLevel = settings.GroupByItemLevel && knowsRewards;
        foreach (var person in People)
        {
            person.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PlannerPick.IsPicked))
                {
                    OnPropertyChanged(nameof(PickedLabel));
                    PlanCommand.NotifyCanExecuteChanged();
                }
            };
        }
    }

    /// <summary>The page before there is a guild board to pick from.</summary>
    public static GroupPlannerViewModel Empty() =>
        new([], id => $"#{id}", _ => null, knowsRewards: false, isLoggedIn: false,
            _ => Task.FromResult<MplusCharacter?>(null));

    public IReadOnlyList<PlannerPick> People { get; }

    /// <summary>There is somebody to pick; otherwise the page says the board is empty.</summary>
    public bool HasData => People.Count > 0;

    /// <summary>Logged out: the best runs of the others come from Raidex alone.</summary>
    public bool NeedsLogin { get; }

    /// <summary>The addon knows what a key level rewards - what the item level switch needs.</summary>
    public bool KnowsRewards { get; }

    private int Picked => People.Count(p => p.IsPicked);

    /// <summary>"5 von 8 gewählt".</summary>
    public string PickedLabel => string.Format(CultureInfo.CurrentCulture, Strings.PlannerPicked, Picked, People.Count);

    // ===== The two switches, as the settings keep them =====

    [ObservableProperty]
    public partial bool BySpec { get; set; }

    [ObservableProperty]
    public partial bool ByItemLevel { get; set; }

    /// <summary>What the item level switch does - or why it cannot be used.</summary>
    public string ItemLevelHint => KnowsRewards
        ? string.Format(CultureInfo.CurrentCulture, Strings.PlannerByItemLevelHint, _allowance)
        : Strings.PlannerNoRewards;

    partial void OnBySpecChanged(bool value) => Remember(s => s.GroupBySpec = value);

    partial void OnByItemLevelChanged(bool value) => Remember(s => s.GroupByItemLevel = value);

    private static void Remember(Action<AppSettings> change)
    {
        var settings = AppSettings.Load();
        change(settings);
        settings.Save();
    }

    // ===== The plan =====

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlanCommand))]
    public partial bool IsPlanning { get; set; }

    /// <summary>What stands between the picks and a plan, or what the plan left out.</summary>
    [ObservableProperty]
    public partial string? Message { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlan))]
    public partial PlannedGroup? Best { get; set; }

    /// <summary>The other keys among the picked, each with its own best group.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMore))]
    public partial IReadOnlyList<PlannedGroup> More { get; set; } = [];

    public bool HasPlan => Best is not null;

    public bool HasMore => More.Count > 0;

    private bool CanPlan => !IsPlanning && !NeedsLogin && Picked >= GroupPlanner.GroupSize;

    [RelayCommand(CanExecute = nameof(CanPlan))]
    private async Task Plan()
    {
        var picked = People.Where(p => p.IsPicked).ToList();
        IsPlanning = true;
        Message = Strings.PlannerLoading;
        Best = null;
        More = [];
        try
        {
            await FetchAsync(picked.Where(p => p.Season is null).ToList());
            var missing = picked.Where(p => !p.CanBeWeighed).Select(p => p.Name).ToList();
            var candidates = picked.Where(p => p.CanBeWeighed).Select(Candidate).ToList();
            if (candidates.Count < GroupPlanner.GroupSize)
            {
                Message = missing.Count > 0 ? Missing(Strings.PlannerNoSeason, missing) : Strings.PlannerNeedFive;
                return;
            }
            var options = new GroupOptions(BySpec, ByItemLevel, _allowance, _recommended);
            var groups = GroupPlanner.Plan(candidates, options);
            if (groups.Count == 0)
            {
                Message = WhyNot(candidates, options);
                return;
            }
            Best = Group(groups[0]);
            More = groups.Skip(1).Take(Alternatives).Select(Group).ToList();
            Message = missing.Count > 0 ? Missing(Strings.PlannerWithout, missing) : null;
        }
        finally
        {
            IsPlanning = false;
        }
    }

    /// <summary>
    /// Who is left out and why: what went wrong on the way to Raidex first,
    /// then the names. Without a reason it is their season Raidex has none of;
    /// with one it is the server, and saying only the names would blame them.
    /// </summary>
    private string Missing(string names, IReadOnlyList<string> who)
    {
        var line = string.Format(CultureInfo.CurrentCulture, names, string.Join(", ", who));
        return _problem() is { } problem ? problem + "\n" + line : line;
    }

    /// <summary>
    /// The seasons of everyone picked but not yet known, four at a time:
    /// Raidex answers each from its own cache or from Blizzard, and asking for
    /// a whole raid at once helps nobody.
    /// </summary>
    private async Task FetchAsync(IReadOnlyList<PlannerPick> missing)
    {
        if (missing.Count == 0)
        {
            return;
        }
        using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(missing.Select(async person =>
        {
            await gate.WaitAsync();
            try
            {
                person.Season = await _lookup(person);
            }
            finally
            {
                gate.Release();
            }
        }));
    }

    private PlannedGroup Group(GroupProposal proposal) =>
        new(proposal, _dungeonName(proposal.MapId), German, (_recommended(proposal.Level) ?? 0) - _allowance);

    /// <summary>
    /// Why no five of them fit around any of their keys - by trying without
    /// the spec switch, so it is only blamed when it is the one. The item
    /// level cannot be the reason: it leaves nobody out.
    /// </summary>
    private string WhyNot(IReadOnlyList<GroupCandidate> candidates, GroupOptions options)
    {
        if (!candidates.Any(c => c.HasKey))
        {
            return Strings.PlannerNoKey;
        }
        if (options.BySpec && GroupPlanner.Plan(candidates, options with { BySpec = false }).Count > 0)
        {
            return Strings.PlannerNoRoles;
        }
        return Strings.PlannerNoFit;
    }

    /// <summary>
    /// The addon's runs beat Raidex's for a character of this account: they
    /// are first-hand and as fresh as the last logout, where Raidex's come
    /// through Blizzard's profile and its caches. Role and item level only
    /// Raidex knows.
    /// </summary>
    private static GroupCandidate Candidate(PlannerPick person)
    {
        var season = person.Season;
        return new GroupCandidate(person.Id, person.Name, person.RealmSlug, person.Class,
            person.Role, person.Rating, season?.ItemLevel, person.KeyMapId, person.KeyLevel,
            person.OwnBest ?? season!.Scores);
    }

    /// <summary>Raidex's word for a role as the planner counts it.</summary>
    internal static GroupRole RoleOf(string? role) => role switch
    {
        "tank" => GroupRole.Tank,
        "healer" => GroupRole.Healer,
        "damage" => GroupRole.Damage,
        _ => GroupRole.Unknown,
    };
}
