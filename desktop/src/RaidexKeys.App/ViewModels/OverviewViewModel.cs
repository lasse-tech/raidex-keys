// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.App.Web;
using RaidexKeys.Core;
using RaidexKeys.Core.Rating;
using RaidexKeys.Core.Web;

namespace RaidexKeys.App.ViewModels;

/// <summary>The next run - the keystone in the bags - and the route chosen for it in MDT.</summary>
/// <param name="GainLine">What timing the key adds to the rating (Show.Gain).</param>
public sealed record NextRun(Keystone? Key, RouteSummary? Route, string? GainLine = null)
{
    public string Title => Key?.Dungeon ?? Strings.NoKeystone;
    public string LevelLabel => Key?.LevelLabel ?? "";
    public string? ImageUrl => Key is null ? null : WebLinks.DungeonImage(Key.MapId);
    public string? WowheadUrl => Key is null
        ? null
        : WebLinks.Wowhead(Key.MapId, Key.Dungeon, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de");
    public string? RouteLine => Route is null
        ? null
        : string.Format(CultureInfo.CurrentCulture, Strings.RouteLine, Route.Name, Route.ForcesLabel, Route.Pulls);
}

/// <summary>One run of the goal's plan, as the plan's popup lists it.</summary>
public sealed record GoalRun(string Dungeon, int Level, int Gain)
{
    public string LevelLabel => $"+{Level}";
    public string GainLabel => $"+{Gain}";
}

/// <summary>A rating goal and what reaching it takes.</summary>
/// <param name="Runs">The plan, highest level first.</param>
/// <param name="Suggestion">"Grim Batol +11, Priorat +10 …" - the first runs, for the card.</param>
/// <param name="Progress">0 to 100: the way from the last full hundred to the target.</param>
public sealed record RatingGoal(int Target, IReadOnlyList<GoalRun> Runs, string Suggestion, double Progress)
{
    public string Label => string.Format(CultureInfo.CurrentCulture, Strings.CardGoal, Target);
    public string RunsLabel => Runs.Count == 1
        ? Strings.OneRunNeeded
        : string.Format(CultureInfo.CurrentCulture, Strings.RunsNeeded, Runs.Count);
    public string PlanTitle => string.Format(CultureInfo.CurrentCulture, Strings.PlanTitle, Target);
}

/// <summary>"Diese Woche": reset, score, runs in time, next run, goal, best times.</summary>
public sealed partial class OverviewViewModel : SectionViewModel
{
    public OverviewViewModel() : base(ShellViewModel.OverviewKey, Strings.NavOverview)
    {
    }

    /// <summary>
    /// The goal set per character, for as long as the app runs: the page is
    /// rebuilt whenever the addon's file changes and would forget it.
    /// </summary>
    private static readonly Dictionary<string, int> Targets = new(StringComparer.Ordinal);

    private readonly Func<int, RatingGoal?>? _goalFor;

    /// <param name="goalFor">What a target takes; null when it is out of reach.</param>
    /// <param name="character">Whose week this is; shown after the reset line.</param>
    /// <param name="incomplete">The rating is above 0 but no run is stored (AddonCharacter.IsIncomplete).</param>
    /// <param name="raiderIoUrl">The character on Raider.IO; its name then links there.</param>
    public OverviewViewModel(TimeSpan? untilReset, int score, int inTime, int runs,
        NextRun? nextRun, Func<int, RatingGoal?>? goalFor, IReadOnlyList<DungeonRowViewModel> bests, string? character = null,
        bool incomplete = false, string? raiderIoUrl = null) : this()
    {
        RaiderIoUrl = character is null ? null : raiderIoUrl;
        Incomplete = incomplete;
        UntilReset = untilReset;
        Score = score;
        InTimeLabel = $"{inTime} / {runs}";
        NextRun = nextRun;
        Bests = bests;
        BestsTable = new TableSort<DungeonRowViewModel>("overview", bests, DungeonRowViewModel.Columns);
        Character = character;
        _goalFor = goalFor;
        var target = Character is not null && Targets.TryGetValue(Character, out var set) && set > score
            ? set
            : RatingPlanner.DefaultTarget(score);
        Goal = goalFor?.Invoke(target);
    }

    public bool HasData => Bests.Count > 0;

    public bool Incomplete { get; }

    /// <summary>
    /// Said under the table when no run is stored: "incomplete" when the
    /// rating proves there are runs, "no run yet" only for a rating of 0.
    /// </summary>
    public string? NoRunsHint => !HasData || Character is null || Bests.Any(b => b.HasRun)
        ? null
        : Incomplete
            ? string.Format(CultureInfo.CurrentCulture, Strings.RunsIncomplete, Character, Score)
            : string.Format(CultureInfo.CurrentCulture, Strings.NoRunsYet, Character);

    public TimeSpan? UntilReset { get; }

    public string? Character { get; }

    public string? RaiderIoUrl { get; }

    /// <summary>
    /// "Reset in 3 Tagen, 11 Std · Zarok" - without the name when it is a
    /// link of its own (RaiderIoUrl), then "Reset in 3 Tagen, 11 Std · ".
    /// </summary>
    public string? Subtitle
    {
        get
        {
            string? reset = UntilReset switch
            {
                null => null,
                { Days: 0 } t => string.Format(CultureInfo.CurrentCulture, Strings.ResetInHours, t.Hours),
                { Days: 1 } t => string.Format(CultureInfo.CurrentCulture, Strings.ResetInOneDayHours, t.Hours),
                { } t => string.Format(CultureInfo.CurrentCulture, Strings.ResetInDaysHours, t.Days, t.Hours),
            };
            if (RaiderIoUrl is not null)
            {
                return reset is null ? null : reset + " · ";
            }
            var parts = new[] { reset, Character }.Where(p => !string.IsNullOrEmpty(p)).ToArray();
            return parts.Length == 0 ? null : string.Join(" · ", parts);
        }
    }

    public int Score { get; }
    public string InTimeLabel { get; } = "";
    public NextRun? NextRun { get; }
    public IReadOnlyList<DungeonRowViewModel> Bests { get; } = [];
    public TableSort<DungeonRowViewModel> BestsTable { get; } = new("overview", [], DungeonRowViewModel.Columns);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LowerGoalCommand), nameof(RaiseGoalCommand))]
    public partial RatingGoal? Goal { get; set; }

    [RelayCommand(CanExecute = nameof(CanLowerGoal))]
    private void LowerGoal() => SetTarget(Goal!.Target - 100);

    private bool CanLowerGoal() => Goal is { } g && g.Target - 100 >= RatingPlanner.MinTarget(Score);

    [RelayCommand(CanExecute = nameof(CanRaiseGoal))]
    private void RaiseGoal() => SetTarget(Goal!.Target + 100);

    private bool CanRaiseGoal() => Goal is { } g && _goalFor?.Invoke(g.Target + 100) is not null;

    private void SetTarget(int target)
    {
        if (_goalFor?.Invoke(target) is not { } goal)
        {
            return;
        }
        Goal = goal;
        if (Character is not null)
        {
            Targets[Character] = target;
        }
    }

    [RelayCommand]
    private Task Open(string? url) => Browser.OpenAsync(url);
}
