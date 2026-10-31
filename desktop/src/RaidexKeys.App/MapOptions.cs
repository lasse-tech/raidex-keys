// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core;

namespace RaidexKeys.App;

/// <summary>
/// What a route map shows besides the route itself. The choice is the user's
/// last one: a row opened later, and the map in its own window, start the way
/// the last one was left, and it survives a restart.
/// </summary>
public static class MapOptions
{
    private static bool? _arrows;

    /// <summary>Whether the way round is drawn: 1 → 2 → 3 as curved arrows.</summary>
    /// <remarks>
    /// A preview run starts with the arrows drawn - they are part of what the
    /// landing page's shot of a route is for - and leaves the setting alone:
    /// taking a screenshot must not change how the app opens next time.
    /// </remarks>
    public static bool Arrows
    {
        get => _arrows ??= Program.PreviewSection is not null || AppSettings.Load().RouteArrows;
        set
        {
            if (Arrows == value)
            {
                return;
            }
            _arrows = value;
            if (Program.PreviewSection is not null)
            {
                return;
            }
            var settings = AppSettings.Load();
            settings.RouteArrows = value;
            settings.Save();
        }
    }
}
