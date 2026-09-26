# CLAUDE.md

## Project Overview

DistanceRPG — a turn-based party RPG (movement-as-resource combat) ported from
the Phaser prototype at https://github.com/Sheckerol/Game onto the custom C#
engine at https://github.com/Sheckerol/GameEngine, rendered as a 3D dungeon.
.NET 10, OpenTK via the engine.

**The engine is a sibling checkout, not a package**: `GameEngine/` must be
cloned next to this repo (project references use `..\..\GameEngine\...`).
Engine changes belong in the GameEngine repo, never here.

## Build & Run

```bash
dotnet build
dotnet run --project GameEngine.DistanceRPG/GameEngine.DistanceRPG.csproj
dotnet test GameEngine.DistanceRPG.Tests/GameEngine.DistanceRPG.Tests.csproj
```

## Workflow Rules

`main` only advances through merged pull requests. Start feature branches from
up-to-date main, push, and open a PR with `gh pr create`.

## Structure

```
GameEngine.DistanceRPG/         # Game executable
  Program.cs                    # Entry point (crash handler + logging)
  DistanceRpgGame.cs            # Game subclass: the title, then the screen between runs and the runs
  TitleScene.cs                 # The first scene: CONTINUE / NEW CAMPAIGN / LOAD GAME / QUIT (TitleHud,
                                # TitleText; SlotMenu and SlotText, the slot list)
  SaveSlots.cs                  # The save folder's slots: its campaign.json is the autosave, slot-n/ the
                                # manual slots, each written by the campaign loaded from it (SaveStore one
                                # slot, SaveListing what each holds, CampaignBoot a slot's load and every write)
  PauseMenu.cs                  # The menu ESC opens in play, both scenes': 1 CLOSE, 2 SAVE GAME (the save
                                # list, SlotMenu's save mode, at a safe point), 3 QUIT TO TITLE (IRunHost.cs'
                                # ISaveHost is what the scenes ask of the game for it)
  DungeonScene.cs               # Scene: geometry, input, fog wiring, events
  DungeonHud.cs                 # Screen-space HUD (bitmap text, floating combat text, inventory panel)
  CharacterObject.cs / EnemyObject.cs   # 3D stand-ins mirroring logic positions
  FogOverlayRenderer.cs         # Fog quad layer (opaque unexplored / translucent explored)
  WorldSpace.cs                 # Logic px (32/tile, y-down) ↔ world units (1/tile, XZ plane)
  ShippedData.cs                # Reads the embedded JSON masters: the built-in fallback, at the edge
  Data/                         # The JSON MASTER COPIES, hand-kept: tuning.json, restricted.json,
                                # weapons.json, dungeons.json — embedded in the build as the
                                # fallback, and copied beside the exe for playtest edits
  Logic/                        # ENGINE-FREE gameplay core — no OpenTK/engine types allowed
    Content/                    # Loading and validating the data files; StartingParty.cs (code)
    Enchantments/               # EnchantmentDefinitions.cs: the enchantment catalogue (code)
    Mulberry32.cs               # JS-bit-identical RNG (map seeds must reproduce)
    MapGenerator.cs             # Rooms + greedy-MST corridors (RNG call order is load-bearing)
    Pathfinder.cs               # A*, octile heuristic, JS-identical heap tie-breaking
    FogGeometry.cs / FogBoxBuilder.cs / FogState.cs   # Fog-box math + runtime grids
    GridCollision.cs            # Axis-separated circle-vs-tile-grid collision
    TurnSystem.cs               # Turn state machine, drives combat via events
    EnemyAi.cs                  # Targeting + budget-limited move planning
    CombatRules.cs / Weapons.cs / GameConstants.cs / ...
GameEngine.DistanceRPG.Tests/   # xUnit; TestData/distancerpg-golden.json is
                                # generated from the ORIGINAL JS modules under
                                # Node — regenerate only from the Game repo,
                                # never hand-edit
```

## Rules of thumb

- Gameplay rules/measurements live in `Logic/` and stay engine-free and
  deterministic; presentation subscribes to `TurnSystem` events.
- Distances are in logic units (original pixels, 32 per tile). Convert only at
  the presentation boundary via `WorldSpace`.
- Changes to MapGenerator/Mulberry32/Pathfinder must keep the golden tests
  green — they guarantee parity with the original game.
- Behaviour and reference data (the modifiers, the enchantment catalogue in
  `Logic/Enchantments/EnchantmentDefinitions.cs`, the starting party in
  `Logic/Content/StartingParty.cs`) live in code; the relations, the weapon
  types, the weapons (the caster drop order included), the dungeons and the
  tuning numbers live only in `GameEngine.DistanceRPG/Data/*.json`, the master
  copy the build embeds as the fallback (the user's ruling of 2026-09-25). Never
  add a C# copy of a JSON value: the load validates the code catalogues against
  the JSON instead. Nothing the golden tests pin may go in `tuning.json`.
- A `tuning.json` key arrives with the code that reads it — except a number the
  user has ruled and wants in the file first (the Enchanter group's, until Phase
  6a): declare it in `TuningSchema` with `Played = false`, and it is read,
  clamped and warned like every key but left out of the save's tuning hash, so
  turning it never says the numbers changed. The change that gives a rule of
  play one to read sets it played. A key no rule of play reads at all — the
  launcher's `SaveSlots`, how many manual save slots there are — is
  `Played = false` for good.
- Every number of a content file has a range (`Logic/Content/ContentBounds.cs`,
  asked by `ContentValidator.RequireInRange` under `RuleNumberInRange`), its top
  read off a `TuningSchema` clamp or a rule, never a master's value: a new numeric
  field of `weapons.json` or `dungeons.json` gets its row, with why, in the same
  change — `ContentBoundsTests` fails until it has one.
- A save is raw JSON too, and no field of it is trusted: `SaveMapper` reads each
  count against its floor and, where a larger one would wrap the arithmetic that
  reads it, a ceiling off `ContentBounds` — never the tuning in hand — and then
  `Logic/Save/SaveConsistency.cs` checks the fields against each other; a save
  that fails is set aside, never restored. A new field of the save gets its
  checks in the same change, and everything play writes must still load
  (`SaveConsistencyTests`).
