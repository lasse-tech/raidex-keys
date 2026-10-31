// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RaidexKeys.Core.SavedVariables;

/// <summary>A sign-up or withdrawal noted in the app for a raid of the game's calendar.</summary>
/// <param name="File">The RaidexKeys.lua of the character's WoW account.</param>
/// <param name="Action">"signup" or "withdraw".</param>
/// <param name="Role">'T', 'H' or 'D'; unused for a withdrawal.</param>
public sealed record SignupRequest(string Id, string File, string Guid, string Character, string Guild,
    string RaidKey, string RaidTitle, DateTimeOffset RaidAt, string Action, char Role, string? Note, DateTimeOffset At);

/// <summary>
/// RaidexKeysInbox in the addon's file: the requests the addon asks for at the
/// next login (addon Core/Inbox.lua). Only that global is replaced; the rest
/// of the file stays as the game wrote it.
/// </summary>
public static partial class SignupInbox
{
    public const string Global = "RaidexKeysInbox";

    [GeneratedRegex(@"^RaidexKeysInbox\s*=", RegexOptions.Multiline)]
    private static partial Regex Start();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*\s*=", RegexOptions.Multiline)]
    private static partial Regex AnyGlobal();

    /// <summary>The ids of the requests in the file now.</summary>
    public static HashSet<string> Pending(string file)
    {
        var globals = LuaParser.ParseGlobals(System.IO.File.ReadAllText(file));
        return globals.TryGetValue(Global, out var inbox) && inbox is LuaTable t && t.Table("requests") is { } requests
            ? requests.Entries.Keys.OfType<string>().ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Writes these requests as the file's inbox, atomically.</summary>
    public static void Write(string file, IReadOnlyCollection<SignupRequest> requests)
    {
        var text = System.IO.File.ReadAllText(file);
        var result = Replace(text, requests);
        // Never write a file the game could not read back.
        var globals = LuaParser.ParseGlobals(result);
        if (!globals.ContainsKey("RaidexKeysDB") && LuaParser.ParseGlobals(text).ContainsKey("RaidexKeysDB"))
        {
            throw new InvalidOperationException($"{file}: RaidexKeysDB lost while writing the inbox");
        }
        var temp = file + ".raidexkeys.tmp";
        System.IO.File.WriteAllText(temp, result, new UTF8Encoding(false));
        System.IO.File.Move(temp, file, overwrite: true);
    }

    /// <summary>The file's text with its inbox replaced; no inbox at all when there are no requests.</summary>
    public static string Replace(string text, IReadOnlyCollection<SignupRequest> requests)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (Start().Match(text) is { Success: true } start)
        {
            var next = AnyGlobal().Match(text, start.Index + start.Length);
            var end = next.Success ? next.Index : text.Length;
            text = text[..start.Index] + text[end..];
        }
        if (requests.Count == 0)
        {
            return text;
        }
        if (text.Length > 0 && !text.EndsWith('\n'))
        {
            text += newline;
        }
        return text + Lua(requests, newline);
    }

    private static string Lua(IEnumerable<SignupRequest> requests, string nl)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(Global).Append(" = {").Append(nl).Append("\t[\"requests\"] = {").Append(nl);
        foreach (var r in requests.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            sb.Append("\t\t[").Append(Quote(r.Id)).Append("] = {").Append(nl);
            void Field(string name, string value) =>
                sb.Append("\t\t\t[\"").Append(name).Append("\"] = ").Append(value).Append(',').Append(nl);
            Field("guid", Quote(r.Guid));
            Field("guild", Quote(r.Guild));
            Field("key", Quote(r.RaidKey));
            Field("action", Quote(r.Action));
            Field("role", Quote(r.Role.ToString()));
            if (r.Note is { Length: > 0 } note)
            {
                Field("note", Quote(note));
            }
            Field("at", r.At.ToUnixTimeSeconds().ToString(c));
            sb.Append("\t\t},").Append(nl);
        }
        sb.Append("\t},").Append(nl).Append('}').Append(nl);
        return sb.ToString();
    }

    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                _ when ch < ' ' => $"\\{(int)ch:000}",
                _ => ch.ToString(),
            });
        }
        return sb.Append('"').ToString();
    }
}

/// <summary>
/// The app's own list of requests (signups.json beside the settings). The
/// game writes the addon's file at logout from memory, so a request written
/// while it runs may be lost; <see cref="Sync"/> writes it again after each
/// save, until the addon answered it.
/// </summary>
public sealed class SignupQueue
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;
    private readonly Lock _lock = new();
    private List<SignupRequest> _requests;

    public SignupQueue(string? path = null)
    {
        _path = path ?? Path.Combine(AppFolders.Config, "signups.json");
        try
        {
            _requests = System.IO.File.Exists(_path)
                ? JsonSerializer.Deserialize<List<SignupRequest>>(System.IO.File.ReadAllText(_path), Json) ?? []
                : [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _requests = [];
        }
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SignupRequest> Pending
    {
        get
        {
            lock (_lock)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Notes a request; one per character and raid, a newer replaces the older.</summary>
    public void Add(SignupRequest request)
    {
        lock (_lock)
        {
            _requests.RemoveAll(r => r.Guid == request.Guid && r.RaidKey == request.RaidKey);
            _requests.Add(request);
            Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string id)
    {
        lock (_lock)
        {
            _requests.RemoveAll(r => r.Id == id);
            Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Drops what the addon answered and what has passed, and brings every
    /// file's inbox to what is noted for it. The files of <paramref name="data"/>.
    /// </summary>
    public void Sync(AddonData data, DateTimeOffset now)
    {
        List<SignupRequest> wanted;
        var dropped = false;
        lock (_lock)
        {
            dropped = _requests.RemoveAll(r => data.InboxAnswers.ContainsKey(r.Id) || r.RaidAt + TimeSpan.FromHours(6) < now) > 0;
            if (dropped)
            {
                Save();
            }
            wanted = [.. _requests];
        }
        foreach (var file in data.Sources.Distinct())
        {
            try
            {
                var mine = wanted.Where(r => r.File == file).ToList();
                if (!SignupInbox.Pending(file).SetEquals(mine.Select(r => r.Id)))
                {
                    SignupInbox.Write(file, mine);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
            {
                // Tried again after the next save.
            }
        }
        if (dropped)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        System.IO.File.WriteAllText(_path, JsonSerializer.Serialize(_requests, Json));
    }
}
