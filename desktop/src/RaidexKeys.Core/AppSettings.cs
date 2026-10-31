// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidexKeys.Core;

/// <summary>The interface language the user picked.</summary>
public enum UiLanguage
{
    System,
    German,
    English,
}

/// <summary>A table's sort as the user left it: the column's name and its direction.</summary>
public sealed class TableSortChoice
{
    public string Column { get; set; } = "";
    public bool Descending { get; set; }
}

/// <summary>Settings kept between runs, as one small JSON file.</summary>
public sealed class AppSettings
{
    public UiLanguage Language { get; set; } = UiLanguage.System;

    /// <summary>
    /// The retail AddOns folder, confirmed at first start or in the settings;
    /// null until the user did, and then the app asks.
    /// </summary>
    public string? AddOnsFolder { get; set; }

    /// <summary>
    /// How many levels above the highest one timed the rating goal may plan
    /// (RatingPlanner): 0 to 3.
    /// </summary>
    public int PlanLevelsAbove { get; set; } = 1;

    /// <summary>
    /// The group planner (plan 7.2b) builds groups of exactly one tank, one
    /// healer and three damage dealers.
    /// </summary>
    public bool GroupBySpec { get; set; } = true;

    /// <summary>The group planner leaves a member out of a key their gear is too light for.</summary>
    public bool GroupByItemLevel { get; set; }

    /// <summary>
    /// How far below a key's recommended item level still counts as geared
    /// enough for it: 0 to 10.
    /// </summary>
    /// <remarks>
    /// The recommendation the game hands out is the Great Vault's level for
    /// that key, a few points above what the run itself drops - a +10 gives
    /// 311 and puts 318 in the vault. Ten is the default so the bar lands at
    /// or below the key's own drop: being geared by the key you are running
    /// is what a key is for.
    /// </remarks>
    public int GroupItemLevelAllowance { get; set; } = 10;

    /// <summary>Raidex, where the app logs in and stores routes.</summary>
    public const string DefaultRaidexServer = "https://raidex.app/";

    /// <summary>
    /// Another Raidex server, set by hand in settings.json; null means
    /// <see cref="DefaultRaidexServer"/>. Not written while null, so a changed
    /// default reaches everyone who never set one.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RaidexServer { get; set; }

    /// <summary>
    /// The environment variable that points one run at another Raidex server:
    /// <c>RAIDEXKEYS_SERVER=https://raidex.example/ raidex-keys</c>. It wins over
    /// settings.json and writes nothing there.
    /// </summary>
    public const string ServerVariable = "RAIDEXKEYS_SERVER";

    /// <summary>The server the app talks to: the environment variable, then settings.json, then Raidex itself.</summary>
    [JsonIgnore]
    public Uri RaidexServerUri => ServerFromEnvironment() ?? ParseServer(RaidexServer) ?? new Uri(DefaultRaidexServer);

    /// <summary>The server <see cref="ServerVariable"/> names; null when it is unset or names none.</summary>
    public static Uri? ServerFromEnvironment() => ParseServer(Environment.GetEnvironmentVariable(ServerVariable));

    /// <summary>
    /// "prod" for raidex.app, or a server's address - a
    /// bare host name means HTTPS; null for nothing usable. The address always
    /// ends in a slash, so the API's paths go below it rather than replace its
    /// last segment.
    /// </summary>
    public static Uri? ParseServer(string? value)
    {
        var text = value?.Trim();
        switch (text?.ToLowerInvariant())
        {
            case null or "":
                return null;
            case "prod" or "live":
                return new Uri(DefaultRaidexServer);
        }
        if (!text!.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }
        return Uri.TryCreate(text.EndsWith('/') ? text : text + "/", UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http"
            ? uri
            : null;
    }

    /// <summary>The monospace font for figures and small headings (App's MonoFonts); the brand's by default.</summary>
    public string MonoFont { get; set; } = "IBM Plex Mono";

    /// <summary>The raid calendar shown: "raidex" or "wow"; null follows the Raidex connection.</summary>
    public string? CalendarSource { get; set; }

    /// <summary>The colour theme (App's AppThemes), as in the addon.</summary>
    public string Theme { get; set; } = "slate";

    /// <summary>The map window shows the route as text beside the map.</summary>
    public bool MapWindowShowsRoute { get; set; }

    /// <summary>A route's map draws the way round: 1 → 2 → 3 as curved arrows between the pulls.</summary>
    public bool RouteArrows { get; set; }

    /// <summary>
    /// The column each table is sorted by, by the table's name ("keystones",
    /// "guild", ...); a table missing here keeps its own order.
    /// </summary>
    public Dictionary<string, TableSortChoice> TableSorts { get; set; } = new(StringComparer.Ordinal);

    // Paths stay as they are - "Spiele/Wörld", not "Spiele/W\u00F6rld" - so the
    // build tooling can read the AddOns folder with sed.
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// %APPDATA%\RaidexKeys on Windows, $XDG_CONFIG_HOME/raidex-keys (usually
    /// ~/.config/raidex-keys) on Linux.
    /// </summary>
    public static string Directory => AppFolders.Config;

    private static string FilePath => Path.Combine(Directory, "settings.json");

    /// <summary>Reads the stored settings; a missing or broken file gives the defaults.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings();
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A settings file is a convenience; the app starts with the defaults.
        }
        return new AppSettings();
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}
