// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Media;

namespace RaidexKeys.App.Themes;

/// <summary>
/// The monospace font for figures, times, levels and the small headings -
/// the brand's IBM Plex Mono or one of four easier to read, all under the SIL
/// Open Font License and shipped in Assets/Fonts with their licence texts.
/// </summary>
/// <remarks>
/// Every style takes it as DynamicResource MonoFont, so a choice in the
/// settings shows at once, without reopening the window.
/// </remarks>
public static class MonoFonts
{
    public const string Default = "IBM Plex Mono";

    public static readonly IReadOnlyList<string> All = [Default, "JetBrains Mono", "Source Code Pro", "Fira Mono", "Cascadia Mono"];

    public static FontFamily Family(string name) =>
        new($"avares://RaidexKeys/Assets/Fonts#{(All.Contains(name) ? name : Default)}");

    /// <summary>The font in use now, for what draws text itself (the route map).</summary>
    public static FontFamily Current =>
        Application.Current?.Resources.TryGetResource("MonoFont", null, out var value) == true && value is FontFamily family
            ? family
            : Family(Default);

    public static void Apply(string name)
    {
        if (Application.Current is { } app)
        {
            app.Resources["MonoFont"] = Family(name);
        }
    }
}
