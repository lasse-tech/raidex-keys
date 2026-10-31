// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.Core;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.App.ViewModels;

/// <summary>A monospace font in the settings' list: its label, its name and the family to show it in.</summary>
public sealed record MonoFontOption(string Label, string Name, Avalonia.Media.FontFamily Family)
{
    public string Sample => "2037 · +12 · 28:20 · 99,39 %";
}

/// <summary>
/// "Einstellungen", on tabs (Lars, 2026-09-13): Allgemein - language and
/// font; Raidex - the connection; Planung - rating goal and group planner;
/// AddOns-Ordner - where the addon's data is read.
/// </summary>
public sealed partial class SettingsViewModel : SectionViewModel
{
    public const string GeneralTab = "general";
    public const string RaidexTab = "raidex";
    public const string PlanningTab = "planning";
    public const string FolderTab = "folder";

    private readonly AddonDataSource? _source;

    /// <summary>The tabs' keys, in the order the head shows them.</summary>
    private readonly IReadOnlyList<string> _tabKeys;

    /// <param name="source">The addon data; null in the preview, which then hides the Raidex and folder tabs.</param>
    public SettingsViewModel(AddonDataSource? source) : base(ShellViewModel.SettingsKey, Strings.NavSettings)
    {
        _source = source;
        // The account lives as long as the app, and so does this page.
        Raidex.RaidexAccount.Instance.Changed += (_, _) => OnPropertyChanged(string.Empty);
        var settings = AppSettings.Load();
        LanguageIndex = (int)settings.Language;
        PlanLevelsIndex = Math.Clamp(settings.PlanLevelsAbove, 0, PlanLevels.Count - 1);
        AllowanceIndex = Math.Max(0, Array.IndexOf(Allowances, settings.GroupItemLevelAllowance));
        MonoFont = MonoFontOptions.FirstOrDefault(o => o.Name == settings.MonoFont) ?? MonoFontOptions[0];
        Theme = Themes.AppThemes.Current;
        Folder = source is null ? null : new AddOnsFolderViewModel(settings.AddOnsFolder);
        _stored = settings.AddOnsFolder;

        // The preview shows no real account and reads no folder: those two tabs stay away.
        var tabs = new List<(string Key, string Title)> { (GeneralTab, Strings.SettingsTabGeneral) };
        if (source is not null)
        {
            tabs.Add((RaidexTab, Strings.SettingsTabRaidex));
        }
        tabs.Add((PlanningTab, Strings.SettingsTabPlanning));
        if (source is not null)
        {
            tabs.Add((FolderTab, Strings.SettingsTabFolder));
        }
        _tabKeys = tabs.Select(t => t.Key).ToList();
        Tabs = tabs.Select(t => t.Title).ToList();
    }

    /// <summary>The stored folder the field last took over.</summary>
    private string? _stored;

    /// <summary>
    /// Changes one setting on the file as it is now. The first-start dialog
    /// writes the same file, so nothing here keeps a copy of it.
    /// </summary>
    private static AppSettings Update(Action<AppSettings> change)
    {
        var settings = AppSettings.Load();
        change(settings);
        settings.Save();
        return settings;
    }

    // ===== Tabs =====

