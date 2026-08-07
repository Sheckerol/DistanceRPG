# Phase 5 — Save / load

Lands last, once the state model above is final.

## 5.1 What is stored

| Group | Contents |
| --- | --- |
| Run | Map seed, current floor index, turn count |
| Party | Position, HP, mana, innate stats, all three XP pools, inventory (each weapon carrying **both** its forged and its live modifier sets, plus enchantments, wear and `WearCapacity`), active status effects |
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
- **Ratios are integer divisors, not floats.** `MovementUnitsPerMana: 10`, not
  `ManaPerMovementUnit: 0.0625`. It reads correctly, it cannot drift, and it
  makes the units explicit in the name — which is the exact error §1.3 already
  made once by thinking in tiles while the code counted units.
- **Out-of-range values clamp and warn** rather than throwing. This is a file a
  human edits by hand; a typo should cost a log line, not a session.

### The starting set

Everything currently flagged as a knob, gathered in one place:

| Group | Keys |
| --- | --- |
| Economy | `MovementUnitsPerMana`, `MovementBudget`, `StartingMaxMana` |
| Statuses | `SearLevelsPerTier`, `SearDamagePerLevel`, `SearDecayPerTurn`, `RiderDamagePerLevel`, `RiderDecayPerTurn`, `WardDecayPerTurn`, `OverhealPerWard` |
| Modifiers | `AcquiredHeadroom`, per-stack values, `MaxForged` overrides |
| Farming | `DefeatStackChance`, `FarmStackAllowance`, `ReviveStep`, `ResurrectTurnsBase`, `ResurrectTurnsFloor`, `CleanKillBonus` |
| Uniques | `UniqueChanceCeiling`, `UniqueChanceMidpoint`, `UniqueChanceK` |
| Dungeon | `BossFloorMin`, `BossFloorMax`, `BossStackOffset`, `CooldownRunsPerFloor`, `MercyFloor` |
| Enchanter | `WearPerHit`, `StartingWearCapacity`, `CapacityPerEnchanting`, `CapacityPerRefinement`, `EnchantmentWearCost`, `WearPerImprovementRoll`, `MaxImprovementChance` |
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

Four files hold content: **`restricted.json`**, **`weapons.json`**,
**`enchantments.json`**, **`dungeons.json`**. They are a different kind of thing
from `tuning.json`, which is scalars — these carry **invariants**, so they are
validated rather than clamped.

### The reason, and it is not that numbers become editable

Numbers becoming editable is a convenience. The real gain is that **adding a
mechanic stops being a content change.** The work splits four ways, and only one
part is code:

| Question | Answered by | Why there |
| --- | --- | --- |
| **What does it do?** | Code | It is a rule. `Brace` fires a retaliation; that has to run |
| **What number?** | `tuning.json` | It is a scalar |
| **What may it coexist with, and can it be rolled?** | `restricted.json` | It is a relation, and relations change every time a mechanic is added |
| **Which weapons and dungeons use it?** | The content files | It is content |

So adding a modifier is: implement the behaviour, price it, declare its
relations — **and none of that last part is a recompile.** Every existing weapon
can adopt it in the same edit. That is the difference between a modifier
*system* and a modifier *list*, and it is §1.1's uniformity finally paying out
in the workflow rather than only in the design.

Relations were originally going to stay in code, on the reasoning that they are
what validates the content and so cannot themselves be content. That was wrong,
and specifically it was wrong in the way that mattered most: **if declaring a
relation needs a recompile, then adding a modifier needs a recompile**, and the
additive property the whole split exists to buy is not real. The validation
concern is genuine but it is answered by validating the relations too (§5.5),
not by freezing them.

### The shared contract

All four follow the same rules, and it is worth stating once:

- **Every entry has a stable string id.** Saves, drops, enemy states and the
  enchanter's seen-catalogue all store ids, never indices or positions.
  Reordering a file is then free and appending to one costs nothing.
- **Validated at load against the predicates that already exist** — `Allowed`,
  `MaxForged`, the type-opposition table. No new validation logic for content:
  the loader asks the same questions the graft roll and the boss drop table ask.
- **Invalid content aborts startup**, naming the entry and the rule it broke.
  Not clamped, not skipped. A bad scalar in `tuning.json` is a bad balance
  number and the game still runs; a broken invariant is something every
  downstream caller assumes holds, and limping past it turns a typo into a
  crash somewhere unrelated.
- **Nothing here touches the golden tests.** `TestData/distancerpg-golden.json`
  pins map generation, fog and pathing — never statlines, relations or drop
  tables. Worth saying plainly, because "the content is data now" sounds like a
  parity concern and is not.

### Load order is a real constraint

The files reference each other in one direction, so they load in one order:

```
tuning.json → restricted.json → enchantments.json → weapons.json → dungeons.json
```

`restricted.json` is the predicates everything after it is checked against;
`weapons.json` names enchantment ids for caster innates and unique souls;
`dungeons.json` names a modifier for its theme, and its **drop table is computed
from the weapon list** rather than read. A cycle here would be a design error
rather than a loader problem — if content ever needs to reference forward, the
thing it is reaching for probably belongs in code.

## 5.5 `restricted.json` — what may coexist, and what may be rolled

