// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RaidexKeys.App.ViewModels;
using RaidexKeys.App.Views;

namespace RaidexKeys.App;

/// <summary>Maps each section's view model to its view.</summary>
/// <remarks>A switch rather than reflection on names, so trimming cannot lose a view.</remarks>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        OverviewViewModel => new OverviewView(),
        KeystonesViewModel => new KeystonesView(),
        RoutesViewModel => new RoutesView(),
        GuildViewModel => new GuildView(),
        GroupPlannerViewModel => new PlannerView(),
        CalendarViewModel => new CalendarView(),
        SettingsViewModel => new SettingsView(),
        AboutViewModel => new AboutView(),
        _ => null,
    };

    public bool Match(object? data) => data is SectionViewModel;
}
