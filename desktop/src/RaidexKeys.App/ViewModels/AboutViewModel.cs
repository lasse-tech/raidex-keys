// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.App.Web;
using RaidexKeys.Core;

namespace RaidexKeys.App.ViewModels;

/// <summary>
/// "Über", the last tab: what this is, who made it, what it is
/// built on - and whether it and the addon are the current versions.
/// </summary>
public sealed partial class AboutViewModel(UpdateStatus updates)
    : SectionViewModel(ShellViewModel.AboutKey, Strings.NavAbout)
{
    /// <summary>"Version 0.13.0 (Build 123)": version from the .csproj, build number from git.</summary>
    public string VersionLabel =>
        string.Format(CultureInfo.CurrentCulture, Strings.VersionFormat, BuildInfo.Version, BuildInfo.BuildNumber);

    public UpdateStatus Updates { get; } = updates;

    [RelayCommand]
    private Task Open(string? url) => Browser.OpenAsync(url);
}
