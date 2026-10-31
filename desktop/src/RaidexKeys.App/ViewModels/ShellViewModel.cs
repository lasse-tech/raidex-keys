// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RaidexKeys.App.Localization;
using RaidexKeys.Core;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.App.ViewModels;

public abstract class ViewModelBase : ObservableObject;

/// <summary>One tab of the window and the page behind it.</summary>
public abstract class SectionViewModel(string key, string title) : ViewModelBase
{
    public string Key { get; } = key;
    public string Title { get; } = title;

    /// <summary>Where the app looked for data and what failed; shown on an empty page.</summary>
    public string? EmptyDetail { get; init; }
}

/// <summary>The window: its tabs and the page shown.</summary>
public sealed partial class ShellViewModel : ViewModelBase, IDisposable
{
    public const string OverviewKey = "overview";
    public const string KeystonesKey = "keystones";
    public const string RoutesKey = "routes";
    public const string GuildKey = "guild";
    public const string PlannerKey = "planner";
    public const string CalendarKey = "calendar";
    public const string SettingsKey = "settings";
    public const string AboutKey = "about";

    private readonly AddonDataSource? _source;
    private readonly SettingsViewModel _settings;

    /// <summary>
    /// The raid calendar lives as long as the window: a new save of the
    /// addon's file says nothing about the calendar, so its raids, tab and
    /// half-filled sign-up stay when the other pages are rebuilt.
    /// </summary>
    private readonly CalendarViewModel _calendar;

    private readonly AboutViewModel _about;

    /// <summary>Sign-ups for the game's calendar, noted here for the addon; live only.</summary>
    private readonly SignupQueue? _signups;

    /// <summary>Whether a newer app or addon is out; asked once at start.</summary>
    public UpdateStatus Updates { get; } = new(AppSettings.Load().RaidexServerUri,
        () => AppSettings.Load().AddOnsFolder);

    /// <summary>The window with fixed content - the brand guide's sample data.</summary>
    public ShellViewModel(string selectedKey, bool isPreview,
        OverviewViewModel? overview = null, KeystonesViewModel? keystones = null,
        RoutesViewModel? routes = null, GuildViewModel? guild = null, GroupPlannerViewModel? planner = null,
        CalendarViewModel? calendar = null)
    {
        IsPreview = isPreview;
        _settings = new SettingsViewModel(source: null);
        _calendar = calendar ?? new CalendarViewModel(new Raidex.LiveCalendar());
        _about = new AboutViewModel(Updates);
        Sections = [.. Build(overview, keystones, routes, guild, planner)];
        Selected = Sections.FirstOrDefault(s => s.Key == selectedKey) ?? Sections[0];
    }

    /// <summary>The window over the addon's data, rebuilt whenever it changes.</summary>
    public ShellViewModel(string selectedKey, AddonDataSource source)
    {
        _source = source;
        _settings = new SettingsViewModel(source);
        _settings.PlanningChanged += (_, _) => Rebuild();
        _calendar = new CalendarViewModel(new Raidex.LiveCalendar());
        _signups = new SignupQueue();
        _calendar.Game.Queue = _signups;
        _signups.Changed += (_, _) => SyncSignups();
        _about = new AboutViewModel(Updates);
        Sections = [.. BuildLive()];
        SyncSignups();
        Selected = Sections.FirstOrDefault(s => s.Key == selectedKey) ?? Sections[0];
        source.Changed += OnSourceChanged;
        Raidex.RaidexAccount.Instance.Changed += OnAccountChanged;
    }

    /// <summary>The window's pages; a new save replaces only those that changed.</summary>
    public ObservableCollection<SectionViewModel> Sections { get; }

    [ObservableProperty]
    public partial SectionViewModel Selected { get; set; }

    partial void OnSelectedChanged(SectionViewModel value) => _calendar?.Shown(value?.Key == CalendarKey);

    /// <summary>True when the window shows the sample data (PreviewData).</summary>
    public bool IsPreview { get; }

    /// <summary>
    /// The "Beispieldaten" pill: in the preview, and in a screenshot only
    /// when it was asked for (<c>--badge</c>) - the software centres' shots
    /// leave it out, the landing page's link preview carries it.
    /// </summary>
    public bool ShowsPreviewBadge => IsPreview && (Program.ScreenshotFile is null || Program.ScreenshotBadge);

    public string PreviewBadge => Strings.PreviewBadge;

