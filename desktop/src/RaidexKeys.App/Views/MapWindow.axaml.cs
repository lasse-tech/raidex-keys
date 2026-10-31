// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using RaidexKeys.App.Localization;
using RaidexKeys.App.ViewModels;
using RaidexKeys.Core;

namespace RaidexKeys.App.Views;

/// <summary>
/// A route's map in a window of its own. One per route: opening it again
/// brings the open one to the front. It opens where the last one was left,
/// in that size, with the route as text shown or not as then.
/// </summary>
public partial class MapWindow : Window
{
    private static readonly Dictionary<string, MapWindow> Open = new(StringComparer.Ordinal);

    private readonly string _key;

    public MapWindow() : this(null)
    {
    }

    private MapWindow(RouteRowViewModel? route)
    {
        InitializeComponent();
        var icon = OperatingSystem.IsWindows() ? "raidex-keys.ico" : "raidex-keys.png";
        Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://RaidexKeys/Assets/{icon}")));
        _key = route is null ? "" : KeyOf(route);
        if (route is null)
        {
            return;
        }
        DataContext = route;
        Title = string.Format(CultureInfo.CurrentCulture, Strings.MapWindowTitle, route.Dungeon, route.Name);

        // A floor picker only for a route over several floors.
        FloorPicker.ItemsSource = route.Floors.Select(f => string.Format(CultureInfo.CurrentCulture, Strings.MapFloor, f.Sublevel)).ToList();
        FloorPicker.IsVisible = route.Floors.Count > 1;
        FloorPicker.SelectionChanged += (_, _) => Map.Floor = route.Floors.ElementAtOrDefault(Math.Max(0, FloorPicker.SelectedIndex));
        FloorPicker.SelectedIndex = 0;
        Map.Floor = route.Floors.FirstOrDefault();

        RouteToggle.IsChecked = AppSettings.Load().MapWindowShowsRoute;
        WindowPlacement.Keep(this, "map");
        Closing += (_, _) => RememberRouteShown();
        Closed += (_, _) => Open.Remove(_key);
    }

    /// <summary>Opens the route's map window, or brings the open one forward.</summary>
    public static void Show(RouteRowViewModel route)
    {
        if (Open.TryGetValue(KeyOf(route), out var open))
        {
            open.Activate();
            return;
        }
        var window = new MapWindow(route);
        Open[window._key] = window;
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null)
        {
            // Owned, it stays above the main window without being modal.
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    private static string KeyOf(RouteRowViewModel route) => route.Dungeon + "\n" + route.Name;

    private void RememberRouteShown()
    {
        var settings = AppSettings.Load();
        settings.MapWindowShowsRoute = RouteToggle.IsChecked == true;
        settings.Save();
    }
}
