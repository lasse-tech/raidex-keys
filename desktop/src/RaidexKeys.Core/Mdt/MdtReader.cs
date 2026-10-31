// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text.RegularExpressions;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core.Mdt;

/// <summary>
/// Reads Mythic Dungeon Tools from the player's installation: its dungeon data
/// files and its SavedVariables with the routes. Both are parsed as data -
/// never run - and nothing is kept beyond what the pages show.
/// </summary>
public static partial class MdtReader
{
    public const string AddonFolderName = "MythicDungeonTools";
    public const string FileName = "MythicDungeonTools.lua";

    /// <summary>
    /// MDT's folder and its SavedVariables for the account one RaidexKeys.lua
    /// belongs to: …/_retail_/WTF/Account/X/SavedVariables/RaidexKeys.lua
    /// leads to …/_retail_/Interface/AddOns/MythicDungeonTools and the
    /// MythicDungeonTools.lua beside it.
    /// </summary>
    public static (string AddonDir, string SavedVariables) Locate(string raidexKeysFile)
    {
        var savedVariables = Path.GetDirectoryName(raidexKeysFile)!;
        var retail = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(savedVariables))))!;
        return (Path.Combine(retail, "Interface", "AddOns", AddonFolderName), Path.Combine(savedVariables, FileName));
    }

    /// <summary>The installed MDT's version stamp: its .toc's write time, or null when it is not installed.</summary>
    public static DateTime? Stamp(string addonDir)
    {
        var toc = Path.Combine(addonDir, AddonFolderName + ".toc");
        return File.Exists(toc) ? File.GetLastWriteTimeUtc(toc) : null;
    }

    /// <summary>
    /// The installed MDT's version as its own export strings carry it: the
    /// .toc's "## Version: 6.2.16" as 6216, which is what MDT writes into
    /// <c>addonVersion</c>. Null when it cannot be read.
    /// </summary>
    public static int? Version(string addonDir)
    {
        var toc = Path.Combine(addonDir, AddonFolderName + ".toc");
        try
        {
            foreach (var line in File.ReadLines(toc))
            {
                if (!line.StartsWith("## Version:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var parts = line[(line.IndexOf(':') + 1)..].Trim().Split('.');
                var digits = parts.Select(part => int.TryParse(part, out var n) ? n : -1).ToList();
                return digits.Count == 3 && digits.All(n => n >= 0)
                    ? digits[0] * 1000 + digits[1] * 100 + digits[2]
                    : null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // No MDT, no version - the export goes without one.
        }
        return null;
    }

    /// <summary>
    /// Every dungeon of the installed MDT, by MDT's dungeon number, with the
    /// folders of its map tiles; files that do not read are skipped.
    /// </summary>
    public static Dictionary<int, MdtDungeon> ReadDungeons(string addonDir, ICollection<string>? errors = null)
    {
        var dungeons = new Dictionary<int, MdtDungeon>();
        if (!Directory.Exists(addonDir))
        {
            return dungeons;
        }
        foreach (var file in Directory.EnumerateFiles(addonDir, "*.lua", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("MDT.dungeonEnemies[dungeonIndex]", StringComparison.Ordinal))
            {
                continue;
            }
            try
            {
                if (ReadDungeon(text, addonDir) is { } dungeon)
                {
                    dungeons[dungeon.Index] = dungeon;
                }
            }
            catch (FormatException e)
            {
                errors?.Add($"{file}: {e.Message}");
            }
        }
        return dungeons;
    }

    /// <summary>
    /// One dungeon file: the number from "local dungeonIndex = …", the
    /// challenge-mode id and name from its map info, the total forces, the
    /// enemy table and - given MDT's folder - where its map tiles lie. Null
    /// when a part is missing.
    /// </summary>
    public static MdtDungeon? ReadDungeon(string text, string? addonDir = null)
    {
        var index = DungeonIndex().Match(text);
        var info = MapInfo().Match(text);
        var total = TotalCount().Match(text);
        var enemiesAt = text.IndexOf("MDT.dungeonEnemies[dungeonIndex]", StringComparison.Ordinal);
        if (!index.Success || !info.Success || !total.Success || enemiesAt < 0)
        {
            return null;
        }
        var mapId = MapIdField().Match(info.Value);
        if (!mapId.Success)
        {
            return null;
        }
        var englishName = EnglishNameField().Match(info.Value);
        var equals = text.IndexOf('=', enemiesAt + "MDT.dungeonEnemies[dungeonIndex]".Length);
        var table = LuaParser.ParseTableAt(text, equals + 1);

        var enemies = new Dictionary<int, MdtEnemy>();
        foreach (var (key, value) in table.Entries)
        {
            if (key is not long enemyIndex || value is not LuaTable enemy)
            {
                continue;
            }
            var count = (int)(enemy.Integer("count") ?? 0);
            var clones = new Dictionary<int, MdtClone>();
            foreach (var (cloneKey, cloneValue) in enemy.Table("clones")?.Entries ?? new Dictionary<object, object?>())
            {
                if (cloneKey is long cloneIndex && cloneValue is LuaTable clone)
                {
                    clones[(int)cloneIndex] = new MdtClone((int)(clone.Integer("count") ?? count),
                        clone.Number("x") ?? 0, clone.Number("y") ?? 0, (int)(clone.Integer("sublevel") ?? 1), clone.Number("scale") ?? 1);
                }
            }
            enemies[(int)enemyIndex] = new MdtEnemy(enemy.String("name") ?? $"#{enemy.Integer("id")}", (int)(enemy.Integer("id") ?? 0),
                clones, enemy.Number("scale") ?? 1, enemy.Boolean("isBoss") ?? false);
        }
        var multiplier = ScaleMultiplier().Match(text);
        return new MdtDungeon(int.Parse(index.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(mapId.Groups[1].Value, CultureInfo.InvariantCulture),
            englishName.Success ? englishName.Groups[1].Value : "",
            int.Parse(total.Groups[1].Value, CultureInfo.InvariantCulture), enemies)
        {
            TileDirs = addonDir is null ? new Dictionary<int, string>() : TileDirsOf(text, addonDir),
            ScaleMultiplier = multiplier.Success
                ? double.Parse(multiplier.Groups[1].Value, CultureInfo.InvariantCulture)
                : 1,
        };
    }

    /// <summary>
    /// "[1] = { customTextures = 'Interface\\AddOns\\'..addonName..'\\Midnight\\Textures\\X' }"
    /// per floor, as a folder below MDT's own - where it really is on disk.
    /// </summary>
    private static Dictionary<int, string> TileDirsOf(string text, string addonDir)
    {
        var dirs = new Dictionary<int, string>();
        foreach (Match m in CustomTextures().Matches(text))
        {
            var relative = m.Groups[2].Value.Replace(@"\\", "/", StringComparison.Ordinal).Trim('/');
            var dir = Path.Combine([addonDir, .. relative.Split('/')]);
            if (Directory.Exists(dir))
            {
                dirs[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] = dir;
            }
        }
        return dirs;
    }

    /// <summary>
    /// The routes in one MythicDungeonTools.lua. AceDB leaves out what equals
    /// its defaults: an unnamed first route, the first route as the selected one.
    /// </summary>
    public static List<MdtRoute> ReadRoutes(string path)
    {
        var globals = LuaParser.ParseGlobals(File.ReadAllText(path));
        var global = (globals.GetValueOrDefault("MythicDungeonToolsDB") as LuaTable)?.Table("global");
        var routes = new List<MdtRoute>();
        if (global?.Table("presets") is not { } presets)
        {
            return routes;
        }
        var current = global.Table("currentPreset");
        foreach (var (dungeonKey, dungeonValue) in presets.Entries)
        {
            if (dungeonKey is not long dungeonIndex || dungeonValue is not LuaTable list)
            {
                continue;
            }
            var selected = current?.Integer(dungeonIndex) ?? 1;
            foreach (var (presetKey, presetValue) in list.Entries)
            {
                if (presetKey is long presetIndex && presetValue is LuaTable preset
                    && ReadPreset(preset, (int)dungeonIndex, presetIndex == selected) is { } route)
                {
                    routes.Add(route);
                }
            }
        }
        return routes;
    }

    /// <summary>
    /// One preset - as stored in the SavedVariables or carried by an export
    /// string - as a route; null for MDT's "&lt;New Preset&gt;" entry (value = 0)
    /// and for a route without a single enemy.
    /// </summary>
    public static MdtRoute? ReadPreset(LuaTable preset, int dungeonIndex, bool selected)
    {
        if (preset.Table("value") is not { } value)
        {
            return null;
        }
        var pulls = (value.Table("pulls")?.List() ?? []).OfType<LuaTable>().Select(ReadPull).ToList();
        return pulls.All(p => p.Clones.Count == 0)
            ? null
            : new MdtRoute(dungeonIndex, preset.String("text"), preset.String("uid"), selected, pulls)
            { Preset = preset };
    }

    private static MdtPull ReadPull(LuaTable pull) => new(pull.Entries
        .Where(e => e.Key is long && e.Value is LuaTable)
        .ToDictionary(e => (int)(long)e.Key, e => (IReadOnlyList<int>)((LuaTable)e.Value!).List().OfType<long>().Select(c => (int)c).ToList()));

    [GeneratedRegex(@"local\s+dungeonIndex\s*=\s*(\d+)")]
    private static partial Regex DungeonIndex();

    [GeneratedRegex(@"MDT\.mapInfo\[dungeonIndex\]\s*=\s*\{.*?\}", RegexOptions.Singleline)]
    private static partial Regex MapInfo();

    [GeneratedRegex(@"\bmapID\s*=\s*(\d+)")]
    private static partial Regex MapIdField();

    [GeneratedRegex(@"\benglishName\s*=\s*""([^""]*)""")]
    private static partial Regex EnglishNameField();

    [GeneratedRegex(@"MDT\.dungeonTotalCount\[dungeonIndex\]\s*=\s*\{[^}]*?\bnormal\s*=\s*(\d+)")]
    private static partial Regex TotalCount();

    [GeneratedRegex(@"MDT\.scaleMultiplier\[dungeonIndex\]\s*=\s*([\d.]+)")]
    private static partial Regex ScaleMultiplier();

    [GeneratedRegex(@"\[(\d+)\]\s*=\s*\{\s*customTextures\s*=\s*'Interface\\\\AddOns\\\\'\s*\.\.\s*addonName\s*\.\.\s*'([^']*)'")]
    private static partial Regex CustomTextures();
}
