// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using RaidexKeys.Core;

namespace RaidexKeys.App.Views;

/// <summary>
/// Where a window stood and how large it was, kept beyond the session - the
/// way Iterio (stardust) does it for its main window.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Put back <b>before</b> the window is shown, so it is mapped in the
/// right place and does not jump there visibly.</item>
/// <item>No larger than the screen it comes back on; the position only where
/// a screen is today - not on a monitor unplugged since. Under Wayland a
/// client cannot place itself: the position is ignored, size and maximised
/// state still come back.</item>
/// <item>Maximised last, and the geometry tracked while the window is normal:
/// maximised, a window reports the screen, and a window closed maximised
/// must un-maximise to where it stood before.</item>
/// </list>
/// All windows share windows.json beside settings.json, by key; written
/// beside and then moved, so a crash never leaves half a file.
/// </remarks>
public static class WindowPlacement
{
    private sealed record Placement(int X, int Y, double Width, double Height, bool Maximized);

    private static string FilePath => Path.Combine(AppSettings.Directory, "windows.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Restores the window's placement now - call it before the window is shown - and remembers it on closing.</summary>
    public static void Keep(Window window, string key)
    {
        Restore(window, key);
        Placement? normal = null;
        void Track()
        {
            if (window.WindowState == WindowState.Normal)
            {
                normal = new Placement(window.Position.X, window.Position.Y, window.Width, window.Height, false);
            }
        }
        window.PositionChanged += (_, _) => Track();
        window.SizeChanged += (_, _) => Track();
        window.Closing += (_, _) =>
        {
            var maximized = window.WindowState == WindowState.Maximized;
            var geometry = maximized
                ? normal ?? Load().GetValueOrDefault(key) ?? new Placement(window.Position.X, window.Position.Y, window.MinWidth, window.MinHeight, false)
                : new Placement(window.Position.X, window.Position.Y, window.Width, window.Height, false);
            Save(key, geometry with { Maximized = maximized });
        };
    }

    private static void Restore(Window window, string key)
    {
        if (Load().GetValueOrDefault(key) is not { } placement)
        {
            return;
        }
        var screen = window.Screens.Primary ?? window.Screens.All.FirstOrDefault();
        var maxWidth = screen is null ? double.MaxValue : screen.WorkingArea.Width / screen.Scaling;
        var maxHeight = screen is null ? double.MaxValue : screen.WorkingArea.Height / screen.Scaling;
        if (placement.Width >= window.MinWidth && placement.Height >= window.MinHeight)
        {
            window.Width = Math.Min(placement.Width, Math.Max(window.MinWidth, maxWidth));
            window.Height = Math.Min(placement.Height, Math.Max(window.MinHeight, maxHeight));
        }
        var position = new PixelPoint(placement.X, placement.Y);
        if (window.Screens.All.Any(s => s.WorkingArea.Contains(position)))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = position;
        }
        if (placement.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private static Dictionary<string, Placement> Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, Placement>>(File.ReadAllText(FilePath)) ?? []
                : [];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // A broken file starts over; a window's place is not worth an error.
            return [];
        }
    }

    private static void Save(string key, Placement placement)
    {
        try
        {
            var all = Load();
            all[key] = placement;
            Directory.CreateDirectory(AppSettings.Directory);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(all, Indented));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Same reasoning: the next start simply opens where the system puts it.
        }
    }
}
