# DistanceRPG Roadmap

Planned features beyond the current port. Everything here is a design target,
not shipped behaviour — see the git history for what exists today.

Six phases, in dependency order. Each is one PR off up-to-date `main`, per the
workflow rule in `CLAUDE.md`.

| Phase | Theme | Depends on |
| --- | --- | --- |
| 0 | Unify actors; universal status effects | — |
| 1 | Weapon classes and variants | 0 |
| 2 | Progression: innate stats, weapon/health/mana XP | 1 |
| 3 | Loot drops and enchantments | 1, 2 |
| 4 | Multiple floors | — (but lands after 3) |
| 5 | Save / load | 0–4 |
| 6 | Between runs: wear, the enchanter, the hub | 1–5 |

## Where everything lives

This file is the index. Each section below is its own document under
`docs/roadmap/`, and **`§n.m` references throughout the docs stay canonical** —
use this table to find the file a section lives in.

| Document | Sections | What is in it |
| --- | --- | --- |
| [phase-0-actors](docs/roadmap/phase-0-actors.md) | — | `ActorState`, universal status effects |
| [phase-1-modifiers](docs/roadmap/phase-1-modifiers.md) | §1.1 | **The load-bearing one.** Stacks, forged vs acquired, `forged + 5`, the two forge limits |
| [phase-1-weapons](docs/roadmap/phase-1-weapons.md) | §1.2–1.5 | The eight classes, their four variants each, casters, uniques |
| [phase-1-combat](docs/roadmap/phase-1-combat.md) | §1.6 | Crit riders, crits bypassing Block, the damage pipeline |
| [phase-1-code](docs/roadmap/phase-1-code.md) | §1.7 | `ModifierSet`, migration, what `TurnSystem` and the HUD gain |
| [phase-2-progression](docs/roadmap/phase-2-progression.md) | §2.1–2.3 | Innate stats, the three XP pools |
| [phase-3-loot](docs/roadmap/phase-3-loot.md) | §3.1–3.5 | Drops, the repeat-kill ladder, enchantments, consumables |
| [phase-4-floors](docs/roadmap/phase-4-floors.md) | §4.1–4.5 | Stairs, the boss floor, dungeon themes, fighting your way out |
| [phase-5-save](docs/roadmap/phase-5-save.md) | §5.1–5.2 | What is stored, and in what format |
| [phase-6-between-runs](docs/roadmap/phase-6-between-runs.md) | §6.1–6.6 | Wear, the enchanter, service and grafting, the hub |
| [open-questions](docs/roadmap/open-questions.md) | — | Not yet decided. Append here rather than hedging in place |
| [settled](docs/roadmap/settled.md) | — | Decided, with the reason. Append here when something stops being open |

**Start with `phase-1-modifiers`.** Almost every later decision — loot depth,
boss themes, the enchanter, what a unique even is — is expressed as stacks under
the rules in §1.1, and reads as arbitrary without them.

## Standing constraints

These hold for every phase:

- **Golden tests stay green.** `TestData/distancerpg-golden.json` pins the
  mulberry32 stream, the 70×50 dungeon for the shipped seed, fog union boxes,
  and A* paths. Nothing in these docs may reorder an RNG call in `MapGenerator`,
  `Mulberry32`, or `Pathfinder`. New randomness gets its **own stream derived
  from the map seed**, exactly as `EnemyPlacer` already does
  (`mapSeed ^ SeedSalt`) — never a continuation of an existing one.
- **`Logic/` stays engine-free and deterministic.** No OpenTK types, no
  `Random.Shared` outside an injected delegate. Presentation subscribes to
  `TurnSystem` events; it never owns rules.
- **Distances stay in logic units** (32 per tile). Conversion happens only at
  the `WorldSpace` boundary.
