# Changelog

What changed in each release of Raidex Keys, app and addon. The files are on
[GitHub releases](https://github.com/lasse-tech/raidex-keys/releases).

## 0.18.0 – 2026-11-06

**App**

- The same five **colour themes** as the addon, under Einstellungen: Raidex,
  Nord — Polar Night, Ember, Verdant and Slate. Slate is the default.
- The raid calendar switches between **Raidex** and **WoW**: the guild raids
  of the game's calendar with the addon's sign-ups, roles, item levels and
  boss progress, as of the last logout or `/reload`.
- **Sign up for a raid of the game's calendar in the app**: pick character,
  role and note; the addon asks at that character's next login and applies it
  with one click.
- The app says when a newer addon is out and links to its **CurseForge** page.
- A route from your own Mythic Dungeon Tools can be **saved in Raidex** straight
  from its row, not only a pasted string.
- A route saved in Raidex no longer carries the name of whoever exported it.
- Without data yet, the overview offers a look at **sample data**.
- `RAIDEXKEYS_SERVER` takes `prod` or a server's address; the `dev` shortcut
  is gone.

**Addon**

- The **first Shift- or Ctrl-click after a `/reload` works again**. It was
  swallowed: the game does not hand the keyboard state to a freshly loaded
  interface, so the addon read "no key held" while the key was down. It now
  listens for the key itself — on the minimap button and on the timer card.
- **No more "script ran too long" in delves.** The guild's raid progress was
  read from all its achievements in one go, and in a delve the game asks for
  that every few seconds. The read is now spread over several frames and
  waits while you are in combat.
- Interface **120105** (12.1.5). Specialization, whispers, chat links and
  party messages use the current game API (`C_SpecializationInfo`,
  `ChatFrameUtil`, `C_ChatInfo`) instead of the deprecated globals.
- Not for World of Warcraft: Forever: it has no Mythic+ (`Blizzard_ChallengesUI`
  does not load there).

## Addon 0.8.0 – 2026-09-20

- Hovering a row shows a **Raidex Keys mark** behind the name of everyone who
  runs the addon too.
- In your own pinned key you can **set your role** — tank, healer or damage.
  The key holder stands in the group's list as its first member.
- A key can be **pinned for several days at once**. Pinning it again no longer
  takes the earlier posting off the board.
- The open window **no longer makes the game stutter**.
- A key with many sign-ups **scrolls** its list, by wheel or by its bar. Names
  past the eighth used to stand behind the buttons.

## Addon 0.7.0 – 2026-09-15

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

## Addon 0.6.0 – 2026-09-15

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

## Addon 0.5.0 – 2026-09-15

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

## 0.17.0 – 2026-09-13

**App**

- Without **Mythic Dungeon Tools** installed, the app no longer reads
  everything anew every five seconds. The pages were rebuilt each time: the
  class icons blinked, and a text field lost its focus while typing, so a
  raid could not be created.
- The **raid calendar** counts tanks, healers and damage dealers, as Raidex
  does. The fourth tile, which added up everyone signed up, is gone.
- Signing up for a raid always takes a **role**: the form starts without one
  and signs nobody up until tank, healer or damage is picked. Until now it
  picked damage for everyone who did not change it.

**Addon**

- The **key board** shows its times in **realm time**, as the game's calendar
  does. A member whose computer stood in another time zone saw every posting
  an hour off - 21:15 for 22:15. Late in the evening, the form now opens on
  tomorrow instead of on today's small hours.
- The **group tab** shows the keys of group members the guild already told
  this week, not only the answers from the group itself - and asks the group
  again after a /reload.

## 0.16.1 – 2026-09-13

**App**

- When Raidex sends a **fresh guild board**, the page on screen stays where it
  is. Until now the app lost track of which page was open the moment the
  board came in.

**Addon**

- Unchanged (0.4.0). The public repository's README now shows what the addon
  does on its own, with pictures of its window and its timer.

## 0.16.0 – 2026-09-13

**App**

- The **raid calendar** from Raidex has a page of its own, **Raidkalender**:
  your guild's upcoming and past raids, who is coming in which role and who
  sits on the bench. Sign up as main or alt with character, role, state and
  a note, withdraw again, and - as far as Raidex lets you - create, change,
  delete and share raids. The tab **Guides** lists the guides of every
  instance, grouped by who added them; guild master, raid leads and
  quartermasters add and remove them there. The page needs a Raidex
  connection - without one its sidebar entry is greyed out.
- Where the app has no data yet, "Installiere das Addon Raidex Keys" is a
  **link to the addon's page** on raidex.app.
- The **settings stand on tabs**: Allgemein (language, font), Raidex (the
  connection), Planung (rating goal, group planner) and AddOns-Ordner.
- **Another Raidex server for one run**: `RAIDEXKEYS_SERVER=dev raidex-keys`
  talks to Raidex's development system, `prod` or a full address work too.
  While the app talks to anything but raidex.app, a pill under the sidebar
  says so.

**Addon**

- A new **Zielwertung** tab: the fewest runs that lift your
  character to 2500, 3000 and 3500 M+ rating, each at the lowest level that
  does it, with what each adds - and who holds a key for it: your alts, your
  group or your guild.
- The **Pinnwand** tab is your guild's key board. Pin your key for a day and a
  time with a note - "a few more keys afterwards" - and guild members sign up
  for it, as tank, healer or damage, ten at most, so you can still talk it
  over with those who come too late. The count reads against a group of five -
  yellow below it, green at 5/5, red when more have signed up, as in 7/5. A click on a name whispers them. The
  board travels through the guild's addon channel, so it reaches members who
  were offline when a key was pinned; Raidex and the desktop app are not
  involved yet.
- The **window stays open** over a `/reload` and a loading screen when you
  left it open. Closed with its button, Escape or the minimap, it stays closed.
  Guild names **wear their class colour at once**: the classes from the guild
  roster are kept, where they stood white for seconds after every `/reload`.
- The **rating column's head fits**: "WERTUNG" and the board's "SIGNED UP"
  were wider than their column and cut short.
- The key's **start is said to you** now, not to the group: during a key the
  game keeps addons out of the group chat, and the lines never arrived.
  Everyone with the addon reads it - the key's holder with the key linked.
- The **end reaches the group** again: the holder's "Well done!" or "Maybe next
  time!" waits until the game lets addons speak after the chest, a minute at
  most.
- At the chest **no boss is announced twice**: the game stops the run's clock
  just before it says the run is done, and the timer took that for a new run -
  every boss came again with the final time, and the card showed that time for
  each.
- The addon **stands on its own**: its window, the Mythic+ timer and the
  minimap button need no desktop app. The app's Windows installer still brings
  it along, and the addon also comes by itself.
- The addon has a **window** of its own now, as the design draws it: the tabs
  Twinks, Gruppe und Gilde over one table - character, dungeon, key, this
  week's runs, the Great Vault and the M+ rating, the biggest key first. Over
  it stand the week's affixes and how long it is until the reset. A left-click
  on the minimap button opens it (the options moved to shift-click), and so
  does `/rk window`. It reads what is stored, so every alt is in it without
  logging in.
- The **column heads sort** the table - a second click turns the order around
  - and the Gilde tab, sorted by dungeon, **groups the keys under it**, with
  how many stand there. A click on a tab puts its order back.
- The Twinks tab shows **this week's runs** as a small square each, filled
  where the key was timed, and beside the Great Vault slots the **item level**
  its best unlocked slot would give. Every tab says **how long ago** a row was
  last heard of.
- **Your own character stands first** and wears the violet edge, in Twinks and
  in Gruppe.
- A **right-click on a row** puts its key into the chat: your own as the link
  out of your bags, anyone else's as name, dungeon and level.
- The window's **font is yours to pick** (Options -> AddOns -> Raidex Keys):
  Marcellus, the face it was designed in and which now ships with the addon,
  or the game's Friz Quadrata or Arial Narrow. Its **size** is yours as well:
  a font size in the options that takes rows, columns and the window along,
  and a **grip** in the lower right corner that drags the window bigger or
  smaller - never below what its columns need.
- While the options stand open, the timer's preview can show **any dungeon
  of the season at any level**, or a line of letters with umlauts, to see what
  the chosen font makes of them.
- **The run speaks in the chat.** When the timer starts, the key is linked in the group chat -
  "Raidex Keys loaded for [Keystone: The Blinding Vale (9)]", by whoever
  holds it, so it comes once however many run the addon - and the group reads
  "Timer started, 30 minutes left – Have a great run!";
  after every boss you alone read when it fell and where your M+ rating would
  land, with what that adds. At the end the key's holder tells the group
  "Well done! New keystone: [Keystone]" when the run was in time, or "Maybe
  next time!" when it was not. Both can be switched off in the options. While
  the game keeps addons out of the chat, as a running key can, the lines are
  shown to you instead of being refused.
- The window and the timer **move on a grid**: dragged, they go in steps of 10
  points and line up with each other. The step is set in the options - 20, 30,
  40, or off.
- The **options are grouped**: under AddOns -> Raidex Keys the window and the
  Mythic+ timer have pages of their own, and the timer's page is divided into
  what it does during a key, what it says in the chat, the preview and its
  look.
- The tabs read **Gilde, Twinks, Gruppe**. The affixes over them are set
  small and stop before the countdown; the name and the dungeon have more room,
  and the window is a little wider for it. **Einstellungen** in the header
  can be clicked now - the frame that drags the window lay over it.
- The column heads **all show**: in game three of the four stood empty. The
  name and the dungeon share the room that is left - a name up to its realm,
  the dungeon the rest, and where no dungeon is on screen the name has it all
  and the numbers stand together at the right. A **scrollbar** stands beside a
  list longer than the window.
- The panel's **Defaults** puts back what is not a setting as well: where the
  timer, the window and the minimap button stand, and the timer's two colours.
  `/rk reset` does the same.
- The **Gilde tab counts you in**: your own key stands beside the guild's,
  which means the tab holds real keys from the first day in a guild rather
  than sample values. Guild names wear their class colour now - a keystone
  message carries no class, so it comes from the guild roster - and opening
  the window asks the guild again, at most once a minute.
- The **footer says what is stored**, never what is on screen: it counts the
  characters, the group with the keys known in it, and the guild with its keys
  of the week. Where nothing is stored it says so - "Noch keine Charaktere
  gespeichert", "nicht in einer Gruppe", "keine Gilde".
- Two alts of the same name on different realms are told apart by the realm
  in the Twinks tab.
- `/rk preview` shows **every surface with sample values at once** - window,
  minimap tooltip and Mythic+ timer - so the look can be judged and each piece
  dragged into place without a keystone, a group or a guild. A tab with
  nothing to show says **why it is empty** and where to go about it, rather
  than standing on made-up rows.
- The timer's look is **yours to set** (Options -> AddOns -> Raidex Keys):
  how much of the dungeon shows through it, the font out of the five the game
  ships, the size of the text, and the background and text colour. The grey
  tones follow the colours you pick, so one choice never leaves the rest
  behind, while the colours that mean something - the keystone level, the
  clock, the time deaths cost - stay where they are. "Zurück zum Entwurf",
  or `/rk reset`, puts the designed look back if a colour turns out
  unreadable.
- The ticks beside the bosses are **drawn** now, not written: Friz Quadrata
  has no glyph for them, so in game they were empty boxes.
- The Mythic+ timer wears the **design's card** now: a dark panel with a
  violet edge, rules between its parts, the clock as a bar with the +2 and +3
  limits marked on it, and the enemy forces with a bar of their own. Head and
  clock are set in the game's display face, Morpheus.
- A **Mythic+ timer**. While a keystone runs, it shows the level
  and the dungeon, the affixes, the time left with the limits for +2 and +3,
  every boss with the time it fell and your best time for it, the enemy forces
  to two decimals and the deaths with the time they cost. One line keeps
  telling you what the run would be worth if it ended this very moment - its
  dungeon rating, and how much it adds to your M+ rating over your best run
  there. Deaths so far are in that figure, since they are in the clock;
  whatever is still to come is not. Your best time per
  boss is kept per dungeon and key level, and a `/reload` in the middle of a
  run keeps the times already taken. The game's own timer is faded out while
  it shows. Both can be switched off in the options, and `/rk timer` shows a
  made-up run to drag the display where you want it.
- **The card stays up after the run.** The game drops the keystone the moment
  the chest appears, and the timer went with it - before you could read what
  the run took, what it was worth and what the deaths cost. It now stands as
  it stood until you leave the dungeon or start the next key.
- **"Questverfolgung ausblenden"** (Options -> AddOns -> Raidex Keys): with it
  ticked, the tracked quests, campaigns and achievements go while a keystone
  runs and come back when it ends - the "All Objectives" bar with them, which
  is what is left standing over the card when nothing is tracked. The screen
  during a key is the key. It ships unticked, and leaves the game's own timer
  to the box above it.

## 0.15.0 – 2026-09-12

**Desktop app**

- A pull on the route's map is one shape again: the coloured area that holds
  its pack has an edge of its own now and a little more room around the
  circles. A third of a colour with no outline disappeared over a real dungeon
  map, and the enemies looked like loose circles.
- The Routen page says which table is which. The routes read from your own
  Mythic Dungeon Tools now carry that heading and a line saying they belong to
  MDT, so it is clear which rows the app may change and which it only reads.
- A route stored in Raidex hands its **MDT string back out** with one click -
  your own and the ones your guild shared. The app writes into no addon, so
  that copy is how a shared route reaches the game: paste it into MDT's import.
  Until now a guild route could be looked at and nothing more.
- A route that only exists in your own MDT hands out a string too, one the app
  **writes itself**. MDT only makes a string when you press export in game;
  now every route in the list can be passed on or pasted back in above to keep
  it in Raidex, without going into the game for it first.
- An open row folds: the map away with the chevron beside "Eigenes Fenster",
  the route as text with the one before its heading. The line with the buttons
  stays where it was, so the way back is where the map went.
- The map can draw **the way round**: 1 → 2 → 3 as curved arrows from pull to
  pull, switched on with the new button over the map or in the map window.
  Each arrow bends around whatever pulls lie between it and the next pack -
  not always possible on a crowded floor, but it tries. The switch is
  remembered.

**Addon**

- Unchanged at 0.1.12. It ships with this release as always, but there is
  nothing in it to reinstall for.

**Landing page**

- Every download says its build number and when it was built, beside size and
  version; the SHA-256 moved to a line of its own under the file name, where
  it does not crowd them.

## 0.14.0 – 2026-09-12

**Desktop app**

- The app says when a newer version of it or of the addon is out - once at
  start, as a line on the new **Über** page and a strip over the pages with a
  "Später" beside it. It downloads nothing, installs nothing and insists on
  nothing; an old version keeps working. The versions come from the landing
  page's own `latest.json`, so the check needs no Raidex account and sends
  nothing about you.
- **Über** is a page of its own at the foot of the sidebar, where it was a
  dialog behind a button in Einstellungen.

**Addon**

- Unchanged at 0.1.12. It ships with this release as always, but there is
  nothing in it to reinstall for.

**Landing page**

- The gallery is a carousel: one view at a time and large enough to read,
  stepped with the arrows, the dots, the arrow keys or a swipe. A click still
  opens a shot unscaled. Without JavaScript the six shots stack, each its own
  link to its picture.

## 0.13.0 – 2026-09-12

**Desktop app**

- **Gruppenplaner**, a page of its own between Gilde and Einstellungen: pick
  who plays tonight, and every keystone among them is weighed by what its run
  would pay the five who come. The group that wins is the one whose worst-off
  member gains most - at a tie the larger total - so no group where four gain
  and one gets nothing. Beside the best group stand the other keys among those
  picked. Needs a Raidex account: the others' best runs come from there, while
  your own characters are weighed by the runs the addon saved.
- The planner has two switches. **Spezialisierung beachten** builds a group of
  one tank, one healer and three damage dealers, by the specialisation Raidex's
  roster knows. **Gegenstandsstufe beachten** marks whoever is light for the
  key with their item level - it leaves nobody at home. What counts as light is
  what the game rewards for that key level, with an allowance of 0, 3, 5 or 10
  in the settings.
- Every seat says what the run pays that member right on the timer, and its
  tooltip what a good time would add on top. Where the worst-off member gains
  nothing at the timer, the line below the group says who, why, and what a good
  time would still be worth to them.
- The candidates and the guild board carry Blizzard's own icon for a
  specialisation beside the name, and the specialisation under it. Until Raidex
  has scanned a guild for specialisation ids, a drawn role glyph stands in -
  shield, cross or sword.
- When no group comes out, the planner says what stood in the way instead of
  guessing: too few people, or the specialisations among them.
- `--diagnose` says what Raidex answers about each of your own characters -
  season, rating, role, item level, and its best score per dungeon beside the
  addon's - and how many reward levels the addon's file carries. Enough to
  trace a figure that looks wrong to where it came from.
- The landing page gained a page on **what data goes where**
  ([raidex.app/keys/daten/](https://raidex.app/keys/daten/)): what the app
  writes to your machine, what it reads there, what leaves it, and what reaches
  Raidex with an account.

**Addon 0.1.12**

- Stores what each keystone level rewards this season, which is what the group
  planner's item level mark goes by. Midnight's client answers only the Great
  Vault's level, and only once it feels like it - the addon asks again until it
  does, and drops the table when a new season starts.
- `/rk` says how many reward levels are known, `/rk debug` what the game
  answers right now and what was stored from it.

**Raidex**

- The Keys module gained `GET /mplus/character`: a guild mate's best run per
  dungeon this season, with their role and item level, for the group planner.
  The roster keeps the specialisation id beside its name, so a tank can be told
  from a healer without guessing at words. Needs `make migrate` after the
  deploy, and a guild scan to fill in.

## 0.12.0 – 2026-09-12

Initial release: the Raidex Keys desktop app for Windows and Linux, and its
WoW addon.

**Desktop app (Windows, Linux)**

- Shows the Mythic+ week of all your characters: keystone, rating, best runs
  per dungeon and Great Vault progress, as of your last logout.
- Every row with a dungeon carries its picture. Its details slide out: the
  timer with the +2 and +3 limits, your best run against it, and links.
- Characters open their Raider.IO page, keystone levels the dungeon on
  Wowhead, and a season dungeon its Raider.IO leaderboard.
- A rating calculator plans the runs to a rating goal and shows what every key
  would add.
- The guild board lists every guild keystone the addon has seen.
- Routes come from your own Mythic Dungeon Tools. They show the forces per
  pull and are drawn on the dungeon's map. A pasted MDT string shows its route
  before you import it in game.
- A route's map opens in a window of its own, moved and sized freely, with
  the route as text beside it on demand.
- Connect Raidex Keys with your Raidex account by a short code: the app
  shows it, you confirm it on the Raidex website.
- With a Raidex account, pasted routes are stored in Raidex and shared with
  your guild.
- With a Raidex account, your keys reach your guild, and the guild board shows
  the keys your guild mates reported - with spec and item level - besides
  those the addon heard in game.
- The windows come back where and as large as you left them.
- Every table sorts by a click on a column header; another click reverses the
  order. Each table keeps its sort for the next start.
- The font for figures is yours to choose: IBM Plex Mono, JetBrains Mono,
  Source Code Pro, Fira Mono or Cascadia Mono.
- It finds your AddOns folder on its own: in the Windows install path and in
  Lutris, Wine, Steam/Proton and Bottles prefixes. Every save is picked up live.
- The interface is in German and English and follows the system language.
- Something wrong? Start it once with `--diagnose`: it writes what it finds
  and reads to `diagnose.txt`, for your bug report.

**Installation**

- Windows 10 and 11: an installer sets the app up for your user, without
  admin rights, and puts the WoW addon in too - it looks for your AddOns
  folder and you confirm it. Or take the portable `.zip`.
- Linux: a `.deb` package for Debian, Ubuntu, Linux Mint and their
  relatives; for every other distribution the `.tar.gz`, whose `install.sh`
  installs to `~/.local`.
- The files are not signed yet, so Windows may warn at the first start:
  "More info", then "Run anyway". `SHA256SUMS` has every file's checksum.

**Addon**

- Records keystone, rating, best runs and vault of every character.
- Collects guild keys over the LibKeystone protocol, as BigWigs does, and
  shares yours with your guild - switched off in its options, if you like.
- A minimap button: its tooltip shows your key, how many of your group's
  keys are known and how many your guild has this week; a right-click links
  your key in chat. `/rk minimap` hides or shows it.
- `/rk` tells what it has stored, `/rk options` opens its options.
