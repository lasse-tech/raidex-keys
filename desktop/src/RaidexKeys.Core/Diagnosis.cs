// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using RaidexKeys.Core.Mdt;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core;

/// <summary>
/// What the app sees on this machine, as plain text: settings, the AddOns
/// folder and what it holds, what the search finds, and what every data file
/// reads as. For <c>raidex-keys --diagnose</c> - the answer to "the app shows
/// nothing", without anyone having to look at the machine.
/// </summary>
public static class Diagnosis
{
    public static string Report()
    {
        var sb = new StringBuilder();
        void Line(string text = "") => sb.AppendLine(text);

        var home = AppFolders.Home;
        Line($"Raidex Keys {BuildInfo.Version} (build {BuildInfo.BuildNumber}) - diagnosis {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        Line($"System: {RuntimeInformation.OSDescription} · user {Environment.UserName} · home {home}");
        Line();

        var settingsFile = Path.Combine(AppSettings.Directory, "settings.json");
        Line($"Settings: {settingsFile} ({(File.Exists(settingsFile) ? "present" : "missing")})");
        if (File.Exists(settingsFile))
        {
            foreach (var line in SafeReadLines(settingsFile))
            {
                Line("  " + line);
            }
        }
        var settings = AppSettings.Load();
        Line();

        Line($"AddOns folder from the settings: {settings.AddOnsFolder ?? "(not set - the app searches on its own)"}");
        if (settings.AddOnsFolder is { } chosen)
        {
            Describe(chosen, Line);
        }
        Line();

        var found = WowLocator.FindAddOnsFolders();
        Line($"Search finds {found.Count} retail AddOns folder(s):");
        foreach (var folder in found)
        {
            Describe(folder, Line);
        }
        if (!OperatingSystem.IsWindows())
        {
            Line("Lutris game configs:");
            var any = false;
            foreach (var (file, prefixes) in WowLocator.LutrisConfigs(home))
            {
                any = true;
                Line($"  {file}: prefix {(prefixes.Count == 0 ? "(none)" : string.Join(", ", prefixes))}");
            }
            if (!any)
            {
                Line($"  none in {string.Join(", ", WowLocator.LutrisConfigDirs(home))}");
            }
        }
        Line();

        var files = WowLocator.FindSavedVariables(settings.AddOnsFolder);
        var parts = new List<AddonData>();
        Line($"Data files the app reads: {files.Count}");
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            Line($"  {file}");
            Line($"    {info.Length} bytes, written {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            try
            {
                var data = AddonDataReader.Read(file);
                parts.Add(data);
                Line($"    reads fine: {data.Characters.Count} character(s), {data.Maps.Count} dungeon(s), " +
                     $"{data.Guilds.Sum(g => g.Keys.Count)} guild key(s), season {data.Season}");
                // What the group planner's item level switch hangs on: the
                // addon writes it from 0.1.10, and only at a logout or /reload
                // after the game handed the table out.
                Line(data.Rewards.Count > 0
                    ? $"    reward levels: {data.Rewards.Count} (a +10 gives item level {data.RecommendedItemLevel(10)})"
                    : "    reward levels: none - an addon before 0.1.10, or the file was written before the game answered");
                foreach (var c in data.Characters)
                {
                    Line($"    - {c.Name} ({c.Realm}): rating {c.Rating}, key {(c.KeyMapId is { } m ? $"{m} +{c.KeyLevel}" : "none")}, " +
                         $"{c.Best.Count} best run(s), saved {c.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
                    if (c.IsIncomplete)
                    {
                        Line("      INCOMPLETE: a rating above 0 needs runs, none stored - the game had not handed them to the addon");
                    }
                }
            }
            catch (Exception e)
            {
                Line($"    DOES NOT READ: {e.GetType().Name}: {e.Message}");
            }
        }
        Line();

        Line("Mythic Dungeon Tools (routes):");
        foreach (var file in files)
        {
            var (addonDir, savedVariables) = MdtReader.Locate(file);
            if (MdtReader.Stamp(addonDir) is null)
            {
                Line($"  {addonDir}: not installed");
                continue;
            }
            var errors = new List<string>();
            var dungeons = MdtReader.ReadDungeons(addonDir, errors);
            Line($"  {addonDir}: {dungeons.Count} dungeon(s) read" + (errors.Count > 0 ? $", {errors.Count} file(s) not" : ""));
            errors.ForEach(e => Line("    DOES NOT READ: " + e));
            if (!File.Exists(savedVariables))
            {
                Line($"  {savedVariables}: missing - no route saved on this account yet");
                continue;
            }
            try
            {
                var routes = MdtReader.ReadRoutes(savedVariables);
                Line($"  {savedVariables}: {routes.Count} route(s)");
                foreach (var route in routes)
                {
                    var forces = dungeons.TryGetValue(route.DungeonIndex, out var d) ? RouteForces.Of(route, d) : null;
                    Line($"    - dungeon {route.DungeonIndex}{(d is null ? " (unknown)" : $" = {d.MapId} {d.EnglishName}")}: " +
                         $"\"{route.Name ?? "(Default)"}\"{(route.Selected ? " [selected]" : "")}, {route.Pulls.Count} pull(s)" +
                         (forces is null ? "" : $", {forces.Count}/{forces.DungeonTotal} = {forces.Percent:0.00} %"));
                }
            }
            catch (Exception e)
            {
                Line($"  {savedVariables}: DOES NOT READ: {e.GetType().Name}: {e.Message}");
            }
        }
        Line();

        Raidex(Line, AddonData.Merge(parts));
        Line($"Watching files: {WatcherCheck()}");
        return sb.ToString();
    }

    /// <summary>
    /// What Raidex answers about the own characters - the figures the group
    /// planner weighs a key by, beside the addon's own. A gap between the two
    /// columns is the thing to look at when a gain looks wrong.
    /// </summary>
    private static void Raidex(Action<string> line, AddonData data)
    {
        var settings = AppSettings.Load();
        var server = settings.RaidexServerUri;
        var session = SessionStore.Load(server);
        var source = AppSettings.ServerFromEnvironment() is not null ? $", from {AppSettings.ServerVariable}"
            : Environment.GetEnvironmentVariable(AppSettings.ServerVariable) is { Length: > 0 } unusable
                ? $", {AppSettings.ServerVariable}=\"{unusable}\" names no server and is ignored"
                : settings.RaidexServer is not null ? ", from settings.json" : "";
        line($"Raidex: {server} ({(session is null ? "no session - not connected" : "session kept")}{source})");
        if (session is null || data.Characters.Count == 0)
        {
            line("");
            return;
        }
        var client = new RaidexClient(server, session);
        foreach (var c in data.Characters)
        {
            var slug = Web.WebLinks.RealmSlug(c.Realm);
            MplusCharacter season;
            try
            {
                // Blizzard's slug, not the realm_key that only matches spellings.
                season = client.MplusCharacterAsync(slug, c.Name).GetAwaiter().GetResult();
            }
            catch (RaidexException e)
            {
                line($"  {c.Name}-{c.Realm} (asked for {slug}/{c.Name}): {e.Code}{(e.Status is { } s ? $" ({(int)s})" : "")}");
                continue;
            }
            line($"  {c.Name}-{c.Realm}: season {season.Season?.ToString(CultureInfo.InvariantCulture) ?? "?"}, rating {season.Rating}, " +
                 $"role {season.Role ?? "unknown"}, spec {season.Spec ?? "unknown"}, " +
                 $"item level {season.ItemLevel?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, {season.Best.Count} best run(s)");
            var scores = season.Scores;
            foreach (var (mapId, map) in data.Maps.OrderBy(m => m.Value.Name, StringComparer.CurrentCulture))
            {
                var addon = c.Best.GetValueOrDefault(mapId);
                var theirs = scores.TryGetValue(mapId, out var score) ? score : (double?)null;
                line($"    {mapId} {map.Name}: addon " +
                     (addon is null ? "no run" : $"+{addon.Level} = {addon.Score:0.0}") +
                     " · Raidex " + (theirs is null ? "no run" : $"{theirs:0.0}"));
            }
        }
        line("");
    }

    private static void Describe(string folder, Action<string> line)
    {
        var check = AddOnsFolder.Inspect(folder);
        line($"  {folder}");
        line(check.IsValid
            ? $"    retail AddOns folder: yes · addon installed: {(check.HasAddon ? "yes" : "no")} · data files: {check.SavedVariables.Count}"
            : "    NOT a retail AddOns folder (…/_retail_/Interface/AddOns)");
    }

    /// <summary>Whether a file watcher can be set up at all (inotify limits on Linux).</summary>
    private static string WatcherCheck()
    {
        try
        {
            using var watcher = new FileSystemWatcher(Path.GetTempPath()) { EnableRaisingEvents = true };
            return "works";
        }
        catch (Exception e)
        {
            return $"FAILS ({e.GetType().Name}: {e.Message}) - the app checks the files every 5 seconds instead";
        }
    }

    private static IEnumerable<string> SafeReadLines(string file)
    {
        try
        {
            return File.ReadAllLines(file);
        }
        catch (Exception e)
        {
            return [$"(cannot read: {e.Message})"];
        }
    }
}
