// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Web;
using RaidexKeys.App.Localization;
using RaidexKeys.Core.Raidex;

namespace RaidexKeys.App.ViewModels;

/// <summary>"Keystones": every character of the account with its key.</summary>
/// <param name="asOf">When the newest of them was saved; null for sample data.</param>
public sealed class KeystonesViewModel(IReadOnlyList<KeyRowViewModel> characters, DateTimeOffset? asOf = null)
    : SectionViewModel(ShellViewModel.KeystonesKey, Strings.NavKeystones)
{
    public IReadOnlyList<KeyRowViewModel> Characters { get; } = characters;
    public TableSort<KeyRowViewModel> Table { get; } = new("keystones", characters, KeyRowViewModel.Columns);
    public bool HasData => Characters.Count > 0;

    /// <summary>"4 Charaktere · Stand 11.09.2026 13:29".</summary>
    public string Subtitle
    {
        get
        {
            var count = Characters.Count == 1
                ? Strings.KeystonesSubtitleOne
                : string.Format(CultureInfo.CurrentCulture, Strings.KeystonesSubtitle, Characters.Count);
            return asOf is { } t
                ? count + " · " + string.Format(CultureInfo.CurrentCulture, Strings.AsOf, t.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
                : count;
        }
    }
}

/// <summary>What a pasted route would be stored in Raidex as: the string itself and what the app read from it.</summary>
public sealed record RouteToSave(string Mdt, string Name, string? Uid, int MapId, int Pulls, double Forces);

/// <summary>What a pasted MDT string shows: the route, and a line about it or why there is none.</summary>
public sealed record PastedRoute(RouteRowViewModel? Row, string? Message, string? Detail = null, RouteToSave? Save = null);

/// <summary>
/// "Routen": the player's routes from Mythic Dungeon Tools, per season dungeon;
/// a pasted one, which can be stored in Raidex; and the routes stored there.
/// </summary>
/// <param name="mdtInstalled">False when no MDT was found beside the addon; the empty page says so.</param>
/// <param name="read">Reads a pasted MDT string against the installed MDT; null in the preview.</param>
/// <param name="storedRow">Turns a route stored in Raidex into a row; null in the preview.</param>
public sealed partial class RoutesViewModel : SectionViewModel, IDisposable
{
    private bool _mdtInstalled;
    private Func<string, PastedRoute>? _read;
    private Func<StoredRoute, RouteRowViewModel>? _storedRow;

    public RoutesViewModel(IReadOnlyList<RouteRowViewModel> routes, bool mdtInstalled = true,
        Func<string, PastedRoute>? read = null, Func<StoredRoute, RouteRowViewModel>? storedRow = null)
        : base(ShellViewModel.RoutesKey, Strings.NavRoutes)
    {
        Routes = routes;
        RoutesTable = new TableSort<RouteRowViewModel>("routes", routes, RouteRowViewModel.Columns);
        _mdtInstalled = mdtInstalled;
        _read = read;
        _storedRow = storedRow;
        if (storedRow is not null)
        {
            Account.Changed += OnAccountChanged;
            StoredTable = Stored(BuildStored());
        }
        WireSaves();
    }

    /// <summary>The save button on every route of the player's own MDT, while logged in; and which are saved already.</summary>
    private void WireSaves()
    {
        var mine = Account.Routes.Where(r => r.Mine && r.Uid is not null).Select(r => r.Uid!).ToHashSet(StringComparer.Ordinal);
        foreach (var row in Routes)
        {
            if (_storedRow is null || !IsLoggedIn || row.ToSave is not { } toSave)
            {
                row.SaveCommand = null;
                continue;
            }
            var route = toSave();
            row.IsSaved = route?.Uid is { } uid && mine.Contains(uid);
            row.SaveCommand ??= new AsyncRelayCommand(async () =>
            {
                if (toSave() is { } r && await Account.SaveRouteAsync(r.Mdt, r.Name, r.Uid, r.MapId, r.Pulls, r.Forces, false))
                {
                    row.IsSaved = true;
                }
            });
        }
    }

    public IReadOnlyList<RouteRowViewModel> Routes { get; private set; }
    public TableSort<RouteRowViewModel> RoutesTable { get; private set; }
    public bool HasData => Routes.Count > 0;

    public string Subtitle => Routes.Count == 1
        ? Strings.RoutesSubtitleOne
        : string.Format(CultureInfo.CurrentCulture, Strings.RoutesSubtitleCount, Routes.Count);

    public string EmptyTitle => _mdtInstalled ? Strings.EmptyRoutesTitle : Strings.EmptyMdtTitle;
    public string EmptyBody => _mdtInstalled ? Strings.EmptyRoutesBody : Strings.EmptyMdtBody;

    private static Raidex.RaidexAccount Account => Raidex.RaidexAccount.Instance;

    public bool ShowsRaidex => _storedRow is not null;
    public bool IsLoggedIn => Account.Me is not null;

    // ===== A route from an MDT string =====

    public bool CanPaste => _read is not null;

    [ObservableProperty]
    public partial string? PasteText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasted), nameof(CanSave), nameof(NeedsLogin))]
    [NotifyCanExecuteChangedFor(nameof(SaveToRaidexCommand))]
    public partial PastedRoute? Pasted { get; set; }

    public bool HasPasted => Pasted is not null;

    partial void OnPasteTextChanged(string? value)
    {
        Pasted = string.IsNullOrWhiteSpace(value) || _read is null ? null : _read(value);
        SaveMessage = null;
    }

    [RelayCommand]
    private async Task PasteFromClipboard()
    {
        if (await Clipboard.TextAsync() is { } text)
        {
            PasteText = text.Trim();
        }
    }

    [RelayCommand]
    private void ClearPaste() => PasteText = null;

    /// <summary>"Nur für mich", "Für meine Gilde" - in the order of <see cref="ShareIndex"/>.</summary>
    public IReadOnlyList<string> ShareOptions { get; } = [Strings.PasteShareMe, Strings.PasteShareGuild];

    [ObservableProperty]
    public partial int ShareIndex { get; set; }

    /// <summary>Logged in, with a route worth storing: the save button.</summary>
    public bool CanSave => ShowsRaidex && IsLoggedIn && Pasted?.Save is not null;

    /// <summary>Logged out, with a route worth storing: the way to log in instead.</summary>
    public bool NeedsLogin => ShowsRaidex && !IsLoggedIn && Pasted?.Save is not null;

    [ObservableProperty]
    public partial string? SaveMessage { get; set; }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveToRaidex()
    {
        if (Pasted?.Save is not { } route)
        {
            return;
        }
        var guild = ShareIndex == 1;
        SaveMessage = await Account.SaveRouteAsync(route.Mdt, route.Name, route.Uid, route.MapId, route.Pulls, route.Forces, guild)
            ? guild
                ? string.Format(CultureInfo.CurrentCulture, Strings.PasteSavedGuild, Account.Me?.Guild ?? "")
                : Strings.PasteSavedMe
            : Account.Problem;
    }

    [RelayCommand]
    private Task Login() => Account.LoginAsync();

    // ===== Stored in Raidex =====

    /// <summary>The routes stored in Raidex, sorted as their table's header says; anew with every change of the account.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStored), nameof(StoredHint))]
    public partial TableSort<RouteRowViewModel> StoredTable { get; set; } = Stored([]);

    private static TableSort<RouteRowViewModel> Stored(IReadOnlyList<RouteRowViewModel> rows) =>
        new("storedRoutes", rows, RouteRowViewModel.Columns);

    public bool HasStored => StoredTable.Rows.Count > 0;

    /// <summary>Why the list is empty: logged out, or nothing stored yet; or what went wrong.</summary>
    public string? StoredHint => Account.Problem is { } problem
        ? problem
        : !IsLoggedIn
            ? Strings.StoredLoggedOut
            : StoredTable.Rows.Count == 0 ? Strings.StoredEmpty : null;

    private List<RouteRowViewModel> BuildStored()
    {
        if (_storedRow is null)
        {
            return [];
        }
        // A row open before - its route shared or taken back - stays open.
        var open = StoredTable.Rows.Where(r => r.IsExpanded && r.Stored is not null).Select(r => r.Stored!.Id).ToHashSet();
        var rows = new List<RouteRowViewModel>();
        foreach (var stored in Account.Routes)
        {
            var row = _storedRow(stored);
            row.IsExpanded = open.Contains(stored.Id);
            if (stored.Mine)
            {
                row.ShareCommand = new AsyncRelayCommand(() => Account.ShareRouteAsync(stored.Id, !stored.SharedWithGuild));
                row.DeleteCommand = new AsyncRelayCommand(async () =>
                {
                    if (!row.ConfirmingDelete)
                    {
                        row.ConfirmingDelete = true;
                        return;
                    }
                    await Account.DeleteRouteAsync(stored.Id);
                });
            }
            rows.Add(row);
        }
        return rows;
    }

    private void OnAccountChanged(object? sender, EventArgs e)
    {
        StoredTable = Stored(BuildStored());
        WireSaves();
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(NeedsLogin));
        OnPropertyChanged(nameof(StoredHint));
        SaveToRaidexCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The routes and MDT of a new save; what is typed and open here stays.</summary>
    public void Adopt(RoutesViewModel fresh)
    {
        _mdtInstalled = fresh._mdtInstalled;
        _read = fresh._read;
        _storedRow = fresh._storedRow;
        Routes = fresh.Routes;
        RoutesTable = fresh.RoutesTable;
        StoredTable = Stored(BuildStored());
        WireSaves();
        OnPropertyChanged(string.Empty);
    }

    public void Dispose() => Account.Changed -= OnAccountChanged;
}

/// <summary>"Gilde": the guild's keys this week. The group planner has its own page.</summary>
/// <param name="guildName">The guild the keys were heard in; null for sample data.</param>
/// <param name="seenGuild">False when no character has been logged in in a guild yet.</param>
public sealed class GuildViewModel(IReadOnlyList<KeyRowViewModel> members, string? guildName = null, bool seenGuild = true)
    : SectionViewModel(ShellViewModel.GuildKey, Strings.NavGuild)
{
    public IReadOnlyList<KeyRowViewModel> Members { get; } = members;
    public TableSort<KeyRowViewModel> Table { get; } = new("guild", members, KeyRowViewModel.Columns);
    public bool HasData => Members.Count > 0;

    public string Subtitle
    {
        get
        {
            var count = Members.Count == 1
                ? Strings.GuildSubtitleOne
                : string.Format(CultureInfo.CurrentCulture, Strings.GuildSubtitle, Members.Count);
            return guildName is null ? count : count + " · " + guildName;
        }
    }

    public string EmptyTitle => seenGuild ? Strings.EmptyGuildNoKeysTitle : Strings.EmptyGuildTitle;
    public string EmptyBody => seenGuild ? Strings.EmptyGuildNoKeysBody : Strings.EmptyGuildBody;
}
