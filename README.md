# DistanceRPG

A turn-based party RPG where movement is the resource: every step, attack, and
saved manoeuvre spends the same per-turn distance budget. Ported from the
original [Phaser prototype](https://github.com/Sheckerol/Game) onto the
custom [GameEngine](https://github.com/Sheckerol/GameEngine), reinterpreted as
a 3D dungeon with an angled top-down camera.

Each run generates a fresh dungeon (the seed is logged as the run is entered and
also determines the enemy layout, so a run can be reproduced — see the dev
overrides under [Saves](#saves)). Every room but the party's starting one holds
0–4 training dummies; a defeated dummy gets back up some turns later, stronger,
and sooner each time it falls, until the dungeon's boss is killed. Dummies stay
passive until spotted and doze back off two turns after losing sight of the
party.

## Layout requirement

The engine is consumed as a **sibling checkout** — clone both repos next to
each other:

```
<parent>/
  GameEngine/    ← https://github.com/Sheckerol/GameEngine
  DistanceRPG/   ← this repo
```

## Build & run

```bash
dotnet build
dotnet run --project GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj
dotnet test GameEngine.DistanceRPG.Tests/GameEngine.DistanceRPG.Tests.csproj
```

## Controls

The game opens on the screen between runs: the dungeon list, with the last
run's summary above it once a run has ended. Until the tutorial's golem is
beaten it lists the tutorial alone. A saved campaign resumes where it was left:
there, with `CAMPAIGN RESUMED` under the title, or back in the dungeon mid-run,
with `RESUMED` over the floor banner (see [Saves](#saves)).

| Between runs | Action |
| --- | --- |
| W/S / arrows | Choose a dungeon (a locked one shows its countdown and is skipped) |
| Enter | Descend into the chosen dungeon |
| N, then N | New campaign: the starting party (`Logic/Content/StartingParty.cs`), nothing played. It asks first, and replaces the saved campaign; any other key after the first N keeps it |
| Esc | Save the campaign and quit (with NEW CAMPAIGN asked, Esc only answers no) |

| In a dungeon | Action |
| --- | --- |
| WASD / arrows | Move the active character |
| 1–4 / Tab / click | Switch party member (click their box, the party list, or the member — unless a support staff in hand casts on them instead) |
| Left-click enemy | Attack with the equipped weapon — or cast with it: a debuff staff lands its effect on the enemy, a wand aims its shape at it |
| Left-click ally / floor | A support staff (Renewal, Warding) casts on the ally clicked; a Blast, Cone or Beam wand casts at the floor point clicked |
| N | A Nova wand casts around the active character |
| O | Hold fire: a ranged weapon with Overwatch spends an attack's movement to hold its shots through the enemies' turn, loosed at whatever walks into reach |
| Space / Enter | End turn (banks half your unspent movement) |
| E | Take the stairs the active character stands on: down, up, or out by the first floor's way out. Every living member must be within 3 tiles; the way out asks first, and E or Enter says yes |
| G | Pick up what the active character stands on: a drop, or a weapon the party left there |
| I / B | Inventory: six slots a member, slot 1 in hand (2–6 swap that bag slot into the hand) |
| X (inventory open) | Leave a weapon: then 2–6 puts that bag slot's weapon on the floor here, free. X again cancels; G takes it back |
| Scroll wheel | Zoom |
| Esc | Close the open menu (at the way out's prompt, that is no), otherwise open/close the pause menu, whose 2 saves the run and quits (`2  SAVE AND QUIT`; the run also saves itself at every turn, which the menu says). Does nothing on a run's summary |
| Enter (run's summary) | Back to the screen between runs |

Outside combat the rest of the party marches behind the active character in a
single-file line, the group paces itself to the member with the least movement
left, and the turn cycles automatically once everyone's movement runs out.
Marching is granted only at the start of a turn, and only if no living enemy
is visible and everyone has regrouped within their max movement of the leader
— so a party split by a fight doesn't charge back into formation the moment
line of sight breaks. Sighting the enemy revokes it for the rest of the turn:
everyone stops following, characters body-block each other, and turns end
manually.

## Saves

There is one campaign and one save, and no manual saving: the game saves on
its own and resumes where you left off.

- **Where**: `%LOCALAPPDATA%\DistanceRPG\saves\campaign.json` on Windows
  (`~/.local/share/DistanceRPG/saves/` on Linux) — the player's data folder,
  never beside the exe, which a rebuild or `dotnet clean` replaces. Set
  `DISTANCERPG_SAVE_DIR` to a folder to keep the saves there instead. The log
  names the full path of every save written or read.
- **When**: all the time, on its own. Mid-run the game saves when a run is
  entered, at the start of every one of your turns, and every time the party
  arrives on a floor; it saves at a run's end (the summary with it), when NEW
  CAMPAIGN is confirmed or `--new-campaign` begins one, and on every quit — the
  pause menu's `2  SAVE AND QUIT`, Esc on the screen between runs, or the
  window's close button. A save is never taken while the enemies are moving: a
  quit during their turn (the pause menu can be opened then, and the close
  button pressed any time) plays their turn out to your next one first — at the
  game's fixed logic step, so exactly as it would have played — and saves that.
  If the enemies wipe the party on the way, the run ends and the campaign is
  saved between runs with its summary.
- **Resuming**: every launch resumes the campaign where it was. Between runs it
  is the screen between runs, with `CAMPAIGN RESUMED` under the title and the
  last run's summary above the list. Mid-run it is the dungeon: the same floor,
  turn and positions, HP, mana, statuses and movement left, the corpses lying on
  the very weapons they lay on, the weapons left on the floor, the explored fog
  and what the party could already see this turn, the same member active and
  the party marching or not as it was — with `RESUMED` over the floor banner.
  The dice fall as they would have without the quit: the d20 is the run's own,
  seeded by the run and saved, so reloading never rerolls a turn — on purpose,
  to protect players from themselves: a bad roll is never reloaded away. A campaign
  saved under other numbers (`tuning.json` edited since) plays on under the new
  ones and says `THE NUMBERS HAVE CHANGED SINCE THIS CAMPAIGN WAS SAVED - IT PLAYS
  ON UNDER THE NEW ONES`, once: the next save records the new numbers. (A
  revival due under the new numbers happens as the floor opens.)
- **`campaign.bak.json`** is the save before the last one. Every write goes to a
  temp file first and then replaces the save in one step, the old save becoming
  the backup, so a write that fails — a full disk, a locked file, a folder with
  no rights — costs nothing: the save on the disk is the last good one, the log
  says why, and the game says `THE CAMPAIGN COULD NOT BE SAVED` at once, over the
  floor you are playing, and again on the screen between runs — once, however
  many saves failed, and not at all if a later save has gone through by then. A
  quit that cannot save leaves `campaign.unsaved` beside the save, and the next
  launch says `THE CAMPAIGN COULD NOT BE SAVED AS THE GAME LAST QUIT` as it
  resumes, between runs or over the floor: what resumes is the last save from
  before that quit.
- **A save that cannot be played is set aside, never deleted**: one that is not
  a save, one a newer build wrote, one naming content that is gone, one that
  contradicts itself or holds a number past what play can reach (below), or one
  that crashed the game as it resumed. A `campaign.resuming` marker sits beside the
  save while a resume is in flight — until the resumed screen is up, the screen
  between runs or the resumed floor — and a launch that finds it knows the last
  resume never finished. It is renamed `campaign.unreadable-N.json` (or
  `campaign.crashed-N.json`), the backup is tried — one save older, so at most a
  turn or a floor back mid-run — and failing that a new campaign begins. The
  screen between runs says so under the title, whole: what became of the
  campaign, `THE NUMBERS HAVE CHANGED ...` when they have, and each file set
  aside by its full path (drawn with forward slashes, since the game's font has
  no backslash; Windows reads it either way). When the backup holds a run in
  progress, the floor it resumes on says the same, whole, over the floor
  banner; the banner moves down to make room, and the words stay up longer the
  more there are. Only what the data files raised after that is cut to three
  rows (`+N MORE - SEE THE LOG`). The log has every word. **Rename it back to
  `campaign.json` to try it again.** Nothing ever writes over a file set aside.
- **No field of a save is trusted** (the user's ruling of 2026-09-25: "since we
  are storing in raw json we should probably not trust the fields"). Each is
  checked on its own — an id the content has, a count no smaller than play
  writes it and, where a larger one would break the game's arithmetic, no larger
  than play can reach: a forged stack past 3, a live one past its forged count
  plus 50, an enchantment's tier past 2147, more than 10 on a weapon, a run
  counter in the billions, hit points past the largest bar plus what was earned
  — and then against the rest: a retired dungeon never cleared, a last run's
  summary that disagrees with the campaign (its run count, its lock, the dungeon
  it cleared), a run in progress in a dungeon the list could not have offered,
  a boss standing that the run has down. A save that fails any of it is set
  aside as one that cannot be read, the notice naming the field and what is
  wrong there: edit `"retired": ["tutorial"]` while `"cleared": []` and the
  launch says `CAMPAIGN.JSON COULD NOT BE READ (CAMPAIGN.RETIRED[0]: TUTORIAL IS
  RETIRED AND WAS NEVER CLEARED)`, sets it aside and resumes the backup; give a
  carried weapon's entry `"tier": 99999` and it names the tier and `IS ABOVE
  2147`. A lock that has already lifted is no contradiction: it stays in the
  save, and the list reads its dungeon as open.
- **Starting over**: N, then N, on the screen between runs replaces the saved
  campaign with a new one (the old one stays in `campaign.bak.json` until the
  next save). `DistanceRPG --new-campaign` — `dotnet run --project
  GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj -- --new-campaign` — sets
  the save and its backup aside as `campaign.abandoned-N.json` (the backup first,
  so the higher number is the later save) and starts fresh, saving the new
  campaign at once: even a crash before its next save resumes the new campaign,
  never the old one. If another program holds either file open
  so it cannot be moved, it stops there, nothing is saved that session, and the
  next launch resumes the old campaign. Deleting `campaign.json` by hand resumes
  the backup instead, if there is one.
- **The dev overrides play apart**: a launch with `DISTANCERPG_DUNGEON` (a
  dungeon's id: straight into it) or `DISTANCERPG_SEED` (the first run's map
  seed) plays a fresh campaign in `saves\override\`, emptied at every such
  launch, and never reads or writes `campaign.json` — every override launch is as
  repeatable as ever, and your campaign is untouched. Unset them to go back to it.

## Tuning the numbers: `tuning.json`

Every number the game is tuned by — the movement budget, the mana divisor, each
status's effect per level, what a stack of each modifier is worth, the farm's
odds, the boss depths, the carry — sits in one file, `tuning.json`, grouped
under the headings of `docs/roadmap/phase-5-save.md` §5.3 (and three of its own:
Progression, Combat and Drops) with a comment on every key. A few gameplay
numbers were never keys and are still compiled — an enemy's 50 hit points, 100
mana and 100 movement, the crowded-room healer's odds, the chart's ×1.5, the
proficiency steps, the room cap; whether they belong in `tuning.json` is open
(`docs/roadmap/open-questions.md`).

- **Where**: beside the executable — `GameEngine.DistanceRPG/bin/Debug/net10.0/tuning.json`
  after a Debug build. The log prints its full path at startup
  (`[Data] Read D:\...\net10.0\tuning.json`); the logs are in
  `%LOCALAPPDATA%\Sheckerol\GameEngine\logs`.
- **`GameEngine.DistanceRPG/Data/tuning.json` is the master copy**, and the only
  home of these numbers: the game keeps no copy of one in its code. The game is
  built with it inside, and plays that built-in copy wherever the copy beside
  the exe leaves a key out — or all of it, when that copy is missing or cannot
  be read. A build copies the master beside the exe, where a playtest edits it:
  **edit the copy beside the exe to playtest, and the master to change what
  ships.** A build replaces the copy beside the exe whenever the `Data/` master
  is newer — any change to the master, a checkout or a pull — so keep an edit
  worth keeping in `Data/`.
- **Restart to apply**: the file is read once, when the game starts, and never
  during play. If the file is there but the game cannot read it, the built-in
  numbers play and the screen says why. This happens when an editor holds the
  file open and locked, or the folder does not let the game read it. The screen
  shows `TUNING.JSON COULD NOT BE READ (ANOTHER PROGRAM HAS IT OPEN) - THE
  BUILT-IN NUMBERS ARE PLAYING` and does not call the file missing. The log has
  the whole error. A file with nothing in it — as an editor that crashed while
  saving can leave one — plays the built-in numbers too, and says
  `TUNING.JSON: THE FILE IS EMPTY - THE BUILT-IN NUMBERS ARE PLAYING`.
- **Name only what you change**: a key left out keeps its built-in number, so
  `{ "MovementBudget": 240 }` is a whole file. Comments and trailing commas are fine.
- **A mistake costs a warning, never a crash**: an unknown key, a value out of
  range, or one that is not a whole number is shown under the BETWEEN RUNS title
  (or over the first floor when `DISTANCERPG_DUNGEON` boots straight in, or over
  the floor a saved run resumes on) and
  written to the log, and the game plays on — `"MovementUnitsPerMana": 0` plays 1
  and says `TUNING.JSON: MOVEMENTUNITSPERMANA 0 IS BELOW 1 - USING 1`. A few keys
  are also fitted to the content: `"PartyCarrySlots": 4` plays 8, because every
  starting member carries two weapons. The log lists every number the file
  changed (`[Data] ...\tuning.json changes 2 numbers: MovementBudget 240, ...`).
- **Derived numbers do not follow**: `MovementUnitsPerMana` is derived from the
  movement budget and the cast costs, and turning `MovementBudget` leaves it
  where it was — so a longer budget also banks more mana a turn. Its comment
  gives the formula to re-derive it by hand.
- **The Enchanter's keys play nothing yet**: `WearCapacityPerBar`,
  `FirstSlotCapacity`, `SlotCapacityStep`, `EnchantmentSlots`,
  `FirstEnchantmentWear` and `EnchantmentWearStep` are what a deposit at the
  enchanter costs, as the user ruled on 2026-09-25 — how a weapon's wear
  capacity grows, where its enchantment slots open, and the wear attaching each
  enchantment needs (`docs/roadmap/phase-6-between-runs.md` §6.1, §6.2). The
  enchanter arrives in Phase 6a, and until then turning one changes nothing in
  play. Each is still checked like every key — `"EnchantmentSlots": 0` is
  taken as 3 and says `TUNING.JSON: ENCHANTMENTSLOTS 0 IS BELOW 3 - USING 3` —
  and the log lists it among the numbers the file changed; but a save's record
  of the numbers it was played under leaves them out, so turning one never says
  `THE NUMBERS HAVE CHANGED` when a campaign resumes. The docs' older
  `WearCapacityLadder` and `EnchantmentWearCost` are no keys: a file naming
  either is told which key took its place.
- **To reset** the copy, delete it (the built-in numbers play, and the screen
  says the file is missing), or write the built-in copies back:
  `dotnet run --project GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj -- --write-data <exe folder>`
  writes the four built-in copies into the folder, byte for byte, and exits
  without opening a window. A number worth keeping goes into
  `GameEngine.DistanceRPG/Data/tuning.json`, the master, and the game is rebuilt;
  `--write-data` refuses that source `Data` folder, so it can never write the
  last build's copies over an edit there.
- **The master names every number.** A key or a table member left out of the
  master stops the game on `THE GAME CANNOT START`, naming
  `TUNING.JSON (BUILT-IN)` and the key, and ending
  `THE BUILT-IN TUNING.JSON IS BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/TUNING.JSON AND REBUILD - ESC QUITS`:
  nothing stands under it. (A copy beside the exe may leave out what it likes.)
- Nothing the golden tests pin — the map generator, the random streams, the
  pathfinder — is in it, and a test keeps it that way.
- **An edit to the master fails a test only where a design doc fixes the
  value**: `MovementBudget` 160, and `MovementUnitsPerMana` 10 derived from it
  (`docs/roadmap/phase-1-weapons.md`'s `(160 − 40) / (8 + 3)`), fail
  `MovementBudgetTests.TheBudgetIsTheTuningsKey` by name, and the doc and the
  derived key move with them; and `FriendlyFireEnabled` turned on fails
  `WandTests.FriendlyFire_OffByDefault_HalfWhenOn`, since the same doc ships
  wands with friendly fire off until the wand placement scorer exists. Every
  other number in it fails nothing: the tests read it from the master, or stand
  on numbers of their own (`Premise`, in the test project: the numbers the
  worked examples were written against, which most scenario suites play instead
  of the master's) — give `PerStack`'s `Block` 4, rebuild and run the suite, and
  it passes, the Bulwark absorbing 12 and its test saying so.

## The content files

Three content files sit beside `tuning.json` (`docs/roadmap/phase-5-save.md`
§5.4–§5.9): `restricted.json`, `weapons.json` and `dungeons.json`. They are read
once, when the game starts, in that order, after the game's own catalogues, and
everything in them is named by a stable string id.

- **`restricted.json`** holds the relations: what may sit together on a weapon
  and what may be rolled, for modifiers and enchantments alike. `excludes` is a
  list of groups (every id in a group excludes every other, both ways), `requires`
  maps an id to the ids it cannot exist without, `kind` limits a modifier to
  `melee`, `ranged` or `caster` (lower case), `forgedOnly` lists the modifiers
  that can be deepened but never added, and `neverRolled` lists the enchantments
  no roll grants and no service copies (the unique souls, as shipped). `kind` and
  `forgedOnly` are for modifiers only, and `neverRolled` for enchantments only:
  the wrong sort of id in one stops the game. `neverRolled` is this file's alone —
  the catalogue's unique flag only pins a soul's tier — so put `vampiric` in it
  and it stops dropping; take a soul out and it may be copied (no drop carries a
  soul either way).
  A relation is an edit here, never a rebuild: take the crit riders' group
  (`["CritWeaken", "CritSunder"]`) out and restart, and a dagger may hold both.
- **The enchantment catalogue and the unique souls are code**
  (`Logic/Enchantments/EnchantmentDefinitions.cs`); so is the starting party
  (`Logic/Content/StartingParty.cs`) — a change to either is a rebuild, and a new
  campaign starts with the party the code names (a saved campaign keeps the party
  it started with). Both are still checked when the game starts, against the
  files beside it. The catalogue's dialled numbers — the `applyPercent` of
  `flaming`, `cold`, `shocking`, `acidic` and `serrated`, and `arcane`'s
  `potency` — are not in the catalogue at all: they are `tuning.json`'s
  `ApplyPercent` and `ArcanePotency`, so turn them there, and the master
  `tuning.json` must name each. `enchantments.json` and `party.json` are no
  longer read: a build removes the old copies beside the exe (an edit in one is
  lost with it), and a copy found there anyway is named on screen as no longer read
  (`PARTY.JSON IS NO LONGER READ - THE STARTING PARTY IS IN THE CODE
  (LOGIC/CONTENT/STARTINGPARTY.CS)`), its path in the log.
- **`weapons.json`** holds every weapon class and every weapon, in three
  sections:
  - `classes` gives each of the eight classes (`Dagger`, `Sword`, `Spear`, `Axe`,
    `Ranged`, `Throwing`, `Staff`, `Wand`) one row: its `kind` (`melee`, `ranged`
    or `caster` — the Staff and the Wand cast, and no other class may), the
    `stats` it levels on (`["STR", "DEX"]` for the Sword, the better of the two
    counting; never `CON`), its `signature` (the modifier it is named for, which
    its Purity variant deepens and a dungeon's theme is drawn from; `null` for a
    caster), its `baseline` (`{ "Block": 1, "Push": 1 }`: what every weapon of the
    class is forged with — a martial class's signature and one more, a caster's
    one modifier or more, `{ "Resonant": 1 }` as shipped, and every staff and
    wand carries it alone) and the statline its weapons share (`range`, `damage`,
    `cost`, `manaCost`). Re-basing a class is one edit to its row: give the Dagger
    row `"damage": 17` and restart, and every dagger — the uniques made from them
    too — hits for 17. A class row left out, one the game cannot level or cast
    with (a `Staff` that is `melee`, `stats` naming `CON`, a signature another class
    has), or one whose `baseline` the forge refuses (a stack of `0`, one past its
    forge limit, a modifier `restricted.json` keeps off the class's kind) stops the
    game naming `WEAPONS.JSON` and the class, whose row the baseline is written in.
    The Staff and Wand rows also name their `dropOrder`: the variants a drop of
    the class rolls between, in the order the draw counts them. A staff or wand
    it leaves out never drops — add one to `variants`, then to the `dropOrder`,
    and it drops with no rebuild. Reordering `variants` moves no drop; an edit to
    a `dropOrder` moves what a dummy not yet collected in a saved campaign drops.
    An id there that is no variant of the class, or one named twice, stops the
    game.
  - `variants` lists the thirty-two weapons a drop or a dummy carries, each as its
    `class`, its `role` and the modifiers it `adds` to its class's baseline, with
    the enchantments it arrives with (`{ "id": "vampiric", "tier": 3 }`, the tier
    left out when it is 1) and a wand's `shape`. A variant may name its own
    `range`, `damage`, `cost` or `manaCost`, which then plays for it alone — the
    Staff of Renewal's and the Staff of Mire's mana do. Give the Weakspot Stiletto
    `"damage": 25` and restart, and A hits harder while the other daggers do not.
  - `uniques` lists the artifacts, each as the variant it is `derivedFrom` —
    whose class, statline and shape it takes — the modifiers it `raises` (set to
    the count given, not added to) and its enchantments.

  `arming_sword` (what a dummy holds) and `staff_of_renewal` (a crowded room's
  healer) are named by the code, so they must stay, and so must a variant of each
  class a dummy can be placed holding (Dagger, Sword, Spear, Axe, Throwing).
- **An old-format `weapons.json` beside the exe stops the game.** Before the
  classes section the file was one `weapons` list, every weapon naming all its
  fields. A copy in that format beside the exe — one edited there after its
  master, or one an earlier `--write-data <exe folder>` wrote, newer than the
  master so a build keeps it — opens the game on `THE GAME CANNOT START`, naming
  `WEAPONS.JSON` and `CLASSES` and saying the file predates the classes section:
  delete it and the built-in weapons play (the screen says the file is missing),
  or run `--write-data <exe folder>` to write the current copy there. A copy
  from before the Staff and Wand rows named their `dropOrder` stops the game the
  same way, naming `STAFF` and saying the same two ways out.
- **`dungeons.json`** holds the dungeons the screen between runs lists: `theme`,
  `attunement` (a damage type, or `null`), `bossFloorMin`/`bossFloorMax` (`null`
  plays `tuning.json`'s range), the `boss` (its statline and `innate` modifiers)
  and `retiresOnClear`, the tutorial's alone. There is no drop table: what a boss
  drops is computed from its theme and `restricted.json`, so taking `Brace` out
  of `["Brace", "Opportunist", "Overwatch"]` puts axes in the Spear Hall's drops
  with no edit to this file.
- **A row marked `/* provisional */`** after its id is one the docs did not fix
  — the four dungeons beside the tutorial and their bosses, and the three
  unnamed uniques — a first guess, expected to move after play.
- **The files in `GameEngine.DistanceRPG/Data/` are the master copies** — of every
  relation, weapon class, weapon and dungeon — hand-kept, with the reason for each
  group, class and boss written beside it; the game keeps no copy of any in its
  code. As with `tuning.json`, the game is built with each inside and plays that
  built-in copy when the copy beside the exe is missing or cannot be read, and a
  build copies each beside the exe (`bin/Debug/net10.0/`) whenever the master is
  newer: edit the copy beside the exe to playtest (restart to apply), and the
  master to change what ships (rebuild). The file headers say what each key and
  field means.
- **An edit to a master fails a test only where a design doc fixes the
  number**: the eight class rows, every weapon's statline and spread and the
  wands' shapes (`docs/roadmap/phase-1-weapons.md` §1.2–§1.4) fail
  `WeaponContentTests.Statlines_MatchTheTables`, `WandShapes_MatchTheTable` and
  `WeaponClassTests.AClassRowIsWhatTheClassCarries` by name; the unique table
  (§1.5) fails `UniqueContentTests`; and the tutorial's row
  (`docs/roadmap/phase-5-save.md` §5.8) fails
  `DungeonContentTests.TheTutorialIsTheShippedDungeon` — its golem's 80 HP and
  movement budget of 40 that test alone, its boss's floor 2 every test that
  plays the tutorial. Each doc moves with its pin. Every other number — a
  provisional boss's statline, say — fails nothing: the tests read it from the
  master, or stand on numbers of their own (`Premise`). A relation, an id or a
  row is no number, and a test that names one fails where it names it.
- **Content is checked, never clamped.** A number out of range in `tuning.json`
  costs a warning; in a content file it is a broken rule — every number there
  has a range, below — and the game does not start. It opens on
  `THE GAME CANNOT START` instead, naming the file, the entry,
  the rule and what broke it, with what `tuning.json` changed and
  `FIX THE FILE AND START AGAIN - ESC QUITS`; the log has the same lines. A typo
  such as `"Riposte": ["Blok"]` shows `RESTRICTED.JSON`, `BLOK`, the rule
  `ID DOES NOT RESOLVE TO A MODIFIER OR AN ENCHANTMENT` and the detail
  `NOT A MODIFIER, NOR AN ENCHANTMENT IN THE ENCHANTMENT CATALOGUE - DID YOU MEAN BLOCK?`.
  A field the game does not know, a value of the wrong kind, or a file that is
  not JSON is refused the same way, naming the path in the file
  (`dungeons[1].boss.maxHp`) — or, for a file that is not JSON, the line, the
  column and the word the reader stopped at, on one line
  (`"shape": nul` reads `NUL... IS AN INVALID JSON LITERAL. EXPECTED THE LITERAL
  NULL`). Comments and trailing commas are fine.
- **Every number in a content file has a range** (the user's ruling of
  2026-09-25: "since we are storing in raw json we should probably not trust the
  fields"), and one outside it is refused the same way, under the rule
  `A NUMBER IN A CONTENT FILE LIES IN THE RANGE THE GAME PLAYS IT IN`, naming the
  entry, the field, the number and the whole range with why: give the Halberd
  `"cost": 0` and the screen shows `WEAPONS.JSON`, `HALBERD` and
  `COST 0 IS OUTSIDE 1 TO 10000 - MOVEMENT IS CHARGED A UNIT AT LEAST, ...`; pin
  the tutorial's boss to floor 100 and it shows `DUNGEONS.JSON`, `TUTORIAL` and
  `BOSSFLOORMIN 100 IS OUTSIDE 1 TO 99 - ...`, with no `tuning.json` notice above
  it. Where a rule bounded a number before — a stack below 1, a forge past its
  limit, a boss floor below 1, a boss with no hit point — that rule still refuses
  it, its detail in the same form (`BOSS.MAXHP 0 IS OUTSIDE 1 TO 10000 - ...`).
  The tops are the game's own, read off `tuning.json`'s clamps, never numbers of
  the masters, so every shipped number lies inside them:

  | Number | Range | Why |
  | --- | --- | --- |
  | `weapons.json`: a class row's `range`, and a variant's own | 1–10000 | a reach of 0 hits nothing; 10000 is the farthest `tuning.json` measures (`MovementBudget` at its most), past any map |
  | a class row's `damage`, and a variant's own | 0–1000 | a staff hits for 0, its cast being its innate; 1000 is the most a `tuning.json` magnitude can be (`PerStack` at its most) |
  | a class row's `cost`, and a variant's own | 1–10000 | movement is charged a unit at least, so a smaller cost would be one the game never charges, and the HUD's COST would show it |
  | a class row's `manaCost`, and a variant's own | 0–1000 | a martial class casts for 0; below 0 a cast would open on an empty pool |
  | a class row's `baseline` stack, a unique's `raises`, and every weapon's spread as the game composes it | 1 to the modifier's `MaxForged` (3 at most) | the forge's own rules, as before: a stack is one at least, and nothing is forged past its limit |
  | a variant's `adds` stack | 1–3 | an add adds a stack at least, and none is deeper than any forge goes (`MaxForged` at its most). Checked as the file is read, before it is added to the class's baseline, so the refusal names the number written and its path — `"adds": { "Light": 0 }` on the Flensing Knife shows `WEAPONS.JSON: VARIANTS[0].ADDS.LIGHT: 0 IS OUTSIDE 1 TO 3` |
  | an enchantment's `tier` | 1–3 | a weapon arrives at tier 1, and an artifact at 3 at most; tier grows in play, never in the file. A unique enchantment written above 1 plays at 1 |
  | a wand's `radius`, `length`, `width` and `targetReach`, each its shape measures | 1–10000 | a measured dimension of 0 covers nothing, and none is longer than the farthest the tuning measures. One its shape does not measure is 0, or refused, as before |
  | a Cone's `angleDegrees` | 1–360 | a cone of 0 degrees covers nothing, and none is wider than a full turn |
  | `dungeons.json`: `bossFloorMin` and `bossFloorMax`, where a dungeon pins them | 1–99 | a floor number starts at 1 (an inverted range is refused as before); 99 is the deepest floor `tuning.json` can name (`BossFloorMax` at its most), and one the boss offset can always be fitted to |
  | a boss's `maxHp` | 1–10000 | a boss has a hit point at least, and no more than the largest bar a pool opens on (`StartingPool` at its most) |
  | a boss's `movementBudget` | 1–10000 | a boss moves a unit at least, and no farther than the tuning measures |
  | a boss's `innate` stack | 1–53 | an innate stack is one at least — innate is no forge's, so the golem may stand deeper than any weapon — and none is deeper than a stack can ever grow (`MaxForged` 3 plus `AcquiredHeadroom` 50, each at its most) |

  `restricted.json` holds no number, and a `dropOrder` is ids. A copy beside the
  exe with a number out of range never falls back to the built-in copy; a
  built-in copy out of range is the master's own, whatever sits beside it.
- **What is reported first**: the load runs in the order the game's catalogues,
  tuning, relations, weapons, dungeons, the starting party, and the first check
  to fail is the one shown. Each file's shape is checked before anything reads
  it. A later file's mistake never hides an earlier one's: a typo in
  `dungeons.json` waits behind a broken relation. A stage that ran a built-in
  copy is marked `(BUILT-IN)`: either a file beside it broke it — a
  `restricted.json` there refusing a built-in weapon, and the line at the bottom
  still says `FIX THE FILE AND START AGAIN` — or the master itself is broken: its
  own text is not JSON of its shape, a number in it is out of its range, or no
  content file was read from beside the exe to have broken it. Then the screen ends `THE BUILT-IN WEAPONS.JSON IS
  BROKEN - FIX GAMEENGINE.DISTANCERPG/DATA/WEAPONS.JSON AND REBUILD - ESC QUITS`,
  since only fixing the master and rebuilding can mend it. A
  `restricted.json` edit that breaks the enchantment catalogue — an element
  opposed by two, `["flaming", "cold", "shocking"]`, say — stops on
  `RESTRICTED.JSON` itself, the file to fix,
  and one that makes a weapon illegal names both the weapon — or its class, where
  the relation refuses the class's baseline — and the relation that refused it,
  for modifiers and enchantments alike: add `["Push", "Block"]` to `excludes`,
  and since the Sword's baseline forges both, the game opens on `WEAPONS.JSON`,
  `SWORD`, and a detail ending
  `RESTRICTED.JSON EXCLUDES [PUSH, BLOCK]: PUSH IS ALREADY FORGED HERE`. A
  `weapons.json` that renames a weapon only the starting party names — A's
  `weakspot_stiletto`, C's `great_axe` — stops on `WEAPONS.JSON` too, naming the
  member.
- **A missing file plays the built-in one, and says so**: delete
  `weapons.json` and the game plays the built-in weapons, with
  `WEAPONS.JSON IS MISSING - THE BUILT-IN WEAPONS ARE PLAYING` under the title
  and the path it looked in in the log. The same goes for every content file.
- `--write-data <folder>` writes the four built-in copies: `tuning.json` and the
  three content files, byte for byte as the build carries them. Run on the folder
  beside the exe it resets every edited copy there. It refuses the source `Data`
  folder, which holds the masters.

## How the port was verified

`GameEngine.DistanceRPG.Tests/TestData/distancerpg-golden.json` was generated
by running the original game's JavaScript modules under Node. The C# port
reproduces them bit-for-bit: the mulberry32 RNG stream, the full 70×50 dungeon
for the shipped map seed, fog-of-war union boxes, and A* paths.
