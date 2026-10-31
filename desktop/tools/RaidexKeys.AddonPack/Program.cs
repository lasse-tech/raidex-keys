// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.AddonPack;

// raidex-keys-addonpack <addon folder> <target folder> [build number]
// Copies the addon, every .lua, .xml and .toc without comments; replaces the target.
if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("usage: RaidexKeys.AddonPack <addon folder> <target folder> [build number]");
    return 2;
}
var source = Path.GetFullPath(args[0]);
var target = Path.GetFullPath(args[1]);
var build = args.Length == 3 ? args[2] : "dev";
if (!File.Exists(Path.Combine(source, "RaidexKeys.toc")))
{
    Console.Error.WriteLine($"not the addon folder (no RaidexKeys.toc): {source}");
    return 1;
}

// A .lua or .xml with nothing left but blank lines: WoW reports it as a file
// it cannot open, so the pack stops before such an addon reaches the game.
var empty = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
    .Where(file => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".lua" => AddonSource.IsBlank(AddonSource.StripLua(File.ReadAllText(file))),
        ".xml" => AddonSource.IsBlank(AddonSource.StripXml(File.ReadAllText(file))),
        _ => false,
    })
    .Select(file => Path.GetRelativePath(source, file))
    .Order(StringComparer.Ordinal)
    .ToList();
if (empty.Count > 0)
{
    Console.Error.WriteLine($"nothing but comments, WoW would not open it: {string.Join(", ", empty)}");
    return 1;
}

if (Directory.Exists(target))
{
    Directory.Delete(target, recursive: true);
}
foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
{
    var to = Path.Combine(target, Path.GetRelativePath(source, file));
    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
    switch (Path.GetExtension(file).ToLowerInvariant())
    {
        case ".lua":
            File.WriteAllText(to, AddonSource.StripLua(File.ReadAllText(file)));
            break;
        case ".xml":
            File.WriteAllText(to, AddonSource.StripXml(File.ReadAllText(file)));
            break;
        case ".toc":
            File.WriteAllText(to, AddonSource.StripToc(File.ReadAllText(file), build));
            break;
        default:
            File.Copy(file, to);
            break;
    }
}
Console.WriteLine($"addon packed: {target} (build {build}, comments stripped)");
return 0;
