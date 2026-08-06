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

## 5.4 `weapons.json` — the content, validated at load

The 32 weapons, the uniques, and the class baselines move out of `Weapons.cs`
into data. Statline, forged spread, area shape, innate enchantment — all of it.

### What this actually buys: modifiers become additive

The reason to do it is not that weapon numbers become editable, though they do.
It is that **adding a modifier stops being a content change.** The work splits
three ways and only one part is code:

| What | Lives in | Why there |
| --- | --- | --- |
| A modifier's **behaviour** | Code | It is a rule. `Brace` fires a retaliation; that has to run |
| Its **per-stack value** | `tuning.json` | It is a number |
| Its **relations** — `Excludes`, `Requires`, `RequiresKind`, `ForgedOnly` | Code | They are the invariants the data is checked *against*; editable relations could not validate editable weapons |
| **Which weapons carry it** | `weapons.json` | Content |

So adding `Sundering Roar` or whatever comes next is: implement the behaviour,
add a per-stack value, add any relations it needs — and then it can go on any
weapon, in any spread, forever, without touching the code again. Every existing
weapon can be revised to use it in the same edit.

That is the difference between a modifier system and a modifier *list*. The
whole of §1.1 is built on modifiers being uniform — stacks, one mechanism, no
bespoke cases — and this is that uniformity finally paying out in the workflow
rather than only in the design.

### It is validated, and that is the difference from `tuning.json`

`tuning.json` is scalars that clamp and warn. **Weapon data carries invariants**,
so it is checked at load against the predicates that already exist:

| Check | Rule | From |
| --- | --- | --- |
| Every forged spread is legal | `Allowed(t, kind, present)` | §1.1 |
| Nothing forged too deep | `MaxForged` — `×3`, or `×1` for `Light`/`Resonant` | §1.1 |
| No weapon has fewer than two forged axes | The farm allowance has somewhere to go | §1.2 |
| A unique derives from a variant | Exactly one modifier raised to `×3`, plus its enchantment | §1.5 |
| Casters carry exactly one innate enchantment | Staff fixed by variant, wand a damage type | §3.1 |

§1.7 already lists "weapon data validation" as a caller of `Allowed`. This is
that caller, and the point is that **no new validation code exists** — the
loader asks the same questions the graft roll and the boss drop table ask.

**Invalid data aborts startup**, naming the weapon and the rule it broke. Not
clamped, not skipped. A bad scalar is a bad balance number and the game still
runs; a bad weapon is a broken invariant that `Allowed` callers downstream
assume holds, and limping past it turns a typo into a crash somewhere unrelated.

### Weapons are referenced by stable id, never by index

This is the one thing that will bite if it is got wrong later.

`EnemyPlacer` assigns weapons from a seeded stream, and saves record what every
enemy is carrying. If either refers to a weapon by its **position** in the file,
then adding a weapon silently re-rolls every enemy in every existing save and
breaks seed reproducibility for no visible reason.

So: every weapon carries a **stable string id**, and saves, drops and enemy
states all store that. Reordering the file is then free, and adding to it costs
nothing.

The placement roll needs the same care and §3.1 already has it right — it rolls
over the **eight classes** with the variant rolled later, so the stream depends
on a count fixed in code rather than on the file's length. Adding a fifth
variant to a class would still shift things; that is a deliberate, rare change
rather than an ordinary edit.

### Not the golden tests' problem

Nothing here touches `MapGenerator`, `Mulberry32` or `Pathfinder`, so
`TestData/distancerpg-golden.json` is unaffected — it pins map generation, fog
and pathing, never weapon statlines. Worth stating plainly, because "weapons are
data now" sounds like it should be a parity concern and is not.
