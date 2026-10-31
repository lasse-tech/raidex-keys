// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using RaidexKeys.Core;

namespace RaidexKeys.App;

internal sealed class Program
{
    /// <summary>
    /// With <c>--preview [section]</c> the window shows the sample data of the
    /// brand guide instead of the (still empty) real state. Meant for checking
    /// the layout by eye; the tab bar says so with a "Beispieldaten" pill.
    /// </summary>
    public static string? PreviewSection { get; private set; }

    /// <summary>
    /// With <c>--preview</c>, <c>--screenshot file.png</c> renders the window
    /// into that file and quits: the AppStream screenshots.
    /// </summary>
    public static string? ScreenshotFile { get; private set; }

    /// <summary>
    /// With <c>--badge</c>, a screenshot keeps the "Beispieldaten" pill the
    /// software centres' shots leave out - for the landing page's link
    /// preview, which stands beside a link in a chat and has to say for
    /// itself that the week in it is made up.
    /// </summary>
    public static bool ScreenshotBadge { get; private set; }

    /// <summary>
    /// <c>--scale factor</c>: a screenshot's pixels per layout pixel - the same
    /// 1280 x 720 layout, 1.5 gives the Store's 1920 x 1080.
    /// </summary>
    public static double ScreenshotScale { get; private set; } = 1;

    /// <summary><c>--theme key</c>: this run in that colour theme, the setting untouched.</summary>
    public static string? Theme { get; private set; }

    /// <summary>
    /// With <c>--expanded</c>, the first row of the page starts slid out - the
    /// landing page's shot of a route on its map.
    /// </summary>
    public static bool Expanded { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // --diagnose: what the app sees, as text; no window. Written to a file
        // too, because a Windows GUI app has no console to print to.
        if (Array.IndexOf(args, "--diagnose") >= 0)
        {
            var report = Diagnosis.Report();
            var file = Path.Combine(AppSettings.Directory, "diagnose.txt");
            Directory.CreateDirectory(AppSettings.Directory);
            File.WriteAllText(file, report);
            Console.Write(report);
            Console.WriteLine($"(also written to {file})");
            return;
        }

        Theme = Value(args, "--theme");

        // --lang de|en: this run in that language, the setting untouched.
        Language.Apply(Value(args, "--lang") switch
        {
            "de" => UiLanguage.German,
            "en" => UiLanguage.English,
            _ => AppSettings.Load().Language,
        });

        // --preview-tiles: which floor a shot would draw the sample route on,
        // so a screenshot run can say it before taking twelve of them.
        if (Array.IndexOf(args, "--preview-tiles") >= 0)
        {
            Console.WriteLine(PreviewData.TilesFolder() is { } floor
                ? $"map tiles: {floor}"
                : "map tiles: none - the routes show without a map");
            return;
        }

        var index = Array.IndexOf(args, "--preview");
        if (index >= 0)
        {
            PreviewSection = Value(args, "--preview") ?? ViewModels.ShellViewModel.OverviewKey;
            ScreenshotFile = Value(args, "--screenshot");
            ScreenshotBadge = Array.IndexOf(args, "--badge") >= 0;
            if (double.TryParse(Value(args, "--scale"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale > 0)
            {
                ScreenshotScale = scale;
            }
            Expanded = Array.IndexOf(args, "--expanded") >= 0;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>The argument after <paramref name="option"/>, unless that is the next option.</summary>
    private static string? Value(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith('-') ? args[index + 1] : null;
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // The window class the Linux starter names in StartupWMClass, so
            // the dock groups the window under the starter's icon.
            .With(new X11PlatformOptions { WmClass = "raidex-keys" })
            .LogToTrace();
}
