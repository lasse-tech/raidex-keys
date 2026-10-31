// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using RaidexKeys.App.Localization;
using RaidexKeys.Core;

namespace RaidexKeys.App;

/// <summary>Puts the interface into German or English.</summary>
/// <remarks>
/// German and English are the two languages the app speaks. "System" follows
/// the machine: a German system gets German, everything else English.
/// Numbers and dates follow the interface language, so a German window never
/// shows "101.4%" beside German labels.
/// </remarks>
internal static class Language
{
    /// <summary>The machine's language, captured before the first override.</summary>
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;

    public static bool IsGerman(UiLanguage language) => language switch
    {
        UiLanguage.German => true,
        UiLanguage.English => false,
        _ => SystemCulture.TwoLetterISOLanguageName == "de",
    };

    public static void Apply(UiLanguage language)
    {
        var culture = CultureInfo.GetCultureInfo(IsGerman(language) ? "de-DE" : "en-GB");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Strings.Culture = culture;
    }
}
