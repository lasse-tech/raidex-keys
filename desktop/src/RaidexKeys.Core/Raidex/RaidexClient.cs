// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidexKeys.Core.Raidex;

/// <summary>Who is logged in: the active character and guild as Raidex knows them.</summary>
public sealed record RaidexMe(string? Character, string? Realm, string? Guild);

/// <summary>A route stored in Raidex (its plan, 7.2a): the MDT string and what the app read from it.</summary>
public sealed record StoredRoute(
    long Id, int MapId, string Name, string? Uid, int Pulls, double Forces, string Mdt,
    string Visibility, string? GuildName, bool Mine, string Owner, long UpdatedAt)
{
    public bool SharedWithGuild => Visibility == "guild";
}

/// <summary>A key on the guild board: Raidex's roster (class, spec, item level) with the newest report.</summary>
/// <param name="Source">"self" - reported by its owner; "guild" - seen by someone else in game.</param>
/// <param name="ObservedAt">When the addon saw it, in seconds.</param>
/// <param name="Role">"tank", "healer" or "damage" from the roster's spec id; null until it was scanned again.</param>
/// <param name="SpecIcon">Blizzard's own icon for that specialisation, on their CDN; null with no spec id.</param>
public sealed record BoardKey(
    string Name, string RealmSlug, int? ClassId, string? Spec, int? ItemLevel,
    int MapId, int Level, int? Rating, string Source, long ObservedAt, string ReportedBy,
    string? Role = null, string? SpecIcon = null);

/// <summary>One of a character's best runs this season, as Blizzard has it.</summary>
public sealed record MplusRun(int MapId, int Level, int DurationSec, bool Timed, double Score);

/// <summary>
/// What Raidex knows of a character's Mythic+ season: the best run per dungeon
/// with the rating it earned, and the role and item level the group planner
/// weighs their keystone by (plan 7.2b).
/// </summary>
/// <param name="Role">"tank", "healer" or "damage", from the spec id - no language involved; null when unknown.</param>
public sealed record MplusCharacter(
    string Name, string RealmSlug, int? Season, int Rating,
    int? SpecId, string? Spec, string? Role, int? ItemLevel, IReadOnlyList<MplusRun> Best,
    string? SpecIcon = null)
{
    /// <summary>The best score per dungeon, as the rating calculator takes it.</summary>
    public IReadOnlyDictionary<int, double> Scores =>
        Best.GroupBy(r => r.MapId).ToDictionary(g => g.Key, g => g.Max(r => r.Score));
}

/// <summary>The active guild's keys of this week.</summary>
public sealed record GuildBoard(string Guild, IReadOnlyList<BoardKey> Keys);

/// <summary>What Raidex made of a report: how many it stored, whom it refused and why.</summary>
public sealed record ReportResult(int Stored, IReadOnlyList<string> NotOnAccount, IReadOnlyList<string> NotInGuild);

/// <summary>A connection being made: the app polls with the device code, the user confirms the user code at the address.</summary>
public sealed record ConnectCode(string DeviceCode, string UserCode, Uri VerifyUrl, int Interval, int ExpiresIn);

/// <summary>Why Raidex did not answer as hoped - said in the app, not thrown at the user.</summary>
public sealed class RaidexException(string code, HttpStatusCode? status = null) : Exception(code)
{
    public string Code { get; } = code;
    public HttpStatusCode? Status { get; } = status;

    /// <summary>The session is gone (expired, logged out elsewhere): log in again.</summary>
    public bool LoggedOut => Status == HttpStatusCode.Unauthorized;
}

