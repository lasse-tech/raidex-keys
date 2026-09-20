# Changelog

What changed in each release of the Raidex Keys addon. The files are on
[GitHub releases](https://github.com/incudex/raidex-keys/releases).

## 0.8.0 – 2026-09-20

- Hovering a row shows a **Raidex Keys mark** behind the name of everyone who
  runs the addon too.
- In your own pinned key you can **set your role** — tank, healer or damage.
  The key holder stands in the group's list as its first member.
- A key can be **pinned for several days at once**. Pinning it again no longer
  takes the earlier posting off the board.
- The open window **no longer makes the game stutter**.
- A key with many sign-ups **scrolls** its list, by wheel or by its bar. Names
  past the eighth used to stand behind the buttons.

## 0.7.0 – 2026-09-15

- Raid calendar: **guild progress is there at once**, in either client
  language. A raid stayed at 0/8, or without progress, until the game's
  achievement window had been opened. Best progress known survives a `/reload`.
- Key board: postings and sign-ups **reach the guild at once**. A message over
  the game's shared throttle was dropped and came up to 15 minutes later.
- New installs start with the **Slate** theme, window text at size 12, timer at
  80 % opacity. Settings already made stay; Defaults and `/rk reset` bring the
  new ones.
- The window's **size moves on the grid** while its lower right corner is
  dragged. "Grid when resizing" switches that off.
- A posting's **key level wears the theme's colour**; it stayed Raidex violet.

## 0.6.0 – 2026-09-15

- A **raid calendar** in the new tab *Raids*:
  - The raids your guild enters in the game's calendar as guild events of the
    type raid, today and the next two weeks.
  - Sign up as tank, healer or damage — which the game's calendar cannot.
    It signs you up there too, and taking it back takes it back there.
  - Whoever is signed up in the game's calendar shows up as well, without a
    role. The raid's creator, a moderator or the guild master picks one by
    clicking a name, and can change any role that way.
  - A click on a raid shows who is in, counted as tanks, healers and damage:
    name (its creator with a crown), role icon, item level.
  - Under the title: the raid, its difficulty, the guild's progress from the
    guild's achievements (3/8), and the note of the game's event.
  - Your sign-up takes a note of its own; a sign-up's note shows in the status
    bar under the mouse.
  - A button per role signs you up or changes your role; a click on a name
    whispers them.
  - The list shows each raid's difficulty and a square per boss, filled for
    those your guild has down.
- The tabs are narrower, so six fit beside "Post your key".
- The timer **says nothing at the start** of a key any more — the game
  swallowed those lines anyway. The end still reaches the group; the option is
  now called "The run's end in the group chat".
- **Put away, the timer says nothing**: no boss times, no end.
- A timer **switched off in the options stays off**. The next key turned it on
  again, and its chat lines with it (a player's report). Shift-click on the
  timer and ctrl-click on the minimap button still hide it until the next key.
- Ctrl-click on the minimap button **hides and shows the timer without a chat
  line**.
- A timer switched off in the options **keeps no boss times from the chest**:
  it wrote the chest's time as every open boss's best time.
- The options' **Defaults** leaves the affixes off, as the timer ships, and
  puts the preview's level back to +12.
- The mouse wheel over an open posting **no longer scrolls the list behind it**.
- Escape **ends `/rk preview`** as its button does.

## 0.5.0 – 2026-09-15

- Five **colour themes** for window, key board, timer and minimap tooltip:
  Raidex, Nord — Polar Night, Ember, Verdant, Slate. Choosing one sets the
  timer's background and text colour to match; both can be changed afterwards.
- **More fonts**, the same list for window and timer, each chosen on its own:
  PT Sans Narrow ships with the addon; Expressway, Homespun, Continuum Medium,
  Die Die Die! and Action Man appear when ElvUI or a SharedMedia addon has them.
- **Pick your main** in the Alts tab: the rating goals are then planned on its
  runs, and it wears the glow edge. The timer stays with the logged-in character.
- The key board's **times and days read as you choose** (Options → AddOns →
  Raidex Keys → Window): 24 or 12 hours, and 13.09., 09/13 or 2026-09-13.
- The key board **counts the key holder in**: a fresh posting reads 1/5.
- **Every column sorts** in Alts, Group and Guild, Week, Vault and Updated
  included; figures and times start with the most. Your own character no longer
  stays at the top whatever the order, and the arrow stands by its own head —
  on Rating it stood by Updated.
- Sorted by dungeon, the Guild tab **keeps its Dungeon column**, head and arrow
  included, so a second click turns the groups around.
- The **Group tab is greyed out** while you are in no group.
- **Buttons look like buttons**, on a face of their own inside a thin edge.
- The hints "Click a name to whisper.", "Right-click: link key in chat" and
  "Every run in time" are gone; the clicks still do what they did. The goals'
  footer names the character instead.
- The **tabs stand on a bar** of their own, with more room around them, and
  light up under the mouse.

## 0.4.1 – 2026-09-13

- The **key board** shows its times in **realm time**, as the game's calendar
  does. A member in another time zone saw every posting an hour off. Late in
  the evening the form now opens on tomorrow instead of today's small hours.
- The **Group tab** shows the keys of group members the guild already told this
  week, not only the answers from the group itself, and asks again after a
  `/reload`.

## 0.4.0 – 2026-09-13

- A new **Rating goals** tab: the fewest runs to 2500, 3000 and 3500 M+ rating,
  each at the lowest level that does it, with what each adds, and who holds a
  key for it among your alts, your group or your guild.
- The **Board** tab is your guild's key board:
  - Pin your key for a day and a time with a note.
  - Guild members sign up as tank, healer or damage, ten at most, so you can
    still talk it over with those who come too late.
  - The count reads against a group of five: yellow below, green at 5/5, red
    above, as in 7/5.
  - A click on a name whispers them.
  - It reaches guild members who were offline when the key was pinned.
- A new **window**, as the design draws it: the tabs Alts, Group and Guild over
  one table — character, dungeon, key, this week's runs, Great Vault, M+ rating,
  the biggest key first. Over it the week's affixes and the time to the reset.
  Left-click on the minimap button opens it (options moved to shift-click), as
  does `/rk window`. Every alt is in it without logging that alt in.
- The **column heads sort** the table, a second click turns the order around.
  Sorted by dungeon, the Guild tab **groups the keys** and counts them. A click
  on a tab puts its order back.
- The Alts tab shows **this week's runs** as a square each, filled where the key
  was timed, and the **item level** the best unlocked Great Vault slot gives.
  Every tab says **how long ago** a row was last heard of.
- **Your own character stands first** and wears the violet edge, in Alts and Group.
- A **right-click on a row** puts its key into the chat: your own as the link
  from your bags, anyone else's as name, dungeon and level.
- The window **stays open** over a `/reload` and a loading screen when you left
  it open. Closed with its button, Escape or the minimap, it stays closed.
- Guild names **wear their class colour at once**, from the guild roster; they
  stood white for seconds after every `/reload`.
- The **rating column's head fits**: German "WERTUNG" and the board's
  "SIGNED UP" were cut short.
- The **Guild tab counts you in**, so it holds real keys from the first day in a
  guild. Opening the window asks the guild again, at most once a minute.
- The **footer says what is stored**, never what is on screen: characters, the
  group with the keys known in it, the guild with its keys of the week. Where
  nothing is stored it says so.
- Two alts of the same name on different realms are told apart by the realm.
- A **Mythic+ timer** while a keystone runs:
  - Level and dungeon, the affixes, the time left with the +2 and +3 limits.
  - Every boss with the time it fell and your best time for it, kept per
    dungeon and key level.
  - The enemy forces to two decimals, the deaths and the time they cost.
  - One line keeps saying what the run would be worth if it ended now: its
    dungeon rating, and what it adds over your best run there. Deaths so far
    count, since they are in the clock; what is still to come does not.
  - A `/reload` mid-run keeps the times already taken.
  - The game's own timer is faded out while it shows.
  - **The card stays up after the run**, until you leave the dungeon or start
    the next key. The game drops the keystone the moment the chest appears, and
    the timer went with it.
  - `/rk timer` shows a made-up run to drag the display into place.
- **The run speaks in the chat**: the key linked in the group chat by whoever
  holds it, so it comes once however many run the addon, and "Timer started,
  30 minutes left". After every boss you alone read when it fell and where your
  M+ rating would land. At the end the key's holder tells the group how it went.
  Both can be switched off. While the game keeps addons out of the chat, the
  lines are shown to you instead of being refused.
- The key's **start is said to you**, not to the group: during a key the game
  keeps addons out of the group chat, and the lines never arrived.
- The **end reaches the group** again: the holder's closing line waits until the
  game lets addons speak after the chest, a minute at most.
- At the chest **no boss is announced twice**: the game stops the run's clock
  just before it says the run is done, and the timer took that for a new run.
- The window's **font and size are yours** (Options → AddOns → Raidex Keys):
  Marcellus, which now ships with the addon, or the game's Friz Quadrata or
  Arial Narrow; a font size that takes rows, columns and window along, and a
  grip in the lower right corner — never below what the columns need.
- The timer's look is **yours to set**: how much of the dungeon shows through,
  the font out of the five the game ships, the text size, background and text
  colour. The grey tones follow the colours you pick; the colours that mean
  something — keystone level, clock, the time deaths cost — stay. `/rk reset`
  puts the designed look back.
- Window and timer **move on a grid**: dragged, they go in steps of 10 points
  and line up with each other. 20, 30, 40 or off, set in the options.
- The **options are grouped**: window and timer have pages of their own, and the
  timer's page is divided into what it does during a key, what it says in the
  chat, the preview and its look.
- While the options stand open, the timer's preview shows **any dungeon of the
  season at any level**, or a line of umlauts, to see what the font makes of them.
- `/rk preview` shows **every surface with sample values at once** — window,
  minimap tooltip, timer — so each piece can be judged and dragged into place
  without a keystone, a group or a guild. A tab with nothing to show says **why
  it is empty** rather than standing on made-up rows.
- The tabs read **Guild, Alts, Group**. The affixes are set small and stop
  before the countdown; name and dungeon have more room. **Options** in the
  header can be clicked — the frame that drags the window lay over it.
- The column heads **all show**; three of four stood empty. Name and dungeon
  share what is left, and where no dungeon is on screen the name has it all. A
  **scrollbar** stands beside a list longer than the window.
- The panel's **Defaults** puts back what is not a setting as well: where timer,
  window and minimap button stand, and the timer's two colours. `/rk reset` too.
- The ticks beside the bosses are **drawn**, not written: Friz Quadrata has no
  glyph for them, so in game they were empty boxes.
- The timer wears the **design's card**: a dark panel with a violet edge, rules
  between its parts, the clock as a bar with the +2 and +3 limits marked, and
  the enemy forces with a bar of their own.
- **"Hide the quest tracker"**: with it ticked, tracked quests, campaigns and
  achievements go while a keystone runs and come back when it ends, the "All
  Objectives" bar with them. Ships unticked.

## 0.1.12 – 2026-09-12

- Stores what each keystone level rewards this season. Midnight's client answers
  only the Great Vault's level, and only once it feels like it — the addon asks
  again until it does, and drops the table when a new season starts.
- `/rk` says how many reward levels are known, `/rk debug` what the game answers
  right now and what was stored from it.

## 0.1.9 – 2026-09-12

Initial release.

- Records keystone, rating, best runs and vault of every character.
- Collects guild keys and shares yours with your guild — switched off in the
  options, if you like.
- A minimap button: its tooltip shows your key, how many of your group's keys
  are known and how many your guild has this week; a right-click links your key
  in chat. `/rk minimap` hides or shows it.
- `/rk` tells what it has stored, `/rk options` opens its options.
