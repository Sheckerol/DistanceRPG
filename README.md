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

| Input | Action |
| --- | --- |
| WASD / arrows | Move the active character |
| 1–4 / Tab / click | Switch party member (click their box or the party list) |
| Left-click enemy | Attack with the equipped weapon |
| Space / Enter | End turn (banks half your unspent movement) |
| I / B | Inventory (2/3 swaps a bag slot with the equipped slot) |
| Scroll wheel | Zoom |
| Esc | Close inventory, otherwise open/close the pause menu |

Outside combat the rest of the party marches behind the active character in a
single-file line, the group paces itself to the member with the least movement
left, and the turn cycles automatically once everyone's movement runs out.
Marching is granted only at the start of a turn, and only if no living enemy
is visible and everyone has regrouped within their max movement of the leader
— so a party split by a fight doesn't charge back into formation the moment
line of sight breaks. Sighting the enemy revokes it for the rest of the turn:
everyone stops following, characters body-block each other, and turns end
manually.

## How the port was verified

`GameEngine.DistanceRPG.Tests/TestData/distancerpg-golden.json` was generated
by running the original game's JavaScript modules under Node. The C# port
reproduces them bit-for-bit: the mulberry32 RNG stream, the full 70×50 dungeon
for the shipped map seed, fog-of-war union boxes, and A* paths.
