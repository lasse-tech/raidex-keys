// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace RaidexKeys.Core.Raidex;

/// <summary>
/// Where the Raidex session is kept between runs (plan 6.4): the system's
/// secret store - libsecret through secret-tool on Linux, DPAPI on Windows -
/// and a file only the user may read where there is none.
/// </summary>
public static class SessionStore
{
    public static string? Load(Uri server)
    {
        if (OperatingSystem.IsLinux() && SecretTool(["lookup", .. Attributes(server)], null) is { Length: > 0 } secret)
        {
            return secret.Trim();
        }
        try
        {
            var file = FilePath(server);
            if (!File.Exists(file))
            {
                return null;
            }
            if (OperatingSystem.IsWindows())
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser));
            }
            return File.ReadAllText(file).Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
    }

    public static void Save(Uri server, string session)
    {
        Clear(server);
        if (OperatingSystem.IsLinux() && SecretTool(["store", $"--label=Raidex Keys ({server.Host})", .. Attributes(server)], session) is not null)
        {
            return;
        }
        Directory.CreateDirectory(AppSettings.Directory);
        var file = FilePath(server);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllBytes(file, ProtectedData.Protect(Encoding.UTF8.GetBytes(session), null, DataProtectionScope.CurrentUser));
            return;
        }
        File.WriteAllText(file, "");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.WriteAllText(file, session);
    }

    public static void Clear(Uri server)
    {
        if (OperatingSystem.IsLinux())
        {
            SecretTool(["clear", .. Attributes(server)], null);
        }
        try
        {
            File.Delete(FilePath(server));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing to forget, or not ours to delete: either way the session is not used again.
        }
    }

    private static string[] Attributes(Uri server) => ["application", "raidex-keys", "server", server.Host];

    private static string FilePath(Uri server) => Path.Combine(AppSettings.Directory, $"session-{server.Host}");

    /// <summary>secret-tool's answer, or null when it is not installed, refuses or takes too long.</summary>
    private static string? SecretTool(string[] args, string? input)
    {
        try
        {
            var info = new ProcessStartInfo("secret-tool")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }
            if (input is not null)
            {
                process.StandardInput.Write(input);
            }
            process.StandardInput.Close();
            // Read while waiting, not before it: ReadToEnd returns only once
            // secret-tool has exited, so the limit below never applied - and a
            // secret service D-Bus could not start kept it running for 25 s.
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
