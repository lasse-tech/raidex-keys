// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.SavedVariables;

/// <summary>
/// The installed addon's own .toc: what version of it sits in WoW, so the app
/// can say when it is older than the one that belongs to this app.
/// </summary>
public static class AddonToc
{
    /// <summary>"## Version:" of the installed addon; null when none is there.</summary>
    public static string? Version(string? addOnsFolder) => Field(addOnsFolder, "Version");

    /// <summary>"## X-Build:", the commit count `make install-addon` stamped in; null when none.</summary>
    public static string? Build(string? addOnsFolder) => Field(addOnsFolder, "X-Build");

    private static string? Field(string? addOnsFolder, string name)
    {
        if (string.IsNullOrWhiteSpace(addOnsFolder))
        {
            return null;
        }
        var toc = Path.Combine(addOnsFolder, AddOnsFolder.AddonFolderName,
            AddOnsFolder.AddonFolderName + ".toc");
        try
        {
            foreach (var line in File.ReadLines(toc))
            {
                if (line.StartsWith($"## {name}:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line[(line.IndexOf(':') + 1)..].Trim();
                    return value.Length > 0 ? value : null;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // No addon, no answer - the pages say what that means.
        }
        return null;
    }
}
