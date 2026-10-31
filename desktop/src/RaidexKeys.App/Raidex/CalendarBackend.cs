// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Raidex;

namespace RaidexKeys.App.Raidex;

/// <summary>
/// Where the raid calendar comes from: the Raidex account, or the preview's
/// sample guild. A call that fails throws <see cref="RaidexException"/>.
/// </summary>
public interface ICalendarBackend
{
    /// <summary>Connected to Raidex: the calendar can be shown at all.</summary>
    bool IsConnected { get; }

    /// <summary>The active character's name in Raidex, to sign up with first; null when unknown.</summary>
    string? ActiveCharacter { get; }

    event EventHandler? ConnectionChanged;

    Task<IReadOnlyList<RaidEvent>> RaidsAsync(bool past);
    Task<RaidEvent> CreateAsync(RaidDraft draft);
    Task<RaidEvent> UpdateAsync(long id, RaidDraft draft);
    Task DeleteAsync(long id);
    Task<RaidEvent> SignupAsync(long raidId, SignupDraft signup);
    Task<RaidEvent> WithdrawAsync(long raidId);
    Task<RaidEvent> RemoveSignupAsync(long raidId, long signupId);
    Task<Uri> ShareAsync(long raidId);
    Task<GuideList> GuidesAsync(string instance);
    Task AddGuideAsync(string instance, string title, string url);
    Task DeleteGuideAsync(long id);
    Task<IReadOnlyList<JournalInstance>> JournalAsync(bool dungeons);
    Task<IReadOnlyList<AccountCharacter>> CharactersAsync();
}

/// <summary>The calendar of the guild active in the connected Raidex account.</summary>
public sealed class LiveCalendar : ICalendarBackend
{
    private static RaidexAccount Account => RaidexAccount.Instance;

    public LiveCalendar() => Account.Changed += (_, _) => ConnectionChanged?.Invoke(this, EventArgs.Empty);

    public bool IsConnected => Account.Me is not null;

    public string? ActiveCharacter => Account.Me?.Character;

    public event EventHandler? ConnectionChanged;

    public Task<IReadOnlyList<RaidEvent>> RaidsAsync(bool past) => Account.CalendarAsync(c => c.RaidsAsync(past));
    public Task<RaidEvent> CreateAsync(RaidDraft draft) => Account.CalendarAsync(c => c.CreateRaidAsync(draft));
    public Task<RaidEvent> UpdateAsync(long id, RaidDraft draft) => Account.CalendarAsync(c => c.UpdateRaidAsync(id, draft));
    public Task DeleteAsync(long id) => Account.CalendarAsync(async c => { await c.DeleteRaidAsync(id); return true; });
    public Task<RaidEvent> SignupAsync(long raidId, SignupDraft signup) => Account.CalendarAsync(c => c.SignupAsync(raidId, signup));
    public Task<RaidEvent> WithdrawAsync(long raidId) => Account.CalendarAsync(c => c.WithdrawAsync(raidId));
    public Task<RaidEvent> RemoveSignupAsync(long raidId, long signupId) => Account.CalendarAsync(c => c.RemoveSignupAsync(raidId, signupId));
    public Task<Uri> ShareAsync(long raidId) => Account.CalendarAsync(c => c.ShareRaidAsync(raidId));
    public Task<GuideList> GuidesAsync(string instance) => Account.CalendarAsync(c => c.GuidesAsync(instance));
    public Task AddGuideAsync(string instance, string title, string url) =>
        Account.CalendarAsync(async c => { await c.AddGuideAsync(instance, title, url); return true; });
    public Task DeleteGuideAsync(long id) => Account.CalendarAsync(async c => { await c.DeleteGuideAsync(id); return true; });
    public Task<IReadOnlyList<JournalInstance>> JournalAsync(bool dungeons) => Account.CalendarAsync(c => c.JournalAsync(dungeons));
    public Task<IReadOnlyList<AccountCharacter>> CharactersAsync() => Account.CalendarAsync(c => c.CharactersAsync());
}
