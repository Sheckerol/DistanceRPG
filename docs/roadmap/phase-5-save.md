# Phase 5 — Save / load

Lands last, once the state model above is final.

## 5.1 What is stored

| Group | Contents |
| --- | --- |
| Run | Map seed, current floor index, turn count |
| Party | Position, HP, mana, innate stats, all three XP pools, inventory (each weapon carrying **both** its forged and its live modifier sets, plus enchantments and wear), active status effects |
| Per visited floor | Enemy states (position, HP, alive, `DefeatedAtTurn`, `DefeatCount`, **accumulated revival damage and HP bonuses**, weapon, enchantments, status effects) and the explored fog grid |

Maps are **never serialized** — they regenerate from `floorSeed(n)`. This is
exactly the design `EnemyPlacer`'s doc comment anticipated: regenerate the
dungeon from the seed on every entry, place enemies only on the first visit,
restore saved states afterwards.

## 5.2 Format

`System.Text.Json` with an explicit version field and a DTO layer in
`Logic/Save/` — the gameplay types must not carry serialization attributes, or
`Logic/` stops being clean.

Round-trip test: save, reload, and assert that a fixed sequence of turns
produces an identical world state.


## 5.3 `tuning.json` — the numbers, editable between loads

Almost every open question in these documents is a **number nobody can pick on
paper**: the mana divisor (§1.3), the three Searing constants (§1.5), the
revival step (§3.2), fight-length-dependent decay. Recompiling to try `8`
instead of `16` is the difference between tuning a game and not tuning it.

So: a **`tuning.json` beside the executable**, read once at startup into
`GameConstants`, overriding compiled defaults.

### What it must never contain

**Nothing the golden tests pin.** `MapGenerator`, `Mulberry32` and `Pathfinder`
constants stay compiled, permanently and without exception — a tunable RNG
constant is a tunable *parity* constant, and `TestData/distancerpg-golden.json`
stops meaning anything the moment one is editable.

That wants to be a test rather than a comment: assert the tuning schema's key
set is disjoint from a hardcoded list of parity-critical names, so adding one by
accident fails the build rather than silently invalidating the goldens.

The same applies to anything **structural** — the eight classes, the four
variants each, the modifier relations of §1.1. Those are data, but they are data
with invariants (`Allowed`, `MaxForged`), and a JSON file that can violate them
is a crash waiting on a typo. `tuning.json` holds **scalars only**.

### Rules that keep it honest

- **Read once, at startup.** Never hot-reloaded mid-run. A run has to resolve
  under one set of numbers or its own save stops reproducing.
- **Missing keys fall back to the compiled default.** A partial file is valid,
  so the player edits the three lines they care about — and adding a constant
  later never invalidates an existing file.
- **Ratios are integer divisors, not floats.** `MovementUnitsPerMana: 16`, not
  `ManaPerMovementUnit: 0.0625`. It reads correctly, it cannot drift, and it
  makes the units explicit in the name — which is the exact error §1.3 already
  made once by thinking in tiles while the code counted units.
- **Out-of-range values clamp and warn** rather than throwing. This is a file a
  human edits by hand; a typo should cost a log line, not a session.

### The starting set

Everything currently flagged as a knob, gathered in one place:

| Group | Keys |
| --- | --- |
| Economy | `MovementUnitsPerMana`, `MovementBudget` |
| Statuses | `SearLevelsPerTier`, `SearDamagePerLevel`, `SearDecayPerTurn`, `RiderDamagePerLevel`, `RiderDecayPerTurn` |
| Modifiers | `AcquiredHeadroom`, per-stack values, `MaxForged` overrides |
| Farming | `DefeatStackChance`, `FarmStackAllowance`, `ReviveStep`, `ResurrectTurnsBase`, `ResurrectTurnsFloor`, `CleanKillBonus` |
| Uniques | `UniqueChanceCeiling`, `UniqueChanceMidpoint`, `UniqueChanceK` |
| Dungeon | `BossFloorMin`, `BossFloorMax`, `BossStackOffset`, `CooldownRunsPerFloor`, `MercyFloor` |
| Inventory | `PartyCarrySlots` |

Collecting them is worth as much as making them editable. Half the open
questions in these documents are "this number, against that number" — the mana
divisor against cast costs, Searing decay against fight length, the farm
allowance against `forged + 5`. Seeing them on one screen is how those get
resolved.

### Saves have to notice

A save records the **hash of the tuning values it was played under**. Loading
under different numbers warns rather than refuses — retuning mid-campaign is a
thing a designer does on purpose, and silently changing a saved game's rules
underneath the player is the failure to avoid.

The full values are not stored. The hash costs nothing and answers the only
question that matters: *are these the same numbers?*
