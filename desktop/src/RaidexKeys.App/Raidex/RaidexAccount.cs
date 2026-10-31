// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Net;
using RaidexKeys.App.Localization;
using RaidexKeys.App.Web;
using RaidexKeys.Core;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.App.Raidex;

/// <summary>
/// The connection to a Raidex account and what hangs on it, one for the app's lifetime: the
/// session (kept in the system's secret store), who is logged in, the routes
/// stored there. The pages listen to <see cref="Changed"/>; every call runs on
/// the UI thread, and what fails ends up in <see cref="Problem"/>.
/// </summary>
public sealed class RaidexAccount
{
    public static RaidexAccount Instance { get; } = new();

    private string? _session;
    private CancellationTokenSource? _login;

    private RaidexAccount()
    {
        Server = AppSettings.Load().RaidexServerUri;
    }

    public event EventHandler? Changed;

    public Uri Server { get; }

    /// <summary>The active character and guild in Raidex; null while logged out.</summary>
    public RaidexMe? Me { get; private set; }

    public bool IsLoggingIn => _login is not null;

    public IReadOnlyList<StoredRoute> Routes { get; private set; } = [];

    /// <summary>The addon's data, to report from; set at start.</summary>
    public AddonDataSource? Data { get; set; }

    /// <summary>The active guild's keys this week, as Raidex has them; null while logged out or guildless.</summary>
    public GuildBoard? Board { get; private set; }

    /// <summary>What Raidex made of the last report, and when it went.</summary>
    public ReportResult? LastReport { get; private set; }

    public DateTimeOffset? LastReportAt { get; private set; }

    private CancellationTokenSource? _reportSoon;

    /// <summary>What went wrong last, said for the user; null when all is well.</summary>
    public string? Problem { get; private set; }

    private RaidexClient Client => new(Server, _session);

    /// <summary>
    /// At start: the session kept in the secret store, then who is logged in
    /// and the stored routes - when there is one.
    /// </summary>
    /// <remarks>
    /// Read here, off the UI thread, and not when the account is first
    /// touched: the window touches it while it is being built, and secret-tool
    /// waits on a secret service that may never answer - D-Bus gives up on
    /// starting one after 25 s, and the window stood blank that long (#29).
    /// The preview never calls this, so it never asks the secret store at all.
    /// </remarks>
    public async Task StartAsync()
    {
        var kept = await Task.Run(() => SessionStore.Load(Server));
        // A login that finished in the meantime wins over what was kept.
        if (_session is null && kept is not null)
        {
            _session = kept;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        if (_session is not null)
        {
            await RefreshAsync();
        }
    }

    public async Task RefreshAsync()
    {
        await Try(async () =>
        {
            Me = await Client.MeAsync();
            if (Me is null)
            {
                Forget(Strings.RaidexExpired);
                return;
            }
            Routes = await Client.RoutesAsync();
        });
        await ReportAsync();
    }

    /// <summary>
    /// Reports the keys a moment after the addon's file changed - WoW writes
    /// it and its backup in quick succession, one report is enough.
    /// </summary>
    public void ReportSoon()
    {
        _reportSoon?.Cancel();
        _reportSoon?.Dispose();
        var soon = _reportSoon = new CancellationTokenSource();
        _ = Task.Delay(TimeSpan.FromSeconds(2), soon.Token).ContinueWith(
            _ => Avalonia.Threading.Dispatcher.UIThread.Post(async () => await ReportAsync()),
            soon.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    /// <summary>The own keys and the guild keys seen in game to Raidex (plan 7.1), then the guild board back.</summary>
    public async Task ReportAsync()
    {
        if (Me is null || Data is null || KeysReport.From(Data.Data) is not { } report)
        {
            return;
        }
        await Try(async () =>
        {
            LastReport = await Client.ReportAsync(report);
            LastReportAt = DateTimeOffset.Now;
            try
            {
                Board = await Client.GuildBoardAsync();
            }
            catch (RaidexException e) when (e.Code == "no_active_guild")
            {
                Board = null;
            }
        });
    }

    /// <summary>The code being confirmed on the Raidex website while connecting; null otherwise.</summary>
    public ConnectCode? Pending { get; private set; }

    /// <summary>
    /// Connects the app to a Raidex account (plan 6.4): a code from Raidex,
    /// the Raidex website opened to confirm it after its normal login, and
    /// the app asking every few seconds until it is - then the session.
    /// Only outgoing HTTPS; ten minutes at most, as long as the code lives.
    /// </summary>
    public async Task LoginAsync()
    {
        if (_login is not null)
        {
            return;
        }
        _login = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        Problem = null;
        Raise();
        try
        {
            var code = await Client.ConnectStartAsync(_login.Token);
            Pending = code;
            await OpenConnectPageAsync();
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, code.Interval)), _login.Token);
                var (status, session) = await Client.ConnectPollAsync(code.DeviceCode, _login.Token);
                if (status == "pending")
                {
                    continue;
                }
                if (status == "approved" && session is not null)
                {
                    _session = session;
                    SessionStore.Save(Server, session);
                }
                else
                {
                    Problem = status == "denied" ? Strings.RaidexDenied : Strings.RaidexCodeExpired;
                }
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled here, or the code's ten minutes are over.
        }
        catch (RaidexException e)
        {
            Problem = Describe(e);
        }
        finally
        {
            Pending = null;
            _login.Dispose();
            _login = null;
        }
        if (_session is not null)
        {
            await RefreshAsync();
        }
        Raise();
    }

