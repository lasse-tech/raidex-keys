// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.SavedVariables;

/// <summary>What a folder the user entered as "AddOns folder" turns out to be.</summary>
/// <param name="AddOnsDir">The retail AddOns folder the input leads to, or null.</param>
/// <param name="HasAddon">Whether the Raidex Keys addon is installed there.</param>
/// <param name="SavedVariables">RaidexKeys.lua of every WoW account that has one.</param>
public sealed record AddOnsFolderCheck(string? AddOnsDir, bool HasAddon, IReadOnlyList<string> SavedVariables)
{
    public bool IsValid => AddOnsDir is not null;
}

/// <summary>The AddOns folder of a retail WoW installation, and what hangs off it.</summary>
/// <remarks>
/// The user is asked for the AddOns folder because that is the folder addon
/// players know. The addon's data lies two levels up and across:
/// _retail_/Interface/AddOns  ->  _retail_/WTF/Account/*/SavedVariables.
/// Whatever the user picks - the AddOns folder, Interface, _retail_ or the
/// WoW folder itself - leads to the same AddOns folder.
/// </remarks>
public static class AddOnsFolder
{
    public const string AddonFolderName = "RaidexKeys";

    public static AddOnsFolderCheck Inspect(string? input)
    {
        var retail = RetailDir(input);
        if (retail is null)
        {
            return new AddOnsFolderCheck(null, false, []);
        }
        var addOns = Path.Combine(retail, "Interface", "AddOns");
        var hasAddon = File.Exists(Path.Combine(addOns, AddonFolderName, AddonFolderName + ".toc"));
        return new AddOnsFolderCheck(addOns, hasAddon, SavedVariables(retail));
    }

    /// <summary>The _retail_ folder input points into, or null when it points nowhere retail.</summary>
    private static string? RetailDir(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }
        var path = input.Trim();
        if (path.StartsWith('~'))
        {
            path = AppFolders.Home + path[1..];
        }
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(path))
        {
            return null;
        }

        string? retail = Path.GetFileName(path) switch
        {
            var n when n.Equals("AddOns", StringComparison.OrdinalIgnoreCase) => Path.GetDirectoryName(Path.GetDirectoryName(path)),
            var n when n.Equals("Interface", StringComparison.OrdinalIgnoreCase) => Path.GetDirectoryName(path),
            var n when n.Equals("_retail_", StringComparison.OrdinalIgnoreCase) => path,
            _ => Path.Combine(path, "_retail_"),
        };
        return retail is not null
               && Path.GetFileName(retail).Equals("_retail_", StringComparison.OrdinalIgnoreCase)
               && Directory.Exists(Path.Combine(retail, "Interface", "AddOns"))
            ? retail
            : null;
    }

    private static List<string> SavedVariables(string retail)
    {
        var accounts = Path.Combine(retail, "WTF", "Account");
        try
        {
            return Directory.Exists(accounts)
                ? Directory.EnumerateDirectories(accounts)
                    .Select(a => Path.Combine(a, "SavedVariables", AddonDataReader.FileName))
                    .Where(File.Exists)
                    .Order(StringComparer.Ordinal)
                    .ToList()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>"MYACCOUNT" of …/WTF/Account/MYACCOUNT/SavedVariables/RaidexKeys.lua.</summary>
    public static string AccountOf(string savedVariablesFile) =>
        Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(savedVariablesFile))) ?? savedVariablesFile;
}
