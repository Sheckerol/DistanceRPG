# DistanceRPG

A turn-based party RPG where movement is the resource: every step, attack, and
saved manoeuvre spends the same per-turn distance budget. Ported from the
original [Phaser prototype](https://github.com/Sheckerol/Game) onto the
custom [GameEngine](https://github.com/Sheckerol/GameEngine), reinterpreted as
a 3D dungeon with an angled top-down camera.

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

| Input | Action |
| --- | --- |
| WASD / arrows | Move the active character |
| 1–4 / Tab / click | Switch party member |
| Left-click enemy | Attack with the equipped weapon |
| Space / Enter | End turn (banks half your unspent movement) |
| I / B | Inventory (2/3 swaps a bag slot with the equipped slot) |
| Scroll wheel | Zoom |
| Esc | Close inventory, otherwise quit |

Outside combat — while no living enemy has been sighted this turn — the rest
of the party marches behind the active character in a single-file line, and
the group paces itself to the member with the least movement left. Sighting
the enemy breaks formation: everyone stops following and characters body-block
each other until a turn passes without seeing it.

## How the port was verified

`GameEngine.DistanceRPG.Tests/TestData/distancerpg-golden.json` was generated
by running the original game's JavaScript modules under Node. The C# port
reproduces them bit-for-bit: the mulberry32 RNG stream, the full 70×50 dungeon
for the shipped map seed, fog-of-war union boxes, and A* paths.
