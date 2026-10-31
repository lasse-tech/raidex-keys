// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.App.Web;
using RaidexKeys.Core;
using RaidexKeys.Core.SavedVariables;
using RaidexKeys.Core.Updates;
using RaidexKeys.Core.Web;

namespace RaidexKeys.App.ViewModels;

/// <summary>
/// Whether a newer app or addon is out (plan 6.10) - said, never enforced.
/// One check per start, and what comes of it is a line on the About page and,
/// if there is something, a strip above the pages that can be waved away.
/// </summary>
public sealed partial class UpdateStatus : ViewModelBase
{
    private readonly Uri _server;
    private readonly Func<string?> _addOnsFolder;

    public UpdateStatus(Uri server, Func<string?> addOnsFolder)
    {
        _server = server;
        _addOnsFolder = addOnsFolder;
    }

    /// <summary>What the last check found; nothing until it has run.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Line), nameof(Detail), nameof(HasNews), nameof(ShowStrip), nameof(Checked), nameof(GetLabel))]
    public partial Updates? Found { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStrip))]
    public partial bool Dismissed { get; set; }

    /// <summary>The check has run, whatever it found.</summary>
    public bool Checked => Found is not null;

    public bool HasNews => Found?.Any == true;

    /// <summary>The strip over the pages: only with news, and only until waved away.</summary>
    public bool ShowStrip => HasNews && !Dismissed;

    /// <summary>"Raidex Keys 0.14.0 ist da" - or that both are current.</summary>
    public string Line => Found switch
    {
        null => Strings.UpdateChecking,
        { AddonHere: null } when BuildInfo.Store => Strings.UpdateNoAddon,
        { App: { } app } => string.Format(CultureInfo.CurrentCulture, Strings.UpdateApp, app),
        { Addon: { } addon } => string.Format(CultureInfo.CurrentCulture, Strings.UpdateAddon, addon),
        _ => BuildInfo.Store ? Strings.UpdateAddonCurrent : Strings.UpdateCurrent,
    };

    /// <summary>What to do about it, if anything.</summary>
    public string? Detail => Found switch
    {
        { App: not null, Addon: { } addon } => string.Format(CultureInfo.CurrentCulture, Strings.UpdateAlsoAddon, addon),
        { App: not null } => Strings.UpdateAppHow,
        { Addon: not null } => string.Format(CultureInfo.CurrentCulture, Strings.UpdateAddonHow,
            Found.AddonHere ?? "?"),
        _ => null,
    };

    /// <summary>Where the newer version is; the landing page names it.</summary>
    public string Page => Found?.App is not null
        ? Found.Page ?? "https://raidex.app/keys/#download"
        : WebLinks.AddonCurseForge;

    /// <summary>"Herunterladen" for the app, "Auf CurseForge" for the addon.</summary>
    public string GetLabel => Found?.App is not null ? Strings.UpdateGet : Strings.UpdateCurseForge;

    /// <summary>
    /// Asks once, in the background. Every failure counts as "nothing newer":
    /// being offline is not news, and the app works without ever asking.
    /// </summary>
    public async Task RunAsync()
    {
        var addon = AddonToc.Version(_addOnsFolder());
        Found = await UpdateCheck.RunAsync(_server, BuildInfo.Store ? null : BuildInfo.Version, addon);
    }

    [RelayCommand]
    private Task OpenPage() => Browser.OpenAsync(Page);

    [RelayCommand]
    private void Dismiss() => Dismissed = true;
}