The relations from §1.1, for modifiers and enchantments alike, in one file:

| Key | Holds | Shape |
| --- | --- | --- |
| `excludes` | The exclusion groups — threat zone, displacement, block response, crit rider, currency, and the opposed damage-type pairs | Arrays of ids; every member excludes every other |
| `requires` | Dependencies — `Riposte` needs `Block`, a lingering element needs its element | id → array of ids |
| `kind` | Weapon-type restrictions — `Brace` melee, `Overwatch` ranged | id → melee / ranged / caster |
| `forgedOnly` | Deepenable if already present, **never added from zero** — `Charges` | Array of ids |
| `neverRolled` | Never granted by any roll at all — the unique enchantments | Array of ids |

**Groups are arrays rather than pairs**, which is what makes them readable. The
threat zone is `["Brace", "Opportunist", "Overwatch"]` and the loader expands it
to the six directed exclusions — so a group is one line, cannot be
half-declared, and adding a fourth member to it is adding a word. That is the
`Symmetric()` closure §1.7 already describes, moved to load time.

The last two are deliberately separate, because they say different things.
`forgedOnly` is about a modifier that would be *harmful* granted from nothing —
a `Charges` on a bow is a cap where there was none (§1.1). `neverRolled` is
about something that must stay scarce — a unique enchantment the enchanter
cannot copy (§3.3). One is a safety rule, the other is an economy rule, and
collapsing them would lose the reason either exists.

### The file that validates everything else is validated first

The original objection to relations-as-data was that editable relations cannot
validate editable content. The answer is that `restricted.json` has its own
well-formedness rules, checked before anything reads it:

| Check | Why |
| --- | --- |
| Every id resolves to a real modifier or enchantment | A typo in a relation is a rule that silently does not apply |
| No id excludes itself | Unsatisfiable, and always a mistake |
| `excludes` closes symmetrically | Half-declared exclusions produce order-dependent bugs (§1.7) |
| `requires` is acyclic | `A` needs `B` needs `A` can never be satisfied |
| Nothing both requires and excludes the same id | That modifier can never legally exist |
| Every `forgedOnly` id is forged on at least one weapon | Otherwise it is unreachable — a rule for nothing |

Then the ordering does the rest. **Relations load before content, so a relations
edit that invalidates an existing weapon aborts startup naming both** — the
weapon that is now illegal *and* the rule that made it so. That is strictly
better than freezing either: the pair is checked together, and the error tells
you which of the two you meant to change.

## 5.6 `weapons.json`

The 32 weapons, the uniques, and the class baselines leave `Weapons.cs`.
Statline, forged spread, area shape, innate enchantment id.

| Check | Rule | From |
| --- | --- | --- |
| Every forged spread is legal | `Allowed(t, kind, present)` | §1.1 |
| Nothing forged too deep | `MaxForged` — `×3`, or `×1` for `Light`/`Resonant` | §1.1 |
| No weapon has fewer than two forged axes | The farm allowance has somewhere to go | §1.2 |
| A unique derives from a variant | Exactly one modifier at `×3`, plus its unique enchantment | §1.5 |
| Casters carry exactly one innate enchantment | Staff fixed by variant, wand a damage type | §3.1 |
| Every unique forged with `Light` or `Resonant` names a unique enchantment | A currency cannot carry an artifact's identity | §1.5 |

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

## 5.7 `enchantments.json`

The catalogue and the unique souls: lock, trigger cost, condition, base potency,
effect reference, `unique` flag, and a damage type where the entry is elemental.

**The effect itself stays code.** "Heal the wielder for damage dealt" is a rule,
so an entry names an `EffectKind` and supplies parameters — the same shape as a
modifier naming a behaviour it does not implement.

**The `unique` flag says one thing only: the tier is pinned at 1** (§3.3). Its
*roll-eligibility* lives in `restricted.json` under `neverRolled`, because that
is the same question asked of `Charges` and belongs in the same place. The
loader cross-checks the two and aborts if they disagree, so declaring one and
forgetting the other is a startup error rather than a unique quietly appearing
in the enchanter's catalogue.

| Check | Rule | From |
| --- | --- | --- |
| Trigger cost is **never zero** | No enchantment fires free, ever | §3.3 |
| `unique` entries appear in `neverRolled` | One question, one answer, cross-checked | §5.5 |
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

## 5.8 `dungeons.json`

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

It is also the clearest payoff from relations being data. A `restricted.json`
edit that makes `Brace` legal on axes changes the Brace dungeon's drop table
**with no other edit anywhere** — because the table was never written down.

| Check | Rule | From |
| --- | --- | --- |
| At least one class can carry the theme | Otherwise the boss drops nothing at all | §4.3 |
| The theme is a **class signature** | So each themed dungeon can drop exactly one named unique, via the Purity coincidence | Settled |
| `retiresOnClear` implies a fixed boss floor | The tutorial's two floors are the enchanter's mana budget, not a roll | §4.3 |
| Only one dungeon retires on clear | Two un-repeatable dungeons is a content decision nobody has made | §4.3 |

The second is the one that will get broken by accident, because a theme that is
*not* a class signature still works — it just quietly means that dungeon can
never produce its unique, and nothing else in the game announces the difference.
