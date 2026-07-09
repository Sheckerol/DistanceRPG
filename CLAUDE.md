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
  DistanceRpgGame.cs            # Game subclass; registers DungeonScene
  DungeonScene.cs               # Scene: geometry, input, fog wiring, events
  DungeonHud.cs                 # Screen-space HUD (bitmap text, floating combat text, inventory panel)
  CharacterObject.cs / EnemyObject.cs   # 3D stand-ins mirroring logic positions
  FogOverlayRenderer.cs         # Fog quad layer (opaque unexplored / translucent explored)
  WorldSpace.cs                 # Logic px (32/tile, y-down) ↔ world units (1/tile, XZ plane)
  Logic/                        # ENGINE-FREE gameplay core — no OpenTK/engine types allowed
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
