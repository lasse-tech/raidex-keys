// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core;

/// <summary>The app's folders, always absolute.</summary>
public static class AppFolders
{
    private static readonly Lazy<string> ConfigRoot = new(() =>
        Usable(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify)));

    private static readonly Lazy<string> CacheRoot = new(() => Usable(OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify)
        : Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { } xdg && Path.IsPathFullyQualified(xdg)
            ? xdg
            : Path.Combine(Home, ".cache")));

    private static string Name => OperatingSystem.IsWindows() ? "RaidexKeys" : "raidex-keys";

    /// <summary>The user's home; a private temp folder without one.</summary>
    public static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify) is { } home
        && Path.IsPathFullyQualified(home)
            ? home
            : PrivateTemp();

    /// <summary>%APPDATA%\RaidexKeys, or $XDG_CONFIG_HOME/raidex-keys (usually ~/.config/raidex-keys).</summary>
    public static string Config => Path.Combine(ConfigRoot.Value, Name);

    /// <summary>%LOCALAPPDATA%\RaidexKeys, or $XDG_CACHE_HOME/raidex-keys (usually ~/.cache/raidex-keys).</summary>
    public static string Cache => Path.Combine(CacheRoot.Value, Name);

    /// <summary>The path if it is absolute and can be created, else a private temp folder.</summary>
    private static string Usable(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            try
            {
                Directory.CreateDirectory(path);
                return path;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return PrivateTemp();
    }

    /// <summary>raidex-keys-&lt;user&gt; in the temp folder, readable by this user only.</summary>
    private static string PrivateTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"raidex-keys-{Environment.UserName}");
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(path, Private);
            // Throws when someone else owns it.
            File.SetUnixFileMode(path, Private);
        }
        return path;
    }
}
