// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RaidexKeys.App.Themes;
using RaidexKeys.App.ViewModels;
using RaidexKeys.App.Views;
using RaidexKeys.Core;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.App;

public partial class App : Application
{
    /// <summary>The addon's data; one for the app's lifetime, outliving a relaunched window.</summary>
    private static AddonDataSource? _source;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        MonoFonts.Apply(AppSettings.Load().MonoFont);
        // Screenshots show the brand's own theme unless asked otherwise.
        AppThemes.Apply(Program.Theme ?? (Program.ScreenshotFile is null ? AppSettings.Load().Theme : "raidex"));
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = (MainWindow?)null;
            if (Program.PreviewSection is null)
            {
                var settings = AppSettings.Load();
                _source = new AddonDataSource();
                // Until the user confirmed a folder, show what the search finds.
                _source.Start(settings.AddOnsFolder);
                desktop.Exit += (_, _) => _source.Dispose();
                window = CreateMainWindow(ShellViewModel.OverviewKey);
                if (settings.AddOnsFolder is null)
                {
                    window.Opened += async (_, _) => await AskForAddOnsFolder(window);
                }
                // Who is logged in to Raidex and what is stored there - once the window stands.
                Raidex.RaidexAccount.Instance.Data = _source;
                window.Opened += async (_, _) => await Raidex.RaidexAccount.Instance.StartAsync();
                // Whether a newer app or addon is out - said, never enforced,
                // and the window is up and usable before it is even asked.
                if (window.DataContext is ShellViewModel shell)
                {
                    window.Opened += async (_, _) => await shell.Updates.RunAsync();
                }
            }
            desktop.MainWindow = window ?? CreateMainWindow(Program.PreviewSection ?? ShellViewModel.OverviewKey);
            if (Program.ScreenshotFile is { } file)
            {
                var shot = desktop.MainWindow;
                shot.Width = Screenshot.Width;
                shot.Height = Screenshot.Height;
                shot.Opened += async (_, _) =>
                {
                    await Screenshot.TakeAsync(shot, file, Program.ScreenshotScale);
                    desktop.Shutdown();
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// First start: asks where the AddOns are, prefilled with the search's
    /// first find. "Later" leaves the setting empty, so the next start asks again.
    /// </summary>
    private static async Task AskForAddOnsFolder(MainWindow owner)
    {
        var folder = await new SetupWindow(new AddOnsFolderViewModel(null)).ShowDialog<string?>(owner);
        if (folder is null || _source is null)
        {
            return;
        }
        var settings = AppSettings.Load();
        settings.AddOnsFolder = folder;
        settings.Save();
        // Rereads the data; the window, settings page included, follows (ShellViewModel).
        _source.Start(folder);
    }

    private static MainWindow CreateMainWindow(string section)
    {
        var shell = _source is null
            ? PreviewData.CreateShell(section)
            : new ShellViewModel(section, _source);
        var window = new MainWindow { DataContext = shell };
        window.Closed += (_, _) => shell.Dispose();
        return window;
    }

    private static MainWindow? _sample;

    /// <summary>The sample data in a window of its own, beside the real one.</summary>
    public static void OpenSample()
    {
        if (_sample is { } open)
        {
            open.Activate();
            return;
        }
        var shell = PreviewData.CreateShell(ShellViewModel.OverviewKey);
        var window = new MainWindow { DataContext = shell, Title = Localization.Strings.SampleWindowTitle };
        window.Closed += (_, _) =>
        {
            shell.Dispose();
            _sample = null;
        };
        _sample = window;
        window.Show();
    }

    /// <summary>
    /// Switches the interface language by building the window anew.
    /// </summary>
    /// <remarks>
    /// The strings are read when a view is built ({x:Static}), so a running
    /// window keeps the language it was built in. A fresh window over the old
    /// one is the whole switch; the user stays in the settings, where the
    /// choice was made.
    /// </remarks>
    public static void Relaunch(string section)
    {
        if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }
        var old = desktop.MainWindow;
        var window = CreateMainWindow(section);
        if (old is not null)
        {
            window.Position = old.Position;
            window.Width = old.Width;
            window.Height = old.Height;
            window.WindowState = old.WindowState;
        }
        desktop.MainWindow = window;
        window.Show();
        old?.Close();
    }
}
