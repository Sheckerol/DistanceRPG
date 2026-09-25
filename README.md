# DistanceRPG

A turn-based party RPG where movement is the resource: every step, attack, and
saved manoeuvre spends the same per-turn distance budget. Ported from the
original [Phaser prototype](https://github.com/Sheckerol/Game) onto the
custom [GameEngine](https://github.com/Sheckerol/GameEngine), reinterpreted as
a 3D dungeon with an angled top-down camera.

Each run generates a fresh dungeon (the seed is logged at startup and also
determines the enemy layout, so a run can be reproduced). Every room but the
party's starting one holds 0–4 training dummies; defeated dummies resurrect
after 10 turns. Dummies stay passive until spotted and doze back off two turns
after losing sight of the party.

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
| 1–4 / Tab / click | Switch party member (click their box or the party list) |
| Left-click enemy | Attack with the equipped weapon |
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
  seeded by the run and saved, so reloading never rerolls a turn. A campaign
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
  a save, one a newer build wrote, one naming content that is gone, or one that
  crashed the game as it resumed. A `campaign.resuming` marker sits beside the
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
under the headings of `docs/roadmap/phase-5-save.md` §5.3 with a comment on
every key.

- **Where**: beside the executable — `GameEngine.DistanceRPG/bin/Debug/net10.0/tuning.json`
  after a Debug build. The log prints its full path at startup
  (`[Data] Read D:\...\net10.0\tuning.json`); the logs are in
  `%LOCALAPPDATA%\Sheckerol\GameEngine\logs`.
- **Edit that copy**, not `GameEngine.DistanceRPG/Data/tuning.json`. The one in
  `Data/` is generated from the compiled numbers and pinned byte for byte by
  `ShippedDataTests`, so editing it fails the tests. A build copies it beside
  the exe only when it is newer than the copy there, so an edited copy survives
  a rebuild; `dotnet clean`, or a regenerated `Data/` file, replaces it.
- **Restart to apply**: the file is read once, when the game starts, and never
  during play. If the file is there but the game cannot read it, the built-in
  numbers play and the screen says why. This happens when an editor holds the
  file open and locked, or the folder does not let the game read it. The screen
  shows `TUNING.JSON COULD NOT BE READ (ANOTHER PROGRAM HAS IT OPEN) - THE
  BUILT-IN NUMBERS ARE PLAYING` and does not call the file missing. The log has
  the whole error.
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
- **To reset** the copy, delete it (the built-in numbers play, and the screen
  says the file is missing), or write a fresh one:
  `dotnet run --project GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj -- --write-data <folder>`
  writes the shipped files into `<folder>` and exits without opening a window.
  A number worth keeping goes into `GameEngine.DistanceRPG/Logic/Content/Tuning.cs`,
  and `--write-data GameEngine.DistanceRPG/Data` regenerates the shipped files.
- Nothing the golden tests pin — the map generator, the random streams, the
  pathfinder — is in it, and a test keeps it that way.

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
  that can be deepened but never added, and `neverRolled` lists what no roll
  grants (the unique souls). `kind` and `forgedOnly` are for modifiers only: an
  enchantment in either stops the game.
  A relation is an edit here, never a rebuild: take the crit riders' group
  (`["CritWeaken", "CritSunder"]`) out and restart, and a dagger may hold both.
- **The enchantment catalogue and the unique souls are code**
  (`Logic/Enchantments/EnchantmentDefinitions.cs`); so is the starting party
  (`Logic/Content/StartingParty.cs`) — a change to either is a rebuild, and a new
  campaign starts with the party the code names (a saved campaign keeps the party
  it started with). Both are still checked when the game starts, against the
  files beside it. The catalogue's dialled numbers — the `applyPercent` of
  `flaming`, `cold`, `shocking`, `acidic` and `serrated`, and `arcane`'s
  `potency` — are `tuning.json`'s `ApplyPercent` and `ArcanePotency`: turn them
  there. `enchantments.json` and `party.json` are no longer read: a build
  removes the old copies beside the exe (an edit in one is lost with it), and a
  copy found there anyway is named on screen as no longer read
  (`PARTY.JSON IS NO LONGER READ - THE STARTING PARTY IS IN THE CODE
  (LOGIC/CONTENT/STARTINGPARTY.CS)`), its path in the log.
- **`weapons.json`** holds every weapon: the thirty-two variants (eight classes
  of four) and the uniques, each with its statline (`range`, `damage`, `cost`,
  `manaCost`), its forged spread (`"forged": { "Block": 1, "Push": 1 }`), the
  enchantments it arrives with (`{ "id": "vampiric", "tier": 3 }`, the tier left
  out when it is 1) and a wand's `shape`. A class's baseline is not written
  anywhere: it is what the class's four variants share, so re-basing a class is
  an edit to all four. Give the Weakspot Stiletto `"damage": 25` and restart, and
  A hits harder. `arming_sword` (what a dummy holds) and `staff_of_renewal` (a
  crowded room's healer) are named by the code, so they must stay.
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
- **Edit the copies beside the exe** (`bin/Debug/net10.0/`), as with
  `tuning.json`: the ones in `GameEngine.DistanceRPG/Data/` are generated from
  `Logic/Content/ContentDefaults.cs` and pinned by `ShippedDataTests`. The file
  headers say what each key and field means. Restart to apply.
- **Content is checked, never clamped.** A number out of range in `tuning.json`
  costs a warning; in a content file it is a broken rule, and the game does not
  start. It opens on `THE GAME CANNOT START` instead, naming the file, the entry,
  the rule and what broke it, with what `tuning.json` changed and
  `FIX THE FILE AND START AGAIN - ESC QUITS`; the log has the same lines. A typo
  such as `"Riposte": ["Blok"]` shows `RESTRICTED.JSON`, `BLOK` and `ID DOES NOT
  RESOLVE TO A MODIFIER OR AN ENCHANTMENT - DID YOU MEAN BLOCK?`. A field the game
  does not know, a value of the wrong kind, or a file that is not JSON is refused
  the same way, naming the path in the file (`dungeons[1].boss.maxHp`). Comments
  and trailing commas are fine.
- **What is reported first**: the load runs in the order the game's catalogues,
  tuning, relations, weapons, dungeons, the starting party, and the first check
  to fail is the one shown. Each file's shape is checked before anything reads
  it. A later file's mistake never hides an earlier one's: a typo in
  `dungeons.json` waits behind a broken relation. When a file beside a built-in
  record breaks it, the screen marks the built-in one `(COMPILED)`. A
  `restricted.json` edit that breaks the enchantment catalogue — `neverRolled`
  without `burning`, say — stops on `RESTRICTED.JSON` itself, the file to fix,
  and one that makes a weapon illegal names both the weapon and the relation that
  refused it, for modifiers and enchantments alike: add `["Push", "Block"]` to
  `excludes` and the game opens on `WEAPONS.JSON`, `ARMING_SWORD`, and a detail
  ending `RESTRICTED.JSON EXCLUDES [PUSH, BLOCK]: PUSH IS ALREADY FORGED HERE`. A
  `weapons.json` that renames a weapon only the starting party names — A's
  `weakspot_stiletto`, C's `great_axe` — stops on `WEAPONS.JSON` too, naming the
  member.
- **A missing file plays the built-in one, and says so**: delete
  `weapons.json` and the game plays the built-in weapons, with
  `WEAPONS.JSON IS MISSING - THE BUILT-IN WEAPONS ARE PLAYING` under the title
  and the path it looked in in the log. The same goes for every content file.
- `--write-data <folder>` writes all four files: `tuning.json` and the three
  content files. Run on the folder beside the exe it resets every edited copy;
  `--write-data GameEngine.DistanceRPG/Data` regenerates the shipped ones after a
  change to `ContentDefaults.cs`.

## How the port was verified

`GameEngine.DistanceRPG.Tests/TestData/distancerpg-golden.json` was generated
by running the original game's JavaScript modules under Node. The C# port
reproduces them bit-for-bit: the mulberry32 RNG stream, the full 70×50 dungeon
for the shipped map seed, fog-of-war union boxes, and A* paths.
