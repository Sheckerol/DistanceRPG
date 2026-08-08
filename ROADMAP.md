# DistanceRPG Roadmap

Planned features beyond the current port. Everything here is a design target,
not shipped behaviour — see the git history for what exists today.

**This is a mechanics document, not the lore.** The setting is worked out
elsewhere and most of it never touches these systems. Lore appears here only
where it *earns* a rule. The enchanter powering the tutorial dungeon (§4.3) is
the clearest case, settling that dungeon's depth, its retirement and the
workshop's availability at once; a weapon being *maintained* rather than
levelling (§6.4) is what makes the graft roll the craftsman's eye instead of
luck; and mana burning an enchantment's circle deeper into a particular weapon
(§3.3) is why depth cannot be bought and why transfer costs a tier. Anything
narrative that does not decide a mechanic does not belong in these files, and
nothing here should be treated as the canonical account of the world.

Six phases, in dependency order, shipping as seven PRs off up-to-date `main`
per the workflow rule in `CLAUDE.md` — Phase 6 splits in two (below).

| Phase | Theme | Depends on |
| --- | --- | --- |
| 0 | Unify actors; universal status effects | — |
| 1 | Weapon classes and variants | 0 |
| 2 | Progression: innate stats, weapon/health/mana XP | 1 |
| 3 | Loot drops and enchantments | 1, 2 |
| 4 | Multiple floors | — (but lands after 3) |
| 5 | Save / load | 0–4 |
| 6a | Between runs: wear, the enchanter, the two services | 1–5 |
| 6b | The hub: the farm, the alchemist, consumables | 6a |

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
| [phase-5-save](docs/roadmap/phase-5-save.md) | §5.1–5.8 | What is stored, in what format, and the five data files |
| [phase-6-between-runs](docs/roadmap/phase-6-between-runs.md) | §6.1–6.7 | Wear, the enchanter, enchant-or-refine, grafting, the farm, the hub |
| [open-questions](docs/roadmap/open-questions.md) | — | Not yet decided. Append here rather than hedging in place |
| [settled](docs/roadmap/settled.md) | — | Decided, with the reason. Append here when something stops being open |

**Phase 6 ships as two PRs.** It grew past the one-PR-per-phase rule in
`CLAUDE.md`, and it splits cleanly because the halves share no state:

| | Sections | Why it can go alone |
| --- | --- | --- |
| **6a** | §6.1–6.5, §6.7 | Wear, capacity, the two services, transfer. Needs `CampaignState` and the stable; touches `Weapon` and the enchanter queue |
| **6b** | §6.6, §3.4 | The farm, the alchemist, potions. Needs only a clear set and an inventory slot that can hold a non-weapon |

6a is the one the rest of the design leans on — §1.1's ceilings are reached
through it and §3.3's transfer rule depends on it. 6b is additive and could
slip a release without anything else noticing, which is the definition of a
good seam.

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
  the `WorldSpace` boundary. A constant that converts between units and anything
  else says so **in its name** — `MovementUnitsPerMana`, not a bare rate. §1.3
  had this wrong once by reasoning in tiles while the code counted units, and it
  was a factor of 32.
- **Tunable numbers live in `tuning.json`** (§5.3), read once at startup, never
  hot-reloaded, always with a compiled fallback. Nothing the golden tests pin
  may ever go in it.
- **Behaviour attaches through the event table** (§1.7), never through a
  `switch` a new entry has to be added to. A handler **transforms a payload and
  returns it** so the next one can act on the result — nothing mutates the world
  mid-chain, and the dispatcher applies the settled outcome once. Order is a
  declared priority (§1.6's damage pipeline is the canonical one), raised events
  **queue rather than recurse**, and the subscriber list is an ordered structure
  rather than a `Dictionary` iterated directly, because dispatch order is
  covered by the determinism rule above.
- **Content lives in `restricted.json`, `weapons.json`, `enchantments.json` and
  `dungeons.json`** (§5.4–5.8), loaded in that order and validated at load,
  referenced everywhere by **stable string id** rather than by index. Only
  *behaviour* stays in code — relations are data too, or adding a modifier would
  still be a recompile. A dungeon's drop table is computed rather than authored.
