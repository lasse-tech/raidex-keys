// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.App.Themes;
using RaidexKeys.Core;
using RaidexKeys.Core.Rating;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.App.ViewModels;

/// <summary>One sign-up in a raid of the game's calendar.</summary>
public sealed class GameSignupEntry(GameRaidSignup signup)
{
    public string Name { get; } = signup.Who.Split('-')[0];
    public string Tip { get; } = signup.Who;
    public string ClassColor { get; } = signup.Class is { } c ? ClassColors.Hex(c) : AppThemes.Hex("TextBody");
    public GroupRole Role { get; } = GameCalendarViewModel.RoleOf(signup.Role);
    public string RoleLabel { get; } = signup.Assigned
        ? $"{GameCalendarViewModel.RoleLabel(signup.Role)} · {Strings.GameAssignedTip}"
        : GameCalendarViewModel.RoleLabel(signup.Role);
    public string? ItemLevel { get; } = signup.ItemLevel?.ToString(CultureInfo.CurrentCulture);
    public string? Note { get; } = signup.Note;
}

/// <summary>A heading and the sign-ups under it: one role.</summary>
public sealed record GameSignupGroup(string Label, IReadOnlyList<GameSignupEntry> Entries);

/// <summary>A raid of the game's calendar in the list, and its detail.</summary>
public sealed class GameRaidRowViewModel
{
    public GameRaidRowViewModel(GameRaid raid, DateTimeOffset now, bool showGuild)
    {
        Raid = raid;
        var c = CultureInfo.CurrentCulture;
        var start = raid.At.ToLocalTime();
        DayName = c.DateTimeFormat.GetAbbreviatedDayName(start.DayOfWeek).TrimEnd('.').ToUpper(c);
        DayNumber = start.Day.ToString(c);
        MonthName = c.DateTimeFormat.GetAbbreviatedMonthName(start.Month).TrimEnd('.').ToUpper(c);
        Time = start.ToString("t", c);
        WhenLong = string.Format(c, Strings.CalendarWhen, start.ToString(Strings.CalendarDateFormat, c), Time);
        Countdown = CalendarWords.Countdown(raid.At, raid.At, now);
        var difficulty = raid.DifficultyId switch
        {
            17 => "lfr",
            14 => "normal",
            15 => "heroic",
            16 => "mythic",
            _ => null,
        };
        DifficultyLabel = CalendarWords.Difficulty(difficulty) ?? raid.Difficulty;
        DifficultyColor = CalendarWords.DifficultyColor(difficulty);
        Progress = raid is { Killed: { } killed, Bosses: { } bosses and > 0 }
            ? string.Format(c, Strings.GameProgress, killed, bosses)
            : null;
        SubLine = string.Join(" · ", new[] { raid.Instance, DifficultyLabel }.Where(s => !string.IsNullOrEmpty(s)));
        CreatedLine = raid.Creator is { Length: > 0 } by ? string.Format(c, Strings.CalendarInvitedBy, by) : null;
        Guild = showGuild ? raid.Guild.Split('-')[0] : null;

        Signed = raid.Signups.Count;
        Tanks = raid.Signups.Count(s => s.Role == 'T');
        Healers = raid.Signups.Count(s => s.Role == 'H');
        Damage = Signed - Tanks - Healers;
        RosterLabel = string.Format(c, Strings.GameRoster, Signed);
        Roster = [.. new[] { ('T', Strings.CalendarGroupTanks), ('H', Strings.CalendarGroupHealers), ('D', Strings.CalendarGroupDamage) }
            .Select(g => (g.Item2, Entries: raid.Signups.Where(s => (s.Role is 'T' or 'H' ? s.Role : 'D') == g.Item1)
                .Select(s => new GameSignupEntry(s)).ToList()))
            .Where(g => g.Entries.Count > 0)
            .Select(g => new GameSignupGroup(string.Format(c, g.Item1, g.Entries.Count), g.Entries))];
    }

