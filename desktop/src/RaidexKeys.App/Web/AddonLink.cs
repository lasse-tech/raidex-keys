// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.App.Localization;
using RaidexKeys.Core;
using RaidexKeys.Core.Web;

namespace RaidexKeys.App.Web;

/// <summary>Where the empty pages send people for the addon.</summary>
public static class AddonLink
{
    public static string Text => BuildInfo.Store ? Strings.EmptyNoDataLinkStore : Strings.EmptyNoDataLink;

    public static string Url => BuildInfo.Store ? WebLinks.AddonCurseForge : WebLinks.AddonPage;
}
