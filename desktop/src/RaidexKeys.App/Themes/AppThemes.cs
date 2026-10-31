// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Media;

namespace RaidexKeys.App.Themes;

/// <summary>A colour theme: its key in the settings, its name and every colour role.</summary>
public sealed record AppTheme(string Key, string Name, IReadOnlyDictionary<string, Color> Colors)
{
    /// <summary>Swatches for the settings: background, text, accent, gold.</summary>
    public IReadOnlyList<IBrush> Swatches { get; } =
        [.. new[] { "Window", "Text", "Glow", "Timer" }.Select(role => new SolidColorBrush(Colors[role]))];
}

/// <summary>
/// The addon's colour themes (addon/RaidexKeys/Core/Themes.lua). Every role
/// becomes a resource "&lt;Role&gt;Color" and "&lt;Role&gt;Brush", taken as
/// DynamicResource, so a choice shows at once.
/// </summary>
public static class AppThemes
{
    public const string Default = "slate";

    public static readonly IReadOnlyList<AppTheme> All =
    [
        new("raidex", "Raidex", Raidex()),
        new("polar-night", "Nord — Polar Night", Derive("2E3440", "3B4252", "434C5E", "4C566A",
            "ECEFF4", "E5E9F0", "D8DEE9", "88C0D0", "EBCB8B")),
        new("ember", "Ember", Derive("211715", "30201C", "422C25", "705046",
            "FFF2E8", "E6CBBF", "C09F92", "FFAB70", "F2CC8F")),
        new("verdant", "Verdant", Derive("101F1C", "192D27", "244036", "416454",
            "ECF5ED", "C7DFCE", "96B6A2", "7ED9A6", "E5C77F")),
        new("slate", "Slate", Derive("181C23", "232A34", "303A47", "4B5B70",
            "F0F3F8", "CBD5E1", "A0AEC0", "93B9F5", "E8C77D")),
    ];

    /// <summary>The theme in use.</summary>
    public static AppTheme Current { get; private set; } = Find(Default);

    /// <summary>After a change of theme, for what draws itself (the route map).</summary>
    public static event Action? Changed;

    public static AppTheme Find(string? key) =>
        All.FirstOrDefault(t => t.Key == key) ?? All.First(t => t.Key == Default);

    /// <summary>A role's colour in the current theme as "#RRGGBB", for bindings that take a string.</summary>
    public static string Hex(string role)
    {
        var c = Current.Colors[role];
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    public static void Apply(string? key)
    {
        Current = Find(key);
        if (Application.Current is not { } app)
        {
            return;
        }
        var r = app.Resources;
        var c = Current.Colors;
        foreach (var (role, color) in c)
        {
            r[role + "Color"] = color;
            r[role + "Brush"] = new SolidColorBrush(color);
        }
        r["ScrimBrush"] = new SolidColorBrush(Alpha(c["Scrim"], 0xB8));
        r["OverlayBrush"] = new SolidColorBrush(Alpha(c["Sidebar"], 0xD9));
        r["TimerTintBrush"] = new SolidColorBrush(Alpha(c["Timer"], 0x1A));
        r["StripBrush"] = new SolidColorBrush(Alpha(c["Timer"], 0x14));
        r["StripEdgeBrush"] = new SolidColorBrush(Alpha(c["Timer"], 0x3A));
        r["RowFade"] = Fade(c["Card"], 0xFF, 0xEE, 0x33, 0.38);
        r["CardFade"] = Fade(c["Card"], 0xF2, 0xB3, 0x40, 0.5);
        r["GroupGlow"] = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.85, 0.1, RelativeUnit.Relative),
            Center = new RelativePoint(0.85, 0.1, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.7, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.8, RelativeUnit.Relative),
            GradientStops = { new(c["GroupTop"], 0), new(c["GroupMid"], 0.45), new(c["TableHead"], 1) },
        };

        // Fluent's accent, drop-downs and text boxes.
        r["SystemAccentColor"] = c["Arkan"];
        r["SystemAccentColorDark1"] = c["AccentDark1"];
        r["SystemAccentColorDark2"] = c["AccentDark2"];
        r["SystemAccentColorDark3"] = c["AccentDark3"];
        r["SystemAccentColorLight1"] = c["ArkanHover"];
        r["SystemAccentColorLight2"] = c["Glow"];
        r["SystemAccentColorLight3"] = c["AccentLight3"];
        void Brush(string key, string role) => r[key] = new SolidColorBrush(c[role]);
        Brush("ComboBoxBackground", "Sidebar");
        Brush("ComboBoxBackgroundPointerOver", "NavSelected");
        Brush("ComboBoxBackgroundPressed", "NavSelected");
        Brush("ComboBoxBorderBrush", "Edge");
        Brush("ComboBoxBorderBrushPointerOver", "Arkan");
        Brush("ComboBoxBorderBrushPressed", "Arkan");
        Brush("ComboBoxDropDownBackground", "Card");
        Brush("ComboBoxDropDownBorderBrush", "Edge");
        Brush("ComboBoxItemBackgroundPointerOver", "NavSelected");
        Brush("ComboBoxItemBackgroundSelected", "NavSelected");
        Brush("ComboBoxItemBackgroundSelectedPointerOver", "NavHover");
        Brush("ComboBoxItemForegroundSelected", "Text");
        Brush("TextControlBackground", "Sidebar");
        Brush("TextControlBackgroundPointerOver", "Sidebar");
        Brush("TextControlBackgroundFocused", "Sidebar");
        Brush("TextControlBorderBrush", "Edge");
        Brush("TextControlBorderBrushPointerOver", "Glow");
        Brush("TextControlBorderBrushFocused", "Arkan");
        Brush("TextControlForeground", "Text");
        Brush("TextControlForegroundPointerOver", "Text");
        Brush("TextControlForegroundFocused", "Text");
        Brush("TextControlPlaceholderForeground", "TextFaint");