    public GameRaid Raid { get; }
    public string Title => Raid.Title;
    public string? Instance => Raid.Instance;
    public string DayName { get; }
    public string DayNumber { get; }
    public string MonthName { get; }
    public string Time { get; }
    public string WhenLong { get; }
    public string Countdown { get; }
    public string? DifficultyLabel { get; }
    public bool HasDifficulty => DifficultyLabel is not null;
    public string DifficultyColor { get; }
    public string? Progress { get; }
    public string SubLine { get; }
    public string? CreatedLine { get; }
    public string? Guild { get; }
    public int Signed { get; }
    public int Tanks { get; }
    public int Healers { get; }
    public int Damage { get; }
    public string RosterLabel { get; }
    public IReadOnlyList<GameSignupGroup> Roster { get; }
    public bool HasRoster => Roster.Count > 0;
}

/// <summary>One of the player's characters to sign up with.</summary>
public sealed record GameCharacterOption(AddonCharacter Character, string Label, string ClassColor);

/// <summary>A request noted for a raid, waiting for the character's next login.</summary>
public sealed class GamePendingEntry(SignupRequest request, SignupQueue queue)
{
    public string Label { get; } = request.Action == "withdraw"
        ? string.Format(CultureInfo.CurrentCulture, Strings.GamePendingWithdraw, request.Character.Split('-')[0])
        : string.Format(CultureInfo.CurrentCulture, Strings.GamePendingSignup, request.Character.Split('-')[0],
            GameCalendarViewModel.RoleLabel(request.Role));

    public IRelayCommand RemoveCommand { get; } = new RelayCommand(() => queue.Remove(request.Id));
}

/// <summary>Signing up for one raid through the addon: noted here, applied at the character's next login.</summary>
public sealed partial class GameSignupFormViewModel : ViewModelBase, IDisposable
{
    private readonly GameRaid _raid;
    private readonly SignupQueue _queue;

    public GameSignupFormViewModel(GameRaid raid, IReadOnlyList<AddonCharacter> characters, SignupQueue queue)
    {
        _raid = raid;
        _queue = queue;
        Characters = [.. characters
            .Where(c => c.Guild == raid.Guild && c.Id is not null && c.Source is not null)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new GameCharacterOption(c, c.Name, c.Class is { } k ? ClassColors.Hex(k) : AppThemes.Hex("TextBody")))];
        Character = Characters.FirstOrDefault();
        queue.Changed += OnQueueChanged;
        Pending = BuildPending();
    }

    public IReadOnlyList<GameCharacterOption> Characters { get; }
    public bool HasCharacters => Characters.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedUp))]
    [NotifyCanExecuteChangedFor(nameof(WithdrawCommand))]
    public partial GameCharacterOption? Character { get; set; }

    partial void OnCharacterChanged(GameCharacterOption? value)
    {
        var mine = Mine();
        RoleIndex = mine is null ? RoleIndex : "THD".IndexOf(mine.Role, StringComparison.Ordinal);
        Note = mine?.Note;
    }

    private GameRaidSignup? Mine() => Character?.Character.Id is { } id ? _raid.Signups.FirstOrDefault(s => s.Who == id) : null;

    public bool IsSignedUp => Mine() is not null;

    public IReadOnlyList<string> Roles { get; } = [Strings.RoleTank, Strings.RoleHealer, Strings.RoleDamage];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignupCommand))]
    public partial int RoleIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending))]
    public partial IReadOnlyList<GamePendingEntry> Pending { get; private set; }

    public bool HasPending => Pending.Count > 0;

    private bool CanSignup => Character is not null && RoleIndex is >= 0 and <= 2;

    [RelayCommand(CanExecute = nameof(CanSignup))]
    private void Signup() => Enqueue("signup", "THD"[RoleIndex]);

    [RelayCommand(CanExecute = nameof(IsSignedUp))]
    private void Withdraw() => Enqueue("withdraw", Mine()?.Role ?? 'D');

    private void Enqueue(string action, char role)
    {
        if (Character?.Character is not { Id: { } id, Source: { } file } c)
        {
            return;
        }
        _queue.Add(new SignupRequest(Guid.NewGuid().ToString("N"), file, c.Guid, id, _raid.Guild, _raid.Key,
            _raid.Title, _raid.At, action, role, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(), DateTimeOffset.Now));
    }

    private List<GamePendingEntry> BuildPending() =>
        [.. _queue.Pending.Where(r => r.RaidKey == _raid.Key && r.Guild == _raid.Guild).Select(r => new GamePendingEntry(r, _queue))];

    private void OnQueueChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => Pending = BuildPending());

    public void Dispose() => _queue.Changed -= OnQueueChanged;
}

