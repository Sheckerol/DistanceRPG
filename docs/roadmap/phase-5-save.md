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

## 5.4 The content files, and what they share

Three files hold content: **`weapons.json`**, **`enchantments.json`**,
**`dungeons.json`**. They are a different kind of thing from `tuning.json`,
which is scalars — these carry **invariants**, so they are validated rather than
clamped.

### The reason, and it is not that numbers become editable

Numbers becoming editable is a convenience. The real gain is that **adding a
mechanic stops being a content change.** The work splits, and only one part is
code:

| What | Lives in | Why there |
| --- | --- | --- |
| A modifier's or enchantment's **behaviour** | Code | It is a rule. `Brace` fires a retaliation; that has to run |
| Its **per-stack or per-tier value** | `tuning.json` | It is a number |
| Its **relations** — `Excludes`, `Requires`, `RequiresKind`, `ForgedOnly`, type opposition | Code | They are the invariants the content is checked *against*; editable relations could not validate editable content |
| **Which weapons, dungeons and drops use it** | The content files | Content |

So adding a modifier is: implement the behaviour, price it, declare its
relations — and it is then usable on any weapon in any spread, forever, without
touching code again. Every existing weapon can adopt it in the same edit. That
is the difference between a modifier *system* and a modifier *list*, and it is
§1.1's uniformity finally paying out in the workflow rather than only in the
design.

### The shared contract

All three follow the same rules, and it is worth stating once:

- **Every entry has a stable string id.** Saves, drops, enemy states and the
  enchanter's seen-catalogue all store ids, never indices or positions.
  Reordering a file is then free and appending to one costs nothing.
- **Validated at load against the predicates that already exist** — `Allowed`,
  `MaxForged`, the type-opposition table. No new validation logic: the loader
  asks the same questions the graft roll and the boss drop table ask.
- **Invalid content aborts startup**, naming the entry and the rule it broke.
  Not clamped, not skipped. A bad scalar in `tuning.json` is a bad balance
  number and the game still runs; a broken invariant is something every
  downstream caller assumes holds, and limping past it turns a typo into a
  crash somewhere unrelated.
- **Nothing here touches the golden tests.** `TestData/distancerpg-golden.json`
  pins map generation, fog and pathing — never statlines or drop tables. Worth
  saying plainly, because "the content is data now" sounds like a parity concern
  and is not.

### Load order is a real constraint

The files reference each other in one direction, so they load in one order:

```
tuning.json  →  enchantments.json  →  weapons.json  →  dungeons.json
```

`weapons.json` names enchantment ids for caster innates and unique souls;
`dungeons.json` names a modifier for its theme and a damage type for its
attunement, and its **drop table is computed from the weapon list** rather than
read. A cycle here would be a design error rather than a loader problem — if
content ever needs to reference forward, the thing it is reaching for probably
belongs in code.

## 5.5 `weapons.json`

The 32 weapons, the uniques, and the class baselines leave `Weapons.cs`.
Statline, forged spread, area shape, innate enchantment id.

| Check | Rule | From |
| --- | --- | --- |
| Every forged spread is legal | `Allowed(t, kind, present)` | §1.1 |
| Nothing forged too deep | `MaxForged` — `×3`, or `×1` for `Light`/`Resonant` | §1.1 |
| No weapon has fewer than two forged axes | The farm allowance has somewhere to go | §1.2 |
| A unique derives from a variant | Exactly one modifier at `×3`, plus its unique enchantment | §1.5 |
| Casters carry exactly one innate enchantment | Staff fixed by variant, wand a damage type | §3.1 |

§1.7 already lists "weapon data validation" as a caller of `Allowed`. This is
that caller, and the point is that **no new validation code exists**.

**The index trap is sharpest here.** `EnemyPlacer` assigns weapons from a seeded
stream and saves record what every enemy carries, so a positional reference
means adding one weapon silently re-rolls every enemy in every existing save and
breaks seed reproducibility for no visible reason.

§3.1's placement roll is already right for the same reason — it rolls over the
**eight classes** with the variant rolled later, so the stream depends on a count
fixed in code rather than on the file's length. Adding a fifth variant to a class
would still shift it; that is a deliberate, rare change rather than an ordinary
edit.

## 5.6 `enchantments.json`

The catalogue and the unique souls: lock, trigger cost, condition, base potency,
effect reference, `unique` flag, and a damage type where the entry is elemental.

**The effect itself stays code.** "Heal the wielder for damage dealt" is a rule,
so an entry names an `EffectKind` and supplies parameters — the same shape as a
modifier naming a behaviour it does not implement.

| Check | Rule | From |
| --- | --- | --- |
| Trigger cost is **never zero** | No enchantment fires free, ever | §3.3 |
| Unique entries are tier-pinned and out of the catalogue | The enchanter cannot copy them | §3.3 |
| Damage-type opposition is symmetric and total | Four types, two pairs, every type opposed by exactly one | §1.4 |
| A lingering element names an element that exists | Searing is nothing without its `Flaming` | §1.5 |
| Every effect reference resolves to an `EffectKind` | A typo is not a silent no-op | — |

The first is the one to enforce hardest. §3.3 spends a section on *every trigger
costs mana* holding up three separate arguments — the resource gate, the
levelling path, and `Resonant` having something to discount — and all three fail
quietly if a single entry ships with `0`.

**The id trap is sharper here than for weapons**, because the enchanter's
catalogue is a `HashSet` of enchantment ids in `CampaignState` (§3.5). If ids
shift, a player's accumulated catalogue does not merely mis-sort — it silently
becomes a set of different enchantments, or of nothing.

## 5.7 `dungeons.json`

Name, theme modifier, attunement, boss definition, floor range, and the flags
the tutorial needs.

| Field | Notes |
| --- | --- |
| `theme` | A `ModifierType`. Forged on the boss's own drop, added to the roll pool for every other drop in the dungeon (§4.3) |
| `attunement` | A damage type, or none. Applies to everything the dungeon spawns (§1.4) |
| `bossFloorMin` / `bossFloorMax` | `5`–`10` for ordinary dungeons; the tutorial pins both to `2` |
| `boss` | Statline plus innate modifiers — the golem is `Block ×3` whatever it holds |
| `retiresOnClear` | Tutorial only. Everything else takes the `bossFloor`-run cooldown (§4.3) |

**The drop table is computed, not authored.** §4.3 says a themed boss cannot
drop a class that will not take its theme, and that is `Allowed` run over the
eight classes at load — so the file names a theme and the loader derives which
classes remain. Authoring it by hand would let the two disagree, and the
disagreement would look exactly like a drop-rate bug.

| Check | Rule | From |
| --- | --- | --- |
| At least one class can carry the theme | Otherwise the boss drops nothing at all | §4.3 |
| The theme is a **class signature** | So each themed dungeon can drop exactly one named unique, via the Purity coincidence | Settled |
| `retiresOnClear` implies a fixed boss floor | The tutorial's two floors are the enchanter's mana budget, not a roll | §4.3 |
| Only one dungeon retires on clear | Two un-repeatable dungeons is a content decision nobody has made | §4.3 |

The second is the one that will get broken by accident, because a theme that is
*not* a class signature still works — it just quietly means that dungeon can
never produce its unique, and nothing else in the game announces the difference.
