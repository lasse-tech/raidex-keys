// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Net.Http.Json;
using System.Text.Json;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core.Updates;

/// <summary>The versions the landing page offers (its latest.json).</summary>
public sealed record Latest(string? App, string? Addon, string? Page);

/// <summary>
/// What is newer than what is here: the app against the version the landing
/// page offers, the installed addon against the one that belongs to it.
/// </summary>
/// <param name="App">The newer app version, or null when this one is current.</param>
/// <param name="Addon">The newer addon version, or null.</param>
/// <param name="AddonHere">What is installed in WoW; null when no addon was found.</param>
/// <param name="Page">Where to get it.</param>
public sealed record Updates(string? App, string? Addon, string? AddonHere, string? Page)
{
    public bool Any => App is not null || Addon is not null;
}

/// <summary>
/// Whether this app and the addon beside it are current (plan 6.10).
/// </summary>
/// <remarks>
/// The versions come from the landing page's own <c>latest.json</c>, not from
/// GitHub: the page is what offers the files, it is a host the app already
/// talks to, and it needs no account. Nothing is downloaded and nothing is
/// installed - a newer version is a line on the About page and a strip over
/// the window, and the app carries on either way. Only a data file the app
/// cannot read at all stops it, and that is the reader's business, not this.
/// </remarks>
public static class UpdateCheck
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "RaidexKeys (lars@lasse-tech.de)" } },
    };

    /// <summary>Where the app's version is published, beside the landing page.</summary>
    public static Uri LatestUrl(Uri server) => new(server, "keys/latest.json");

    /// <summary>The public repository's releases; the addon's are tagged addon-v&lt;version&gt;.</summary>
    public static readonly Uri AddonReleases = new("https://api.github.com/repos/lasse-tech/raidex-keys/releases?per_page=30");

    /// <summary>The newest addon version among GitHub's releases; drafts do not count.</summary>
    public static string? NewestAddon(JsonElement releases)
    {
        if (releases.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        string? newest = null;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
            {
                continue;
            }
            if (release.TryGetProperty("tag_name", out var tag) && tag.GetString() is { } name
                && name.StartsWith("addon-v", StringComparison.Ordinal)
                && (newest is null || Newer(name["addon-v".Length..], newest)))
            {
                newest = name["addon-v".Length..];
            }
        }
        return newest;
    }

    /// <summary>
    /// Asks once and compares. Answers "nothing newer" for every failure -
    /// being offline is not something to report as an update.
    /// </summary>
    /// <param name="appVersion">This app's version; null checks the addon only.</param>
    /// <param name="addonVersion">The installed addon's version (<see cref="AddonToc.Version"/>); null checks the app only.</param>
    /// <param name="addonReleases">Where the addon's releases are listed; <see cref="AddonReleases"/> by default.</param>
    public static async Task<Updates> RunAsync(Uri server, string? appVersion, string? addonVersion,
        Uri? addonReleases = null, CancellationToken ct = default)
    {
        Latest? latest = null;
        if (appVersion is not null)
        {
            try
            {
                latest = await Http.GetFromJsonAsync<Latest>(LatestUrl(server), ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or NotSupportedException
                or JsonException)
            {
            }
        }
        string? addon = null;
        if (addonVersion is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, addonReleases ?? AddonReleases);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                    cancellationToken: ct).ConfigureAwait(false);
                addon = NewestAddon(json.RootElement);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
            }
        }
        return new Updates(
            Newer(latest?.App, appVersion) ? latest!.App : null,
            Newer(addon, addonVersion) ? addon : null,
            addonVersion,
            latest?.Page);
    }

    /// <summary>
    /// Whether <paramref name="there"/> is a later version than
    /// <paramref name="here"/>, by their numbers - "0.1.10" is after "0.1.9",
    /// which a string comparison would get backwards. Anything unparsable
    /// counts as not newer: a hint nobody can act on is noise.
    /// </summary>
    public static bool Newer(string? there, string? here)
    {
        if (Parts(there) is not { } theirs || Parts(here) is not { } ours)
        {
            return false;
        }
        for (var i = 0; i < Math.Max(theirs.Length, ours.Length); i++)
        {
            var a = i < theirs.Length ? theirs[i] : 0;
            var b = i < ours.Length ? ours[i] : 0;
            if (a != b)
            {
                return a > b;
            }
        }
        return false;
    }

    private static int[]? Parts(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }
        var parts = new List<int>();
        foreach (var piece in version.Trim().TrimStart('v', 'V').Split('.'))
        {
            if (!int.TryParse(new string(piece.TakeWhile(char.IsAsciiDigit).ToArray()), out var number))
            {
                return null;
            }
            parts.Add(number);
        }
        return parts.Count > 0 ? [.. parts] : null;
    }
}
