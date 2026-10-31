// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Microsoft.Win32;

namespace RaidexKeys.Core.SavedVariables;

/// <summary>Suggests the retail AddOns folders on this machine.</summary>
/// <remarks>
/// Only a suggestion: the user confirms the folder at first start and can
/// change it in the settings (<see cref="AddOnsFolder"/> checks any input).
/// Windows: the install path Blizzard registers, then the usual folders.
/// Linux: WoW runs in a Wine prefix - Lutris (whose game configs name the
/// prefix), plain Wine, Steam/Proton, Bottles, and prefixes kept outside the
/// home directory: one or two levels below / and on
/// mounted drives.
/// </remarks>
public static class WowLocator
{
    private static readonly string[] ProgramFolders = ["Program Files (x86)", "Program Files"];

    /// <summary>Top-level folders that never hold a Wine prefix.</summary>
    private static readonly HashSet<string> SystemFolders = new(StringComparer.Ordinal)
    {
        "bin", "boot", "cdrom", "dev", "etc", "home", "lib", "lib32", "lib64", "libx32", "lost+found",
        "proc", "root", "run", "sbin", "snap", "srv", "sys", "tmp", "usr", "var",
    };

    /// <summary>Every retail AddOns folder found, most likely first.</summary>
    public static IReadOnlyList<string> FindAddOnsFolders()
    {
        var candidates = OperatingSystem.IsWindows() ? WindowsCandidates() : LinuxCandidates();
        return candidates
            .Select(wow => Path.Combine(wow, "_retail_", "Interface", "AddOns"))
            .Where(Directory.Exists)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The addon's data files: below the chosen folder, or below every folder found.</summary>
    public static IReadOnlyList<string> FindSavedVariables(string? addOnsFolder)
    {
        var folders = string.IsNullOrWhiteSpace(addOnsFolder) ? FindAddOnsFolders() : [addOnsFolder];
        return folders
            .SelectMany(f => AddOnsFolder.Inspect(f).SavedVariables)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> WindowsCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            // Blizzard's key points at the _retail_ folder or at the WoW folder.
            var registered = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft",
                "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(registered))
            {
                var trimmed = registered.TrimEnd('\\', '/');
                yield return Path.GetFileName(trimmed).Equals("_retail_", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(trimmed)!
                    : trimmed;
            }
        }
        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles") })
        {
            if (!string.IsNullOrEmpty(root))
            {
                yield return Path.Combine(root, "World of Warcraft");
            }
        }
        foreach (var drive in new[] { @"C:\", @"D:\", @"E:\" })
        {
            yield return Path.Combine(drive, "World of Warcraft");
            yield return Path.Combine(drive, "Games", "World of Warcraft");
        }
    }

    private static IEnumerable<string> LinuxCandidates()
    {
        var home = AppFolders.Home;
        var user = Environment.UserName;
        var prefixes = new List<string>();
        prefixes.AddRange(LutrisPrefixes(home));
        prefixes.AddRange(Children(Path.Combine(home, "Games")));
        prefixes.Add(Path.Combine(home, ".wine"));
        prefixes.AddRange(Children(Path.Combine(home, ".local/share/Steam/steamapps/compatdata")).Select(p => Path.Combine(p, "pfx")));
        prefixes.AddRange(Children(Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata")).Select(p => Path.Combine(p, "pfx")));
        prefixes.AddRange(Children(Path.Combine(home, ".local/share/bottles/bottles")));
        prefixes.AddRange(Children(Path.Combine(home, ".var/app/com.usebottles.bottles/data/bottles/bottles")));

        // Prefixes outside the home directory, one or two levels below /.
        foreach (var top in Children("/").Where(d => !SystemFolders.Contains(Path.GetFileName(d))))
        {
            prefixes.Add(top);
            prefixes.AddRange(Children(top));
        }
        foreach (var mounts in new[] { "/mnt", $"/media/{user}", $"/run/media/{user}" })
        {
            foreach (var drive in Children(mounts))
            {
                prefixes.Add(drive);
                prefixes.AddRange(Children(drive));
            }
        }

        foreach (var prefix in prefixes)
        {
            foreach (var programs in ProgramFolders)
            {
                yield return Path.Combine(prefix, "drive_c", programs, "World of Warcraft");
                // A Windows drive mounted as is.
                yield return Path.Combine(prefix, programs, "World of Warcraft");
            }
            yield return Path.Combine(prefix, "World of Warcraft");
        }
    }

    /// <summary>The folders Lutris keeps its game configs in.</summary>
    public static IReadOnlyList<string> LutrisConfigDirs(string home) =>
    [
        Path.Combine(home, ".config/lutris/games"),
        Path.Combine(home, ".local/share/lutris/games"),
        Path.Combine(home, ".var/app/net.lutris.Lutris/config/lutris/games"),
        Path.Combine(home, ".var/app/net.lutris.Lutris/data/lutris/games"),
    ];

    /// <summary>Every Lutris game config and the "prefix:" lines in it.</summary>
    public static IEnumerable<(string File, IReadOnlyList<string> Prefixes)> LutrisConfigs(string home)
    {
        foreach (var dir in LutrisConfigDirs(home).Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.yml"))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                var prefixes = lines
                    .Select(l => l.Trim())
                    .Where(l => l.StartsWith("prefix:", StringComparison.Ordinal))
                    .Select(l => l["prefix:".Length..].Trim().Trim('"', '\''))
                    .ToList();
                yield return (file, prefixes);
            }
        }
    }

    private static IEnumerable<string> LutrisPrefixes(string home) =>
        LutrisConfigs(home).SelectMany(c => c.Prefixes);

    private static IEnumerable<string> Children(string dir)
    {
        try
        {
            return Directory.Exists(dir) ? Directory.EnumerateDirectories(dir).ToList() : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