        Changed?.Invoke();
    }

    /// <summary>The brand guide's palette, sections 03 and 05.</summary>
    private static Dictionary<string, Color> Raidex() => Colors(new()
    {
        ["Void"] = "07080F",
        ["Surface"] = "0D1022",
        ["Window"] = "0B0D1C",
        ["Sidebar"] = "0A0C18",
        ["NavSelected"] = "151033",
        ["NavHover"] = "1E1747",
        ["Card"] = "0F1326",
        ["TableHead"] = "0C0F20",
        ["Line"] = "1B2038",
        ["RowLine"] = "171B33",
        ["Edge"] = "3A3252",
        ["Text"] = "E8EAF6",
        ["TextBody"] = "C3C8E0",
        ["TextSoft"] = "AFB6D6",
        ["TextMuted"] = "8E94B8",
        ["TextFaint"] = "6E7599",
        ["Arkan"] = "7C5CFF",
        ["ArkanHover"] = "9377FF",
        ["Glow"] = "A78BFF",
        ["AccentDark1"] = "6A4AF0",
        ["AccentDark2"] = "5A3BDD",
        ["AccentDark3"] = "4B2FC9",
        ["AccentLight3"] = "C7B6FF",
        ["OnArkan"] = "0A0C18",
        ["Timer"] = "C9A227",
        ["Scrim"] = "080A16",
        ["SeatEdge"] = "23203C",
        ["SelectedEdge"] = "3A2E6B",
        ["GroupEdge"] = "2E2656",
        ["GroupTop"] = "241A5C",
        ["GroupMid"] = "140F31",
    });

    /// <summary>
    /// A theme from the addon's nine colours; the other roles in the Raidex
    /// palette's proportions to them.
    /// </summary>
    private static Dictionary<string, Color> Derive(string bg, string header, string tab, string edge,
        string text, string body, string muted, string accent, string gold)
    {
        Color B = Parse(bg), T = Parse(tab), M = Parse(muted), A = Parse(accent), X = Parse(text);
        var black = Avalonia.Media.Colors.Black;
        var arkan = Lighten(A, 0.88, 1);
        return new()
        {
            ["Void"] = Mix(B, black, 0.5),
            ["Surface"] = Mix(B, T, 0.05),
            ["Window"] = Mix(B, black, 0.13),
            ["Sidebar"] = Mix(B, black, 0.24),
            ["NavSelected"] = Mix(T, black, 0.2),
            ["NavHover"] = T,
            ["Card"] = Mix(B, A, 0.024),
            ["TableHead"] = B,
            ["Line"] = Mix(B, M, 0.134),
            ["RowLine"] = Mix(B, A, 0.08),
            ["Edge"] = Parse(edge),
            ["Text"] = X,
            ["TextBody"] = Mix(X, Parse(body), 0.75),
            ["TextSoft"] = Mix(X, M, 0.6),
            ["TextMuted"] = M,
            ["TextFaint"] = Mix(M, T, 0.26),
            ["Arkan"] = arkan,
            ["ArkanHover"] = Lighten(A, 0.95, 1),
            ["Glow"] = A,
            ["AccentDark1"] = Lighten(A, 0.8, 0.85),
            ["AccentDark2"] = Lighten(A, 0.71, 0.7),
            ["AccentDark3"] = Lighten(A, 0.63, 0.62),
            ["AccentLight3"] = Mix(A, Avalonia.Media.Colors.White, 0.35),
            ["OnArkan"] = Mix(B, black, 0.24),
            ["Timer"] = Parse(gold),
            ["Scrim"] = Mix(B, black, 0.33),
            ["SeatEdge"] = Mix(B, A, 0.13),
            ["SelectedEdge"] = Mix(T, A, 0.22),
            ["GroupEdge"] = Mix(Parse(header), A, 0.17),
            ["GroupTop"] = Mix(T, arkan, 0.12),
            ["GroupMid"] = Mix(T, black, 0.24),
        };
    }

    private static Dictionary<string, Color> Colors(Dictionary<string, string> hex) =>
        hex.ToDictionary(p => p.Key, p => Parse(p.Value));

    private static Color Parse(string hex) => Color.Parse("#" + hex);

    /// <summary>a moved by share t towards b.</summary>
    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>The colour with its lightness and saturation scaled.</summary>
    private static Color Lighten(Color c, double lightness, double saturation)
    {
        var hsl = c.ToHsl();
        return HslColor.ToRgb(hsl.H, Math.Clamp(hsl.S * saturation, 0, 1), Math.Clamp(hsl.L * lightness, 0, 1));
    }

    private static Color Alpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static LinearGradientBrush Fade(Color c, byte start, byte middle, byte end, double at) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops = { new(Alpha(c, start), 0), new(Alpha(c, middle), at), new(Alpha(c, end), 1) },
    };
}