/// <summary>
/// The guild raids of the game's calendar as the addon saved them at the
/// last logout or /reload. Read only: they are kept up in the game.
/// </summary>
public sealed partial class GameCalendarViewModel : ViewModelBase
{
    private IReadOnlyList<GameRaid> _raids = [];
    private IReadOnlyList<AddonCharacter> _characters = [];
    private DateTimeOffset _now = DateTimeOffset.Now;

    /// <summary>Where sign-ups are noted; null in the preview, which signs up nowhere.</summary>
    public SignupQueue? Queue { get; set; }

    public static GroupRole RoleOf(char role) => role switch
    {
        'T' => GroupRole.Tank,
        'H' => GroupRole.Healer,
        _ => GroupRole.Damage,
    };

    public static string RoleLabel(char role) => role switch
    {
        'T' => Strings.RoleTank,
        'H' => Strings.RoleHealer,
        _ => Strings.RoleDamage,
    };

    /// <summary>A new save of the addon's file; the raid shown stays shown.</summary>
    public void Update(IReadOnlyList<GameRaid> raids, IReadOnlyList<AddonCharacter> characters, DateTimeOffset now)
    {
        _raids = raids;
        _characters = characters;
        _now = now;
        HasAny = raids.Count > 0;
        Show();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoRaids))]
    public partial bool HasAny { get; private set; }

    public IReadOnlyList<string> Scopes { get; } = [Strings.CalendarUpcoming, Strings.CalendarPast];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoRaidsText))]
    public partial int ScopeIndex { get; set; }

    partial void OnScopeIndexChanged(int value) => Show();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRaids), nameof(ShowsNoRaids), nameof(ShowsNothingSelected))]
    public partial IReadOnlyList<GameRaidRowViewModel> Raids { get; private set; } = [];

    public bool HasRaids => Raids.Count > 0;

    public bool ShowsNoRaids => HasAny && !HasRaids;

    public string NoRaidsText => ScopeIndex == 0 ? Strings.CalendarNoUpcoming : Strings.CalendarNoPast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDetail), nameof(ShowsNothingSelected))]
    public partial GameRaidRowViewModel? SelectedRaid { get; set; }

    public bool ShowsDetail => SelectedRaid is not null;

    partial void OnSelectedRaidChanged(GameRaidRowViewModel? value)
    {
        Form?.Dispose();
        Form = value is not null && Queue is not null && value.Raid.At + TimeSpan.FromHours(6) >= _now
            ? new GameSignupFormViewModel(value.Raid, _characters, Queue)
            : null;
    }

    /// <summary>Signing up for the raid shown; null for a past raid and in the preview.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasForm))]
    public partial GameSignupFormViewModel? Form { get; private set; }

    public bool HasForm => Form is not null;

    public bool ShowsNothingSelected => SelectedRaid is null && HasRaids;

    private void Show()
    {
        var keep = SelectedRaid?.Raid.Key;
        var today = new DateTimeOffset(_now.LocalDateTime.Date, _now.Offset);
        var past = ScopeIndex == 1;
        var showGuild = _raids.Select(r => r.Guild).Distinct().Count() > 1;
        var rows = _raids.Where(r => past ? r.At < today : r.At >= today)
            .OrderBy(r => past ? -r.At.ToUnixTimeSeconds() : r.At.ToUnixTimeSeconds())
            .Select(r => new GameRaidRowViewModel(r, _now, showGuild))
            .ToList();
        Raids = rows;
        SelectedRaid = rows.FirstOrDefault(r => r.Raid.Key == keep) ?? rows.FirstOrDefault();
    }
}
