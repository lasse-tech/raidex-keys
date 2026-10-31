// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Reflection;

namespace RaidexKeys.Core;

/// <summary>Version and build number of the running app.</summary>
/// <remarks>
/// The build number is the commit count of the state built
/// (desktop/Directory.Build.targets); "dev" when it was built without git.
/// </remarks>
public static class BuildInfo
{
    private static Assembly App => Assembly.GetEntryAssembly() ?? typeof(BuildInfo).Assembly;

    /// <summary>"0.2.1".</summary>
    public static string Version => App.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>"58", or "dev".</summary>
    public static string BuildNumber => Metadata("BuildNumber") is { Length: > 0 } build ? build : "dev";

    /// <summary>Built for the Microsoft Store (-p:StoreBuild=true).</summary>
    public static bool Store => Metadata("StoreBuild") == "true";

    private static string? Metadata(string key) => App.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == key)?.Value;
}
