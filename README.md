<p align="center">
  <img src="https://raw.githubusercontent.com/lasse-tech/raidex-keys/main/.github/assets/addon-icon.png" width="128" height="128" alt="Raidex Keys icon">
</p>

<h1 align="center">Raidex Keys</h1>

<p align="center">
  <em>Deine Mythic+-Woche im Blick.</em><br>
  <em>Your Mythic+ week at a glance.</em>
</p>

<p align="center">
  <a href="https://github.com/lasse-tech/raidex-keys/releases"><b>Download</b></a> ·
  <a href="https://github.com/lasse-tech/raidex-keys/issues/new/choose">Report a bug</a> ·
  <a href="https://raidex.app/keys/">raidex.app/keys</a>
</p>

<p align="center">
  <a href="https://github.com/lasse-tech/raidex-keys/releases"><img alt="newest release" src="https://img.shields.io/github/v/release/lasse-tech/raidex-keys?include_prereleases&label=download&color=7C5CFF"></a>
  <a href="https://github.com/lasse-tech/raidex-keys/tree/main/addon/RaidexKeys"><img alt="World of Warcraft 12.1.5" src="https://img.shields.io/badge/WoW-retail%2012.1.5-B8873B"></a>
  <a href="https://github.com/lasse-tech/raidex-keys/blob/main/LICENSE"><img alt="licence" src="https://img.shields.io/badge/licence-MIT-2C7BB6"></a>
  <a href="https://raidex.app/keys/"><img alt="languages" src="https://img.shields.io/badge/languages-DE%20%C2%B7%20EN-3B7DD8"></a>
  <a href="https://raidex.app"><img alt="made with love in Europe" src="https://img.shields.io/badge/made%20with%20%3C3%20in-Europe-003399"></a>
</p>

A World of Warcraft addon for Mythic+: the keys, ratings, best runs and Great
Vault of all your characters, your group's and your guild's keys, the runs to
your next rating goal, a key board, a raid calendar and a Mythic+ timer. Part
of the [Raidex](https://raidex.app) family.

<p align="center">
  <img src="https://raw.githubusercontent.com/lasse-tech/raidex-keys/main/.github/assets/addon-window.png" width="440" alt="The addon's window: your guild's keys this week, grouped by dungeon">
  <img src="https://raw.githubusercontent.com/lasse-tech/raidex-keys/main/.github/assets/addon-timer.png" width="440" alt="The addon's Mythic+ timer during a key">
</p>

More pictures on [raidex.app/keys](https://raidex.app/keys/#einblicke).

## What it brings

**A window with six tabs**

- *Guild*: your guild's keys this week, grouped by dungeon
- *Alts*: every character's key, this week's runs, Great Vault and rating — an alt is in it without logging it in
- *Group*: the keys of the group you are in
- *Rating goals*: the fewest runs to 2500, 3000 and 3500 rating, each at the lowest level that does it, and who holds a key for it
- *Board*: your guild's key board
- *Raids*: your guild's raid calendar
- Left open, the window is back after a `/reload` or a loading screen

**The key board**

- Pin your key for a day and a time, with a note
- Guild members sign up as tank, healer or damage, up to ten, counted against a group of five
- A click on a name whispers them
- Reaches guild members who were offline when the key was pinned

**The raid calendar**

- The raids your guild enters in the game's calendar, today and the next two weeks
- Sign up as tank, healer or DPS with a note — this signs you up in the game's calendar too
- A raid shows who is in, counted by role, with item level and note
- Difficulty and your guild's progress in it
- The raid's creator, a moderator or the guild master sets anyone's role

**A Mythic+ timer while a key runs**

- The time left with the +2 and +3 limits
- Every boss with the time it fell and your best time
- The enemy forces to two decimals
- The deaths and what they cost
- Where your rating would land if the run ended now
- Your own chat names each boss as it falls; at the end the key's holder tells the group how it went. Nothing is said at the start

**A minimap button**

- Tooltip: your key, how many of your group's keys are known, how many keys your guild has this week
- Click opens the window, shift-click the options, ctrl-click shows or hides the timer, right-click links your key in chat

**Options** under *Options → AddOns → Raidex Keys*

- Sharing your key with the guild
- Five colour themes
- Fonts and sizes of window and timer, and a grid to line them up
- What the timer shows, says and looks like
- The addon speaks your client's language, German or English

## Download

The [newest release](https://github.com/lasse-tech/raidex-keys/releases); what
changed is in the [changelog](https://github.com/lasse-tech/raidex-keys/blob/main/CHANGELOG.md).

| File | What it is |
|---|---|
| `raidex-keys-addon-<version>.zip` | the addon. Unpack into `World of Warcraft/_retail_/Interface/AddOns`, so that the folder `RaidexKeys` lands there |
| `SHA256SUMS` | the checksum of that file |
| Source code (zip, tar.gz) | addon and app as of this release, added by GitHub |

## Other addons

- LibKeystone is **not shipped**. If another addon such as BigWigs has loaded
  it, Raidex Keys uses that one; otherwise it brings its own.
- Astral Keys and its relatives can stay where they are.

## Build from source

The desktop app is in [`desktop/`](desktop/). Needs the .NET 10 SDK.

| Linux | Windows | |
|---|---|---|
| `make help` | `Makefile.bat help` | list the targets |
| `make debug` | `Makefile.bat debug` | build unoptimised |
| `make run` | `Makefile.bat run` | start the app |
| `make preview SECTION=guild` | `Makefile.bat preview guild` | start with sample data |
| `make test` | `Makefile.bat test` | tests |
| `make lint` | `Makefile.bat lint` | warning-free build, formatting and the other checks |
| `make install` / `uninstall` | `Makefile.bat install` / `uninstall` | the app for this user |
| `make install-addon ADDONS_DIR=…` | `Makefile.bat install-addon` | the addon into WoW |

Warnings are errors (`desktop/Directory.Build.props`). Packages:
`desktop/packaging/linux/build-deb.sh` (`.deb`) and
`desktop/packaging/windows/build-setup.sh` (Windows installer, needs `nsis`),
arguments in each script's header.

## Report a bug or suggest something

[Open an issue](https://github.com/lasse-tech/raidex-keys/issues/new/choose), in
German or English.

## Support

[Buy me a coffee](https://buymeacoffee.com/gossardla)

## Licence

Copyright © 2026 lasse tech, Lars Gossard · <lars@lasse-tech.de>

- The addon: under the [MIT License](https://github.com/lasse-tech/raidex-keys/blob/main/LICENSE), without any warranty
- The desktop app: under the [MIT License](https://github.com/lasse-tech/raidex-keys/blob/main/desktop/LICENSE), without any warranty
- Fonts: SIL Open Font License (`desktop/src/RaidexKeys.App/Assets/Fonts`, `addon/RaidexKeys/Media`); the app's third-party components: `desktop/packaging/THIRD-PARTY-NOTICES.txt`
- The Keys module on the server: part of [Raidex](https://github.com/lasse-tech/raidex), also lasse tech's, under the MIT License
- World of Warcraft is a trademark of Blizzard Entertainment, Inc.; Raidex Keys is not affiliated with Blizzard
