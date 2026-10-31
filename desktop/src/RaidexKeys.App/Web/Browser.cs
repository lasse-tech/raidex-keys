// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace RaidexKeys.App.Web;

/// <summary>Opens a link in the user's browser.</summary>
internal static class Browser
{
    /// <summary>Opens <paramref name="url"/>; false when no way to a browser worked.</summary>
    public static async Task<bool> OpenAsync(string? url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }
        // On Linux Avalonia's launcher hands xdg-open "a\&b" - escaped for a
        // shell it does not use - and the browser gets a broken address. A URL
        // with a query (the Raidex login) needs it as it is.
        if (OperatingSystem.IsLinux() && Start("xdg-open", uri.AbsoluteUri))
        {
            return true;
        }
        // On Windows Avalonia's launcher logs a failure only to its own logger
        // and reports nothing. So the shell's handler for the address first,
        // then url.dll's, which opens a link even when the browser's
        // association is broken - and a false for the caller to say so.
        if (OperatingSystem.IsWindows())
        {
            return Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })
                || Start("rundll32.exe", "url.dll,FileProtocolHandler", uri.AbsoluteUri);
        }
        var window = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        return window is not null && TopLevel.GetTopLevel(window) is { } top && await top.Launcher.LaunchUriAsync(uri);
    }

    private static bool Start(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        return Start(start);
    }

    private static bool Start(ProcessStartInfo start)
    {
        try
        {
            using var process = Process.Start(start);
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Not there, or no handler for it: the next way, if there is one.
            return false;
        }
    }
}