    public IReadOnlyList<string> Tabs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsGeneral), nameof(ShowsRaidexTab), nameof(ShowsPlanning), nameof(ShowsFolder))]
    public partial int TabIndex { get; set; }

    private string TabKey => _tabKeys[Math.Clamp(TabIndex, 0, _tabKeys.Count - 1)];

    public bool ShowsGeneral => TabKey == GeneralTab;
    public bool ShowsRaidexTab => TabKey == RaidexTab;
    public bool ShowsPlanning => TabKey == PlanningTab;
    public bool ShowsFolder => TabKey == FolderTab;

    /// <summary>Opens a tab by its key - the Raidex tab when the raid calendar sends the user here.</summary>
    public void ShowTab(string key)
    {
        var index = _tabKeys.ToList().IndexOf(key);
        if (index >= 0)
        {
            TabIndex = index;
        }
    }

    // ===== Language =====

    /// <summary>The three choices, in the order of <see cref="UiLanguage"/>.</summary>
    public IReadOnlyList<string> Languages { get; } =
        [Strings.LanguageSystem, Strings.LanguageGerman, Strings.LanguageEnglish];

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    partial void OnLanguageIndexChanged(int value)
    {
        var language = (UiLanguage)value;
        var before = AppSettings.Load().Language;
        if (language == before)
        {
            return;
        }
        Update(s => s.Language = language);
        // "System" can mean the language already shown; then nothing changes.
        if (Language.IsGerman(language) != Language.IsGerman(before))
        {
            Language.Apply(language);
            App.Relaunch(Key);
        }
    }

    // ===== Raidex =====

    private static Raidex.RaidexAccount Account => Raidex.RaidexAccount.Instance;

    /// <summary>Not in the preview: its window shows sample data, nothing of a real account.</summary>
    public bool ShowsRaidex => _source is not null;

    public bool IsLoggedIn => Account.Me is not null;
    public bool IsLoggingIn => Account.IsLoggingIn;
    public bool CanLogIn => !IsLoggedIn && !IsLoggingIn;

    /// <summary>"Verbunden als Testheldin · Testgilde", or what to do while connecting.</summary>
    public string? RaidexStatus => Account.Me is { } me
        ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.RaidexLoggedIn,
            string.Join(" · ", new[] { me.Character, me.Guild }.Where(p => !string.IsNullOrEmpty(p))))
        : Account.IsLoggingIn ? Strings.RaidexLoggingIn : null;

    /// <summary>The code to confirm on the Raidex page, while connecting.</summary>
    public string? ConnectCode => Account.Pending?.UserCode;

    [RelayCommand]
    private Task OpenConnectPage() => Account.OpenConnectPageAsync();

    /// <summary>Raidex's website, for a look at what it is.</summary>
    [RelayCommand]
    private Task VisitRaidex() => Web.Browser.OpenAsync(Account.Server.ToString());

    public string? RaidexProblem => Account.Problem;

    /// <summary>"Keys an Raidex gemeldet: 11.09. 18:40 · 3 übernommen", and whom Raidex refused, and why.</summary>
    public string? RaidexReport
    {
        get
        {
            if (Account.LastReport is not { } report || Account.LastReportAt is not { } at)
            {
                return null;
            }
            var lines = new List<string>
            {
                string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.RaidexLastReport, ViewModels.Show.Moment(at), report.Stored),
            };
            if (report.NotOnAccount.Count > 0)
            {
                lines.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.RaidexRefusedAccount, string.Join(", ", report.NotOnAccount)));
            }
            if (report.NotInGuild.Count > 0)
            {
                lines.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.RaidexRefusedGuild, string.Join(", ", report.NotInGuild)));
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>"Server: raidex.app", and where it comes from when the environment variable chose it.</summary>
    public string RaidexServer => string.Format(System.Globalization.CultureInfo.CurrentCulture,
        AppSettings.ServerFromEnvironment() is null ? Strings.RaidexServer : Strings.RaidexServerFromEnvironment,
        Account.Server.Host, AppSettings.ServerVariable);

    [RelayCommand]
    private Task Login() => Account.LoginAsync();

    [RelayCommand]
    private void CancelLogin() => Account.CancelLogin();

    [RelayCommand]
    private Task Logout() => Account.LogoutAsync();

    // ===== Monospace font =====

    /// <summary>The five fonts, each shown in itself with a line of figures.</summary>
    public IReadOnlyList<MonoFontOption> MonoFontOptions { get; } = Themes.MonoFonts.All
        .Select(name => new MonoFontOption(name == Themes.MonoFonts.Default
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.MonoFontDefault, name)
            : name, name, Themes.MonoFonts.Family(name)))
        .ToList();

    [ObservableProperty]
    public partial MonoFontOption? MonoFont { get; set; }

    partial void OnMonoFontChanged(MonoFontOption? value)
    {
        if (value is null || AppSettings.Load().MonoFont == value.Name)
        {
            return;
        }
        Update(s => s.MonoFont = value.Name);
        Themes.MonoFonts.Apply(value.Name);
    }

    // ===== Colour theme =====

    public IReadOnlyList<Themes.AppTheme> ThemeOptions { get; } = Themes.AppThemes.All;

    [ObservableProperty]
    public partial Themes.AppTheme? Theme { get; set; }

    partial void OnThemeChanged(Themes.AppTheme? value)
    {
        if (value is null || value == Themes.AppThemes.Current)
        {
            return;
        }
        Update(s => s.Theme = value.Key);
        Themes.AppThemes.Apply(value.Key);
    }

    // ===== Goal planning =====

    /// <summary>0 to 3 levels above the highest one timed, in that order.</summary>
    public IReadOnlyList<string> PlanLevels { get; } =
        [Strings.PlanLevels0, Strings.PlanLevels1, Strings.PlanLevels2, Strings.PlanLevels3];

    [ObservableProperty]
    public partial int PlanLevelsIndex { get; set; }

    /// <summary>The goal planning changed; the overview plans anew.</summary>
    public event EventHandler? PlanningChanged;

    partial void OnPlanLevelsIndexChanged(int value)
    {
        if (value < 0 || AppSettings.Load().PlanLevelsAbove == value)
        {
            return;
        }
        Update(s => s.PlanLevelsAbove = value);
        PlanningChanged?.Invoke(this, EventArgs.Empty);
    }

    // ===== The group planner's item level allowance =====

    /// <summary>How far below a key's reward level still counts, in the order of <see cref="AllowanceIndex"/>.</summary>
    private static readonly int[] Allowances = [0, 3, 5, 10];

    public IReadOnlyList<string> AllowanceOptions { get; } = Allowances
        .Select(a => a == 0 ? Strings.AllowanceExact : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.AllowanceBelow, a))
        .ToList();

    [ObservableProperty]
    public partial int AllowanceIndex { get; set; }

    partial void OnAllowanceIndexChanged(int value)
    {
        if (value < 0 || AppSettings.Load().GroupItemLevelAllowance == Allowances[value])
        {
            return;
        }
        Update(s => s.GroupItemLevelAllowance = Allowances[value]);
        PlanningChanged?.Invoke(this, EventArgs.Empty);
    }

    // ===== AddOns folder =====

    /// <summary>The folder field; null in the preview.</summary>
    public AddOnsFolderViewModel? Folder { get; }

    /// <summary>The files read, one per line.</summary>
    public string FilesText => _source is null || _source.Files.Count == 0
        ? Strings.FilesNone
        : string.Join("\n", _source.Files);

    public string? ErrorText => _source?.LastError;

    [ObservableProperty]
    public partial string? SavedText { get; set; }

    [RelayCommand]
    private void ApplyFolder()
    {
        if (_source is null || Folder is null || Folder.Check.AddOnsDir is not { } folder)
        {
            return;
        }
        Update(s => s.AddOnsFolder = folder);
        _stored = folder;
        _source.Start(folder);
        SavedText = Strings.FolderSaved;
    }

    /// <summary>The data source changed: show its files anew.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(FilesText));
        OnPropertyChanged(nameof(ErrorText));
        // A folder stored elsewhere (the first-start dialog) replaces the
        // field's content - but only then, never text being typed.
        var stored = AppSettings.Load().AddOnsFolder;
        if (Folder is not null && stored is not null && stored != _stored)
        {
            Folder.Path = stored;
            _stored = stored;
        }
    }

}
