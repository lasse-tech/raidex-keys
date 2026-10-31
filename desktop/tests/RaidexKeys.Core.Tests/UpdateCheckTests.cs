// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Updates;

namespace RaidexKeys.Core.Tests;

/// <summary>Which version counts as newer (plan 6.10).</summary>
public sealed class UpdateCheckTests
{
    [Theory]
    // The one a string comparison gets backwards: 10 is after 9.
    [InlineData("0.1.10", "0.1.9", true)]
    [InlineData("0.1.9", "0.1.10", false)]
    [InlineData("0.14.0", "0.13.0", true)]
    [InlineData("0.13.0", "0.13.0", false)]
    [InlineData("1.0", "0.13.0", true)]
    [InlineData("v0.14.0", "0.13.0", true)]
    // Nothing to compare is never news: an unreachable page must not nag.
    [InlineData(null, "0.13.0", false)]
    [InlineData("", "0.13.0", false)]
    [InlineData("nightly", "0.13.0", false)]
    public void NewerCountsTheNumbers(string? there, string here, bool newer) =>
        Assert.Equal(newer, UpdateCheck.Newer(there, here));

    [Fact]
    public async Task AnUnreachablePageIsNoNews()
    {
        // A port nothing listens on: the check answers, and answers "nothing".
        var nowhere = new Uri("http://127.0.0.1:9/");
        var found = await UpdateCheck.RunAsync(nowhere, "0.13.0", "0.1.12", nowhere);

        Assert.False(found.Any);
        Assert.Equal("0.1.12", found.AddonHere);
    }

    [Fact]
    public void TheNewestAddonReleaseCountsNotTheAppsOrADraft()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
            [
              { "tag_name": "addon-v0.9.0", "draft": true },
              { "tag_name": "addon-v0.8.0", "draft": false, "prerelease": true },
              { "tag_name": "addon-v0.10.0-rc", "draft": false },
              { "tag_name": "v0.17.0", "draft": false },
              { "tag_name": "addon-v0.7.0", "draft": false }
            ]
            """);
        Assert.Equal("0.10.0-rc", UpdateCheck.NewestAddon(json.RootElement));
    }
}
