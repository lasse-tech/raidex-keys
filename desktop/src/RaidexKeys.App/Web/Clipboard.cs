// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace RaidexKeys.App.Web;

/// <summary>The system clipboard: MDT strings in with one click, and back out.</summary>
internal static class Clipboard
{
    private static Avalonia.Input.Platform.IClipboard? Board =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
            is { } window ? TopLevel.GetTopLevel(window)?.Clipboard : null;

    /// <summary>The clipboard's text, or null when it holds none.</summary>
    public static async Task<string?> TextAsync() =>
        Board is { } board ? await board.TryGetTextAsync() : null;

    /// <summary>Puts text on the clipboard; false when there is none to put it on.</summary>
    public static async Task<bool> SetTextAsync(string? text)
    {
        if (Board is not { } board || string.IsNullOrEmpty(text))
        {
            return false;
        }
        await board.SetTextAsync(text);
        return true;
    }
}
