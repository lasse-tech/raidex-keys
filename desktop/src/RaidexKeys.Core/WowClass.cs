// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core;

/// <summary>Playable classes, numbered as the game numbers them (classId).</summary>
public enum WowClass
{
    Warrior = 1,
    Paladin = 2,
    Hunter = 3,
    Rogue = 4,
    Priest = 5,
    DeathKnight = 6,
    Shaman = 7,
    Mage = 8,
    Warlock = 9,
    Monk = 10,
    Druid = 11,
    DemonHunter = 12,
    Evoker = 13,
}

public static class ClassColors
{
    /// <summary>
    /// The game's own class colours. The brand guide leaves them untouched
    /// ("Klassenfarben bleiben unangetastet"), so they are not themed.
    /// </summary>
    public static string Hex(WowClass wowClass) => wowClass switch
    {
        WowClass.Warrior => "#C69B6D",
        WowClass.Paladin => "#F48CBA",
        WowClass.Hunter => "#AAD372",
        WowClass.Rogue => "#FFF468",
        WowClass.Priest => "#FFFFFF",
        WowClass.DeathKnight => "#C41E3A",
        WowClass.Shaman => "#0070DD",
        WowClass.Mage => "#3FC7EB",
        WowClass.Warlock => "#8788EE",
        WowClass.Monk => "#00FF98",
        WowClass.Druid => "#FF7C0A",
        WowClass.DemonHunter => "#A330C9",
        WowClass.Evoker => "#33937F",
        _ => "#E8EAF6",
    };
}
