// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Tests;

public class AppSettingsTests
{
    /// <summary>What RAIDEXKEYS_SERVER and settings.json may name, and the server each one means.</summary>
    [Theory]
    [InlineData("prod", "https://raidex.app/")]
    [InlineData(" PROD ", "https://raidex.app/")]
    [InlineData("https://raidex.example", "https://raidex.example/")]
    [InlineData("http://localhost:8080/raidex", "http://localhost:8080/raidex/")]
    [InlineData("", null)]
    [InlineData("raidex.example", "https://raidex.example/")]
    [InlineData("not a server", null)]
    [InlineData("ftp://raidex.app/", null)]
    public void ParsesAServer(string value, string? expected) =>
        Assert.Equal(expected, AppSettings.ParseServer(value)?.ToString());
}