    /// <summary>
    /// The host, in the tab bar while the app talks to any Raidex but
    /// raidex.app - so a test server is never mistaken for the real one.
    /// Not in the preview, which talks to none.
    /// </summary>
    public string? ServerBadge => IsPreview || Raidex.RaidexAccount.Instance.Server == new Uri(AppSettings.DefaultRaidexServer)
        ? null
        : Raidex.RaidexAccount.Instance.Server.Host.ToUpperInvariant();

    public bool ShowsServerBadge => ServerBadge is not null;

    /// <summary>The server in full, for the badge's tooltip.</summary>
    public string ServerBadgeTip => string.Format(System.Globalization.CultureInfo.CurrentCulture,
        Strings.ServerBadgeTip, Raidex.RaidexAccount.Instance.Server);

    // Order as in the brand guide's sidebar, now the tab bar; the raid calendar, Raidex's own
    // page, after the pages of the keys and before the settings.
    private IReadOnlyList<SectionViewModel> Build(OverviewViewModel? overview, KeystonesViewModel? keystones,
        RoutesViewModel? routes, GuildViewModel? guild, GroupPlannerViewModel? planner) =>
    [
        overview ?? new OverviewViewModel(),
        keystones ?? new KeystonesViewModel([]),
        routes ?? new RoutesViewModel([]),
        guild ?? new GuildViewModel([], seenGuild: false),
        planner ?? GroupPlannerViewModel.Empty(),
        _calendar,
        _settings,
        _about,
    ];

    private IReadOnlyList<SectionViewModel> BuildLive()
    {
        var data = _source!.Data;
        var now = DateTimeOffset.Now;
        var detail = LiveData.EmptyDetail(_source);
        var levelsAbove = AppSettings.Load().PlanLevelsAbove;
        var mdt = _source.Mdt;
        var board = Raidex.RaidexAccount.Instance.Board;
        _calendar.Game.Update(data.Raids, data.Characters, now);
        return Build(LiveData.Overview(data, now, levelsAbove, mdt, detail), LiveData.Keystones(data, now, detail),
            LiveData.Routes(data, mdt), LiveData.Guild(data, now, board), LiveData.Planner(data, now, board));
    }

    /// <summary>A new save of the addon's file: the pages anew, and the keys to Raidex.</summary>
    private void OnSourceChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        Rebuild();
        SyncSignups();
        Raidex.RaidexAccount.Instance.ReportSoon();
    });

    /// <summary>
    /// The Raidex account changed - its guild board, most often: the guild
    /// page anew, and only that one; the others keep what is open on them.
    /// </summary>
    private void OnAccountChanged(object? sender, EventArgs e)
    {
        if (_source is null)
        {
            return;
        }
        var board = Raidex.RaidexAccount.Instance.Board;
        var now = DateTimeOffset.Now;
        var fresh = new Dictionary<string, SectionViewModel>(StringComparer.Ordinal)
        {
            [GuildKey] = LiveData.Guild(_source.Data, now, board),
            [PlannerKey] = LiveData.Planner(_source.Data, now, board),
        };
        Replace([.. Sections.Select(s => fresh.GetValueOrDefault(s.Key) ?? s)]);
    }

    /// <summary>Brings the addon's files in line with the noted sign-ups, off the UI thread.</summary>
    private void SyncSignups()
    {
        if (_signups is { } queue && _source is { } source)
        {
            var data = source.Data;
            _ = Task.Run(() => queue.Sync(data, DateTimeOffset.Now));
        }
    }

    /// <summary>The pages anew, from the data and settings as they are now; the page shown stays.</summary>
    private void Rebuild()
    {
        Replace(BuildLive());
        _settings.Refresh();
    }

    /// <summary>
    /// Puts in the pages that changed, one by one; the others, the page on
    /// screen among them, keep their view and focus. The routes take the new
    /// data into the page that is there.
    /// </summary>
    private void Replace(IReadOnlyList<SectionViewModel> fresh)
    {
        var key = Selected.Key;
        for (var i = 0; i < fresh.Count; i++)
        {
            var old = Sections[i];
            if (ReferenceEquals(old, fresh[i]))
            {
                continue;
            }
            if (old is RoutesViewModel routes && fresh[i] is RoutesViewModel newRoutes)
            {
                routes.Adopt(newRoutes);
                newRoutes.Dispose();
                continue;
            }
            (old as IDisposable)?.Dispose();
            Sections[i] = fresh[i];
        }
        if (Selected?.Key != key)
        {
            Selected = Sections.First(s => s.Key == key);
        }
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            _source.Changed -= OnSourceChanged;
            Raidex.RaidexAccount.Instance.Changed -= OnAccountChanged;
        }
    }
}
