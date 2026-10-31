// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Mdt;

namespace RaidexKeys.Core.SavedVariables;

/// <summary>
/// The addon's data, kept current: reads every RaidexKeys.lua found and
/// reads again when WoW rewrites one (on logout and /reload). Beside each, the
/// routes of Mythic Dungeon Tools, if installed, the same way (<see cref="Mdt"/>).
/// </summary>
/// <remarks>
/// <para>
/// Reading comes first and never depends on watching: a watcher that cannot
/// be set up (the user's inotify limit is spent, a file system without
/// change events) must not leave the app empty. Besides the watchers, a timer
/// looks at the files' write times every few seconds - the safety net for
/// every case the watchers miss.
/// </para>
/// <para>
/// WoW writes the file and its .bak in quick succession, so changes are
/// gathered for a second before reading. A file that does not read - caught
/// mid-write, or broken - keeps its previous data, and the reason is kept for
/// the settings page and the empty pages.
/// </para>
/// </remarks>
public sealed class AddonDataSource : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(5);

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Dictionary<string, AddonData> _byFile = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _writeTimes = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private Timer? _settle;
    private Timer? _poll;

    /// <summary>Raised on a thread-pool thread after the data changed.</summary>
    public event EventHandler? Changed;

    public AddonData Data { get; private set; } = AddonData.Empty;

    /// <summary>Mythic Dungeon Tools: its dungeons and the player's routes, or <see cref="MdtData.None"/>.</summary>
    public MdtData Mdt { get; private set; } = MdtData.None;

    // MDT's dungeon data per MDT folder, read again only when MDT was updated.
    private readonly Dictionary<string, (DateTime Stamp, Dictionary<int, MdtDungeon> Dungeons, List<string> Errors)> _mdtDungeons =
        new(StringComparer.Ordinal);

    /// <summary>The AddOns folder in use; null while the app searches on its own.</summary>
    public string? Folder { get; private set; }

    /// <summary>The files found at the last search.</summary>
    public IReadOnlyList<string> Files { get; private set; } = [];

    /// <summary>Why files could not be read or watched; empty when all is well.</summary>
    public IReadOnlyList<string> Errors { get; private set; } = [];

    public string? LastError => Errors.Count == 0 ? null : string.Join("\n", Errors);

    /// <summary>Searches (again), reads everything found and watches it.</summary>
    /// <param name="addOnsFolder">The AddOns folder from the settings, or null to search.</param>
    public void Start(string? addOnsFolder)
    {
        lock (_lock)
        {
            Folder = addOnsFolder;
            StopWatching();
            Files = WowLocator.FindSavedVariables(addOnsFolder);
            _byFile.Clear();
            _writeTimes.Clear();
        }
        Reload();

        var watchErrors = new List<string>();
        lock (_lock)
        {
            foreach (var file in Files)
            {
                try
                {
                    Watch(file);
                }
                catch (Exception e)
                {
                    // The timer below still notices every save.
                    watchErrors.Add($"{file}: {e.Message}");
                }
            }
            _poll = new Timer(_ => Poll(), null, PollEvery, PollEvery);
        }
        if (watchErrors.Count > 0)
        {
            Trace($"watching failed, polling instead: {string.Join("; ", watchErrors)}");
        }
    }

    private void Watch(string file)
    {
        var watcher = new FileSystemWatcher(Path.GetDirectoryName(file)!)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        watcher.Filters.Add(AddonDataReader.FileName);
        watcher.Filters.Add(MdtReader.FileName);
        watcher.Changed += (_, _) => Poke();
        watcher.Created += (_, _) => Poke();
        watcher.Renamed += (_, _) => Poke();
        _watchers.Add(watcher);
        watcher.EnableRaisingEvents = true;
    }

    private void Poll()
    {
        bool changed;
        lock (_lock)
        {
            changed = Files.Concat(Files.Select(f => MdtReader.Locate(f).SavedVariables))
                .Any(f => WriteTime(f) != _writeTimes.GetValueOrDefault(f));
        }
        if (changed)
        {
            Poke();
        }
    }

    private static DateTime WriteTime(string file)
    {
        try
        {
            return File.GetLastWriteTimeUtc(file);
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }

    private void Poke()
    {
        lock (_lock)
        {
            _settle?.Dispose();
            _settle = new Timer(_ => Reload(), null, Settle, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Reads every known file; keeps the old data of a file that fails.</summary>
    public void Reload()
    {
        lock (_lock)
        {
            var errors = new List<string>();
            foreach (var file in Files)
            {
                _writeTimes[file] = WriteTime(file);
                try
                {
                    _byFile[file] = AddonDataReader.Read(file);
                }
                catch (Exception e)
                {
                    // Whatever the file does, the app keeps running and says why.
                    errors.Add($"{file}: {e.GetType().Name}: {e.Message}");
                }
            }
            Data = AddonData.Merge(_byFile.Values.ToList());
            Mdt = ReadMdt(errors);
            Errors = errors;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// MDT beside every data file: the dungeons of its installation and the
    /// routes of that account; a route saved on two accounts counts once.
    /// </summary>
    private MdtData ReadMdt(List<string> errors)
    {
        var installed = false;
        int? version = null;
        var dungeons = new Dictionary<int, MdtDungeon>();
        var routes = new List<MdtRoute>();
        foreach (var file in Files)
        {
            var (addonDir, savedVariables) = MdtReader.Locate(file);
            // Taken whether MDT is installed or not: Poll compares this file's
            // time on every round, and a time never taken reads as a change -
            // a missing file's is 1601, not DateTime.MinValue. Without MDT the
            // app read everything anew every five seconds.
            _writeTimes[savedVariables] = WriteTime(savedVariables);
            if (MdtReader.Stamp(addonDir) is not { } stamp)
            {
                continue;
            }
            installed = true;
            version ??= MdtReader.Version(addonDir);
            if (!_mdtDungeons.TryGetValue(addonDir, out var cached) || cached.Stamp != stamp)
            {
                var readErrors = new List<string>();
                cached = (stamp, MdtReader.ReadDungeons(addonDir, readErrors), readErrors);
                _mdtDungeons[addonDir] = cached;
            }
            errors.AddRange(cached.Errors);
            foreach (var (index, dungeon) in cached.Dungeons)
            {
                dungeons.TryAdd(index, dungeon);
            }

            if (!File.Exists(savedVariables))
            {
                continue;
            }
            try
            {
                foreach (var route in MdtReader.ReadRoutes(savedVariables))
                {
                    if (route.Uid is null || routes.TrueForAll(known => known.Uid != route.Uid))
                    {
                        routes.Add(route);
                    }
                }
            }
            catch (Exception e)
            {
                errors.Add($"{savedVariables}: {e.GetType().Name}: {e.Message}");
            }
        }
        return installed
            ? new MdtData(true, dungeons, routes) { Version = version }
            : MdtData.None;
    }

    private static void Trace(string message) => System.Diagnostics.Trace.WriteLine("RaidexKeys: " + message);

    private void StopWatching()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
        _poll?.Dispose();
        _poll = null;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopWatching();
            _settle?.Dispose();
        }
    }
}
