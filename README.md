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
beaten it lists the tutorial alone.

| Between runs | Action |
| --- | --- |
| W/S / arrows | Choose a dungeon (a locked one shows its countdown and is skipped) |
| Enter | Descend into the chosen dungeon |
| Esc | Quit the game |

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
| Esc | Close the open menu (at the way out's prompt, that is no), otherwise open/close the pause menu. Does nothing on a run's summary |
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
  (or over the first floor when `DISTANCERPG_DUNGEON` boots straight in) and
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
  writes the shipped file into `<folder>` and exits without opening a window.
  A number worth keeping goes into `GameEngine.DistanceRPG/Logic/Content/Tuning.cs`,
  and `--write-data GameEngine.DistanceRPG/Data` regenerates the shipped file.
- Nothing the golden tests pin — the map generator, the random streams, the
  pathfinder — is in it, and a test keeps it that way.

## How the port was verified

`GameEngine.DistanceRPG.Tests/TestData/distancerpg-golden.json` was generated
by running the original game's JavaScript modules under Node. The C# port
reproduces them bit-for-bit: the mulberry32 RNG stream, the full 70×50 dungeon
for the shipped map seed, fog-of-war union boxes, and A* paths.