    /// <summary>
    /// The confirmation page - at the start, or again, closed by mistake or
    /// wanted in another browser. Should no browser open, the problem line
    /// names the address, to open by hand.
    /// </summary>
    public async Task OpenConnectPageAsync()
    {
        if (Pending is not { } code)
        {
            return;
        }
        Problem = await Browser.OpenAsync(code.VerifyUrl.ToString())
            ? null
            : string.Format(CultureInfo.CurrentCulture, Strings.RaidexBrowserFailed, code.VerifyUrl);
        Raise();
    }

    public void CancelLogin() => _login?.Cancel();

    public async Task LogoutAsync()
    {
        try
        {
            await Client.LogoutAsync();
        }
        catch (RaidexException)
        {
            // Logged out here all the same; the session expires on the server.
        }
        Forget(null);
        Raise();
    }

    /// <summary>What each member's Mythic+ season looked like, kept for this run of the app.</summary>
    private readonly Dictionary<string, MplusCharacter> _seasons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A guild mate's season for the group planner: their best run per dungeon,
    /// role and item level. Asked once per member and run of the app - Raidex
    /// caches it ten minutes of its own. Null when Raidex has none for them;
    /// unlike every other call this one leaves the pages alone, so the picks
    /// on the Gilde page survive the planning.
    /// </summary>
    public async Task<MplusCharacter?> MplusAsync(string realmSlug, string name)
    {
        var who = realmSlug + ":" + name;
        if (_seasons.TryGetValue(who, out var known))
        {
            return known;
        }
        try
        {
            var season = await Client.MplusCharacterAsync(realmSlug, name);
            _seasons[who] = season;
            Problem = null;
            return season;
        }
        catch (RaidexException e)
        {
            if (e.LoggedOut)
            {
                Forget(Strings.RaidexExpired);
                Raise();
            }
            else if (e.Code is not ("unavailable" or "not_in_guild"))
            {
                // Those two are about this one character - Blizzard has
                // nothing on them, or they are in no guild of ours. The
                // planner names who it could not weigh; saying "Raidex
                // refused" on top would blame the connection for it.
                Problem = Describe(e);
            }
            return null;
        }
    }

    /// <summary>
    /// A call for the raid calendar. Like <see cref="MplusAsync"/> it leaves
    /// the other pages alone - the calendar says its own trouble, and the
    /// planner's picks survive - unless the session is gone: then the app
    /// forgets it, and every page hears so. What failed is thrown on, for the
    /// calendar to say (<see cref="Describe"/>).
    /// </summary>
    public async Task<T> CalendarAsync<T>(Func<RaidexClient, Task<T>> call)
    {
        try
        {
            return await call(Client);
        }
        catch (RaidexException e) when (e.LoggedOut)
        {
            Forget(Strings.RaidexExpired);
            Raise();
            throw;
        }
    }

    /// <summary>Stores a route for the user alone or their guild; true when Raidex took it.</summary>
    public Task<bool> SaveRouteAsync(string mdt, string name, string? uid, int mapId, int pulls, double forces, bool shareWithGuild) =>
        Try(async () =>
        {
            await Client.SaveRouteAsync(mdt, name, uid, mapId, pulls, forces, shareWithGuild);
            Routes = await Client.RoutesAsync();
        });

    public Task<bool> ShareRouteAsync(long id, bool shareWithGuild) =>
        Try(async () =>
        {
            await Client.ShareRouteAsync(id, shareWithGuild);
            Routes = await Client.RoutesAsync();
        });

    public Task<bool> DeleteRouteAsync(long id) =>
        Try(async () =>
        {
            await Client.DeleteRouteAsync(id);
            Routes = await Client.RoutesAsync();
        });

    /// <summary>Runs a call to Raidex; its trouble in <see cref="Problem"/>, the pages told either way.</summary>
    private async Task<bool> Try(Func<Task> call)
    {
        try
        {
            await call();
            Problem = null;
            return true;
        }
        catch (RaidexException e)
        {
            if (e.LoggedOut)
            {
                Forget(Strings.RaidexExpired);
            }
            else
            {
                Problem = Describe(e);
            }
            return false;
        }
        finally
        {
            Raise();
        }
    }

    public static string Describe(RaidexException e) => e.Code switch
    {
        "unreachable" or "timeout" => Strings.RaidexUnreachable,
        "no_active_guild" => Strings.RaidexNoGuild,
        "not_allowed" or "not_admin" => Strings.RaidexNotAllowed,
        "char_required" => Strings.RaidexNoCharacter,
        "bnet_reauth_required" => Strings.RaidexBnetReauth,
        // The route itself is missing: the server has not been deployed with it.
        _ when e.Status == HttpStatusCode.NotFound => Strings.RaidexOutdated,
        _ when e.Status == HttpStatusCode.ServiceUnavailable => Strings.RaidexMaintenance,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.RaidexRefused, e.Code),
    };

    private void Forget(string? problem)
    {
        _session = null;
        Me = null;
        Routes = [];
        Board = null;
        LastReport = null;
        LastReportAt = null;
        Problem = problem;
        _seasons.Clear();
        SessionStore.Clear(Server);
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