/// <summary>
/// Raidex's API for the app: who is logged in, and the stored routes. The
/// session travels as a Bearer token, as Raidex's mobile app sends it.
/// </summary>
public sealed class RaidexClient(Uri server, string? session)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Uri Server { get; } = server;

    /// <summary>
    /// A new connection code (plan 6.4): the device code is the app's secret,
    /// the user code what the Raidex page shows, the address where to confirm it.
    /// </summary>
    public async Task<ConnectCode> ConnectStartAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Post, "keys/connect", null, ct);
        var r = doc.RootElement;
        return new ConnectCode(r.GetProperty("deviceCode").GetString()!, r.GetProperty("userCode").GetString()!,
            new Uri(r.GetProperty("verifyUrl").GetString()!), r.GetProperty("interval").GetInt32(), r.GetProperty("expiresIn").GetInt32());
    }

    /// <summary>Whether the code was confirmed: "pending", "approved" with the session, "denied" or "expired".</summary>
    public async Task<(string Status, string? Session)> ConnectPollAsync(string deviceCode, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Post, "keys/connect/poll", new { deviceCode }, ct);
        var r = doc.RootElement;
        return (r.GetProperty("status").GetString() ?? "expired", r.TryGetProperty("session", out var s) ? s.GetString() : null);
    }

    public async Task<RaidexMe?> MeAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "me", null, ct);
        var root = doc.RootElement;
        if (!root.TryGetProperty("user", out var user) || user.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        var character = root.TryGetProperty("activeCharacter", out var c) && c.ValueKind == JsonValueKind.Object ? c : (JsonElement?)null;
        var guild = root.TryGetProperty("activeGuild", out var g) && g.ValueKind == JsonValueKind.Object ? g : (JsonElement?)null;
        return new RaidexMe(
            character is { } ch && ch.TryGetProperty("name", out var n) ? n.GetString() : null,
            character is { } ch2 && ch2.TryGetProperty("realm", out var r) ? r.GetString() : null,
            guild is { } gu && gu.TryGetProperty("displayName", out var gn) ? gn.GetString() : null);
    }

    public async Task<IReadOnlyList<StoredRoute>> RoutesAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "keys/routes", null, ct);
        return doc.RootElement.GetProperty("routes").Deserialize<List<StoredRoute>>(Json) ?? [];
    }

    /// <param name="shareWithGuild">For the guild active in Raidex, or for the user alone.</param>
    public async Task<StoredRoute> SaveRouteAsync(string mdt, string name, string? uid, int mapId, int pulls, double forces,
        bool shareWithGuild, CancellationToken ct = default)
    {
        var body = new { mdt, name, uid, mapId, pulls, forces = Math.Round(forces, 2), visibility = shareWithGuild ? "guild" : "me" };
        using var doc = await SendAsync(HttpMethod.Post, "keys/routes", body, ct);
        return doc.RootElement.GetProperty("route").Deserialize<StoredRoute>(Json)!;
    }

    public async Task<StoredRoute> ShareRouteAsync(long id, bool shareWithGuild, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Patch, $"keys/routes/{id}", new { visibility = shareWithGuild ? "guild" : "me" }, ct);
        return doc.RootElement.GetProperty("route").Deserialize<StoredRoute>(Json)!;
    }

    public async Task DeleteRouteAsync(long id, CancellationToken ct = default) =>
        (await SendAsync(HttpMethod.Delete, $"keys/routes/{id}", null, ct)).Dispose();

    /// <summary>Reports the keys of one data state (<see cref="KeysReport"/>).</summary>
    public async Task<ReportResult> ReportAsync(object report, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Post, "keys/report", report, ct);
        var root = doc.RootElement;
        var refused = root.GetProperty("refused");
        return new ReportResult(root.GetProperty("stored").GetInt32(),
            refused.GetProperty("notOnAccount").Deserialize<List<string>>(Json) ?? [],
            refused.GetProperty("notInGuild").Deserialize<List<string>>(Json) ?? []);
    }

    /// <summary>
    /// A guild mate's Mythic+ season - their best run per dungeon, role and
    /// item level. Raidex serves it for the caller's own characters and their
    /// guild mates only, and caches it ten minutes.
    /// </summary>
    public async Task<MplusCharacter> MplusCharacterAsync(string realmSlug, string name, CancellationToken ct = default)
    {
        var path = $"mplus/character/{Uri.EscapeDataString(realmSlug)}/{Uri.EscapeDataString(name)}";
        using var doc = await SendAsync(HttpMethod.Get, path, null, ct);
        return doc.RootElement.Deserialize<MplusCharacter>(Json)!;
    }

    public async Task<GuildBoard> GuildBoardAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "keys/guild", null, ct);
        var root = doc.RootElement;
        return new GuildBoard(root.GetProperty("guild").GetString() ?? "",
            root.GetProperty("keys").Deserialize<List<BoardKey>>(Json) ?? []);
    }

    // ===== Raid calendar =====

    /// <summary>The active guild's raids: upcoming ones - running ones included - soonest first, or past ones latest first.</summary>
    public async Task<IReadOnlyList<RaidEvent>> RaidsAsync(bool past, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, past ? "raids?scope=past" : "raids?scope=upcoming", null, ct);
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().Select(RaidexCalendar.Raid).ToList()
            : [];
    }

    public async Task<RaidEvent> CreateRaidAsync(RaidDraft draft, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Post, "raids", RaidexCalendar.Body(draft), ct);
        return RaidexCalendar.Raid(doc.RootElement);
    }

    public async Task<RaidEvent> UpdateRaidAsync(long id, RaidDraft draft, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Patch, $"raids/{id}", RaidexCalendar.Body(draft), ct);
        return RaidexCalendar.Raid(doc.RootElement);
    }

    public async Task DeleteRaidAsync(long id, CancellationToken ct = default) =>
        (await SendAsync(HttpMethod.Delete, $"raids/{id}", null, ct)).Dispose();

    /// <summary>Signs a character up - a second main replaces the first, alts go to the bench - and hands the raid back.</summary>
    public async Task<RaidEvent> SignupAsync(long raidId, SignupDraft s, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["role"] = s.Role,
            ["status"] = s.Status,
            ["charId"] = s.Character.Id,
            ["charName"] = s.Character.Name,
            ["charClass"] = s.Character.ClassName,
            ["note"] = string.IsNullOrWhiteSpace(s.Note) ? null : s.Note.Trim(),
            ["isAlt"] = s.IsAlt,
        };
        using var doc = await SendAsync(HttpMethod.Put, $"raids/{raidId}/signup", body, ct);
        return RaidexCalendar.Raid(doc.RootElement);
    }

    /// <summary>Takes the caller's main off the raid; their alts stay.</summary>
    public async Task<RaidEvent> WithdrawAsync(long raidId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Delete, $"raids/{raidId}/signup", null, ct);
        return RaidexCalendar.Raid(doc.RootElement);
    }

    /// <summary>Removes one sign-up - an alt of one's own, or anybody's for who leads the raids.</summary>
    public async Task<RaidEvent> RemoveSignupAsync(long raidId, long signupId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Delete, $"raids/{raidId}/signup/{signupId}", null, ct);
        return RaidexCalendar.Raid(doc.RootElement);
    }

    /// <summary>The raid's public link, whose preview Discord shows.</summary>
    public async Task<Uri> ShareRaidAsync(long raidId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Post, $"raids/{raidId}/share", null, ct);
        return new Uri(Server, "r/" + doc.RootElement.GetProperty("token").GetString());
    }

    /// <summary>The guides of an instance, looked up by its German name.</summary>
    public async Task<GuideList> GuidesAsync(string instance, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "guides/" + Uri.EscapeDataString(instance), null, ct);
        return RaidexCalendar.Guides(doc.RootElement);
    }

    public async Task AddGuideAsync(string instance, string title, string url, CancellationToken ct = default) =>
        (await SendAsync(HttpMethod.Post, "guild/guides", new { instance, title, url, site = "custom" }, ct)).Dispose();

    public async Task DeleteGuideAsync(long id, CancellationToken ct = default) =>
        (await SendAsync(HttpMethod.Delete, $"guild/guides/{id}", null, ct)).Dispose();

    /// <summary>Blizzard's journal: every raid, or every dungeon, newest expansion first.</summary>
    public async Task<IReadOnlyList<JournalInstance>> JournalAsync(bool dungeons, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, dungeons ? "wow/dungeons" : "wow/raids", null, ct);
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().Select(RaidexCalendar.Instance).ToList()
            : [];
    }

    /// <summary>The caller's characters on Battle.net, highest level first.</summary>
    public async Task<IReadOnlyList<AccountCharacter>> CharactersAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "characters", null, ct);
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().Select(RaidexCalendar.Character).ToList()
            : [];
    }

    public async Task LogoutAsync(CancellationToken ct = default) =>
        (await SendAsync(HttpMethod.Post, "auth/logout", null, ct)).Dispose();

    /// <summary>The interface language as Raidex knows it: "de" or "en".</summary>
    private static string ApiLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? "de" : "en";

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(Server, "api/" + path));
        if (session is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session);
        }
        // Raidex answers game data (spec, race and instance names) in this language.
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(ApiLanguage()));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }
        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new RaidexException("unreachable");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new RaidexException("timeout");
        }
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            }
            catch (JsonException)
            {
                throw new RaidexException("not_json", response.StatusCode);
            }
            if (!response.IsSuccessStatusCode)
            {
                var code = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "error" : "error";
                doc.Dispose();
                throw new RaidexException(code, response.StatusCode);
            }
            return doc;
        }
    }
}
